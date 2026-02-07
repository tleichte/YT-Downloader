using System.Text;
using Common;
using Common.DatabaseSchemas;
using Microsoft.Extensions.Logging;
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
                Database.Instance.Mutex.WaitOne();
                QueueSchema.Entry? entry;
                try
                {
                    entry = await Database.Instance.QueueSchema.GetFirstEntry();
                }
                finally
                {
                    Database.Instance.Mutex.ReleaseMutex();
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
                                progressString.Append($"{m_progress.Progress * 100}%");
                                if (!string.IsNullOrEmpty(m_progress.ETA))
                                {
                                    progressString.Append($" - ({m_progress.ETA})");
                                }

                                Database.Instance.Mutex.WaitOne();
                                try
                                {
                                    await Database.Instance.QueueSchema.SetQueueProgress(entry.Value.UserId,
                                        entry.Value.Name,
                                        progressString.ToString());
                                }
                                finally
                                {
                                    Database.Instance.Mutex.ReleaseMutex();
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
    
    private async Task TryRunDownload(QueueSchema.Entry queueEntry, CancellationToken stoppingToken)
    {
        Database.Instance.Mutex.WaitOne();
        await Database.Instance.QueueSchema.SetQueueStatus(QueueStatus.Downloading, queueEntry.UserId, queueEntry.Name);
        Database.Instance.Mutex.ReleaseMutex();
        
        Directory.CreateDirectory(Helpers.GetVideoDiskFolderPathForUser(queueEntry.UserId));
        
        _ = Task.Run(async () =>
        {
            try
            {
                VideoDataResult videoData = await Downloader.RunVideoDataFetchForURL(queueEntry.Url, stoppingToken);

                if (!videoData.Success)
                {
                    AsyncQueues.Results.Enqueue(DownloadResult.CreateFail(queueEntry, null, DownloadFailReason.VideoFetchError));                    
                    return;
                }
                
                // 
                Database.Instance.Mutex.WaitOne();
                LinksSchema.Entry[] existingVideoIdEntries = await Database.Instance.LinksSchema.GetEntriesForVideoId(videoData.VideoId ?? string.Empty);
                Database.Instance.Mutex.ReleaseMutex();

                if (existingVideoIdEntries.Length > 0)
                {
                    Database.Instance.Mutex.WaitOne();
                    await Database.Instance.QueueSchema.RemoveFirstQueueEntry();
                    Database.Instance.Mutex.ReleaseMutex();
                    
                    if (existingVideoIdEntries.Any(e => e.UserId == queueEntry.UserId))
                    {
                        var alreadyDownloadedResult = DownloadResult.CreateFail(queueEntry, videoData.Data,
                            DownloadFailReason.VideoAlreadyDownloaded);
                        AsyncQueues.Results.Enqueue(alreadyDownloadedResult);
                        return;
                    }
                    
                    Database.Instance.Mutex.WaitOne();
                    await Database.Instance.LinksSchema.AddEntry(new LinksSchema.Entry
                    {
                        UserId = queueEntry.UserId,
                        Name = queueEntry.Name,
                        VideoId = videoData.VideoId,
                        MessageId = queueEntry.MessageId,
                        DeleteAfter = queueEntry.Persistent
                            ? DateTime.MaxValue
                            : DateTime.Now + TimeSpan.FromDays(Config.Current.ExpirationDays)
                    });
                    Database.Instance.Mutex.ReleaseMutex();
                    
                    
                    LinksSchema.Entry existingEntry = existingVideoIdEntries[0];
                    string prevFilePath = Helpers.GetVideoDiskFilePath(existingEntry.UserId, videoData.VideoId);
                    string newFilePath = Helpers.GetVideoDiskFilePath(queueEntry.UserId, videoData.VideoId);
                    File.Copy(prevFilePath, newFilePath);
                    AsyncQueues.Results.Enqueue(DownloadResult.CreateSuccess(queueEntry, videoData.Data));
                    return;
                }
                
                Downloader.VideoDownloadResult result = await Downloader.RunDownload(queueEntry.Url, m_progress, cancellationToken:stoppingToken);
                if (!result.Result.Success)
                {
                    Database.Instance.Mutex.WaitOne();
                    await Database.Instance.QueueSchema.RemoveFirstQueueEntry();
                    Database.Instance.Mutex.ReleaseMutex();
                    
                    AsyncQueues.Results.Enqueue(DownloadResult.CreateFail(queueEntry, videoData.Data, result.Result.FailValue));

                    return;
                }
                     
                string moveToPath = Helpers.GetVideoDiskFilePath(queueEntry.UserId, result.Metadata.VideoId);
                File.Move(result.Path, moveToPath);

                Database.Instance.Mutex.WaitOne();

                try
                {
                    await Database.Instance.QueueSchema.RemoveFirstQueueEntry();
                    await Database.Instance.LinksSchema.AddEntry(new LinksSchema.Entry
                    {
                        UserId = queueEntry.UserId,
                        Name = queueEntry.Name,
                        VideoId = result.Metadata.VideoId,
                        MessageId = queueEntry.MessageId,
                        DeleteAfter = queueEntry.Persistent
                            ? DateTime.MaxValue
                            : DateTime.Now + TimeSpan.FromDays(Config.Current.ExpirationDays)
                    });
                    await Database.Instance.LinkMetadataSchema.AddEntry(result.Metadata);
                }
                finally
                {
                    Database.Instance.Mutex.ReleaseMutex();
                }
                    
                AsyncQueues.Results.Enqueue(DownloadResult.CreateSuccess(queueEntry, videoData.Data));
            }
            catch (Exception e)
            {
                m_logger.LogError(e, "Error while running video download!");
            }
        }, stoppingToken);
        
    }
}