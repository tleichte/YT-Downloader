using System.Text;
using Common;
using Microsoft.Extensions.Logging;
using YT_Downloader_DiscordBot.DB;
using YT_Downloader_DiscordBot.DB.Schemas;
using YT_Downloader_DiscordBot.Helper;
using YT_Downloader_Service;

namespace YT_Downloader_DiscordBot.BackgroundTasks;

public class QueueDownloaderBackgroundTask
{
    private VideoDownloadProgress m_progress;
    private ILogger<BotWorker> m_logger;
    
    public void Start(ILogger<BotWorker> logger, CancellationToken stoppingToken)
    {
        m_logger = logger;
        Task.Run(async () =>
        {
            await Run(stoppingToken);
        }, stoppingToken);
    }
    
    private async Task Run(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Database.Current.Mutex.WaitOne();
                QueueSchema.Entry? entry;
                try
                {
                    entry = await Database.Current.QueueSchema.GetFirstEntry();
                }
                finally
                {
                    Database.Current.Mutex.ReleaseMutex();
                }

                if (entry != null)
                {
                    switch (entry.Value.Status)
                    {
                        case QueueStatus.Queued:
                        {
                            // Start video download
                            m_progress = new VideoDownloadProgress();
                            await TryRunDownload(entry.Value, stoppingToken);
                            break;
                        }
                        case QueueStatus.Downloading:
                        {
                            if (m_progress != null)
                            {
                                StringBuilder progressString = new();
                                progressString.Append($"{(m_progress.Progress * 100):0.#}%");
                                if (!string.IsNullOrEmpty(m_progress.ETA))
                                {
                                    progressString.Append($" - ({m_progress.ETA})");
                                }

                                Database.Current.Mutex.WaitOne();
                                try
                                {
                                    await Database.Current.QueueSchema.SetQueueProgress(entry.Value.UserId,
                                        entry.Value.Name,
                                        progressString.ToString());
                                }
                                finally
                                {
                                    Database.Current.Mutex.ReleaseMutex();
                                }
                            }

                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error while running Queue Background Task!");
            }
            
            await Task.Delay(1000, stoppingToken);
        }
    }

    private static async Task DoDownloadFail(QueueSchema.Entry queueEntry, VideoDataResult? videoData, DownloadFailReason failReason)
    {
        Database.Current.Mutex.WaitOne();
        await Database.Current.QueueSchema.RemoveFirstQueueEntry();
        Database.Current.Mutex.ReleaseMutex();
        
        AsyncQueues.DownloadResults.Enqueue(DownloadResult.CreateFail(queueEntry, videoData?.Data, failReason));
    }

    private static async Task DoDownloadSuccess(QueueSchema.Entry queueEntry, VideoDataResult videoData)
    {
        Database.Current.Mutex.WaitOne();
        
        await Database.Current.QueueSchema.RemoveFirstQueueEntry();
        await Database.Current.LinksSchema.AddEntry(new LinksSchema.Entry
        {
            UserId = queueEntry.UserId,
            Name = queueEntry.Name,
            VideoId = videoData.VideoId ?? string.Empty,
            MessageId = queueEntry.MessageId,
            DeleteAfter = queueEntry.Persistent
                ? DateTime.MaxValue
                : DateTime.Now + TimeSpan.FromDays(Constants.ExpirationDays)
        });
        
        Database.Current.Mutex.ReleaseMutex();
        
        AsyncQueues.DownloadResults.Enqueue(DownloadResult.CreateSuccess(queueEntry, videoData.Data));
    }
    
    private async Task TryRunDownload(QueueSchema.Entry queueEntry, CancellationToken stoppingToken)
    {
        Database.Current.Mutex.WaitOne();
        await Database.Current.QueueSchema.SetQueueStatus(QueueStatus.Downloading, queueEntry.UserId, queueEntry.Name);
        Database.Current.Mutex.ReleaseMutex();
        
        Directory.CreateDirectory(Helpers.GetVideoDiskFolderPathForUser(queueEntry.UserId));
        
        _ = Task.Run(async () =>
        {
            try
            {
                VideoDataResult videoData = await Downloader.RunVideoDataFetchForURL(queueEntry.Url, stoppingToken);

                if (!videoData.Success || videoData.VideoId == null || videoData.Data == null)
                {
                    await DoDownloadFail(queueEntry, null, DownloadFailReason.VideoFetchError);                    
                    return;
                }
                
                string videoId = videoData.VideoId;
                
                Database.Current.Mutex.WaitOne();
                LinksSchema.Entry[] existingVideoIdEntries = await Database.Current.LinksSchema.GetEntriesForVideoId(videoData.VideoId ?? string.Empty);
                Database.Current.Mutex.ReleaseMutex();

                if (existingVideoIdEntries.Length > 0)
                {
                    if (existingVideoIdEntries.Any(e => e.UserId == queueEntry.UserId))
                    {
                        await DoDownloadFail(queueEntry, videoData, DownloadFailReason.VideoAlreadyDownloaded);
                        return;
                    }
                    
                    LinksSchema.Entry existingEntry = existingVideoIdEntries[0];
                    string prevFilePath = Helpers.GetVideoDiskFilePath(existingEntry.UserId, videoId);
                    string newFilePath = Helpers.GetVideoDiskFilePath(queueEntry.UserId, videoId);
                    File.Copy(prevFilePath, newFilePath);
                    
                    await DoDownloadSuccess(queueEntry, videoData);
                    return;
                }
                
                Downloader.VideoDownloadResult result = await Downloader.RunDownload(queueEntry.Url, m_progress, cancellationToken:stoppingToken);
                if (!result.Result.Success)
                {
                    await DoDownloadFail(queueEntry, videoData, result.Result.FailValue);
                    return;
                }
                
                string moveToPath = Helpers.GetVideoDiskFilePath(queueEntry.UserId, videoId);
                File.Move(result.Path, moveToPath);

                Database.Current.Mutex.WaitOne();
                await Database.Current.LinkMetadataSchema.AddEntry(new LinkMetadataSchema.Entry
                {
                    VideoId = videoId,
                    VideoTitle = videoData.Data?.Title ?? string.Empty,
                    VideoAuthor = videoData.Data?.Channel ?? string.Empty,
                    OriginalUrl = queueEntry.Url,
                    Duration = videoData.Data?.Duration ?? 0
                });
                Database.Current.Mutex.ReleaseMutex();
                
                await DoDownloadSuccess(queueEntry, videoData);
            }
            catch (Exception e)
            {
                m_logger.LogError(e, "Error while running video download!");
            }
        }, stoppingToken);
        
    }
}