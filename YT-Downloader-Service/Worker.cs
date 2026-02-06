using System.IO.Pipes;
using Common;
using Common.DatabaseSchemas;
using Newtonsoft.Json;
using YT_Downloader_Service.Pipes;

namespace YT_Downloader_Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> m_logger;

    private VideoDownloadProgress m_currentProgress;
    
    public Worker(ILogger<Worker> logger)
    {
        m_logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Config.Create();
        
        Subscribe();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            Database.Instance.Mutex.WaitOne();
            
            await ServiceHelpers.DeleteExpiredLinks();

            QueueSchema.Entry? entry = await Database.Instance.QueueSchema.GetFirstEntry();
            
            Database.Instance.Mutex.ReleaseMutex();
            
            if (entry != null)
            {
                if (entry.Value.Status == QueueStatus.Queued)
                {
                    m_currentProgress = new VideoDownloadProgress();
                    await TryRunDownload(entry.Value, stoppingToken);
                }
                else if (entry.Value.Status == QueueStatus.Downloading)
                {
                    var progressString = $"{m_currentProgress.Progress*100}% - ({m_currentProgress.ETA})";
                    await Database.Instance.QueueSchema.SetQueueProgress(entry.Value.UserId, entry.Value.Name,
                        progressString);
                }
            }
            
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task TryRunDownload(QueueSchema.Entry queueEntry, CancellationToken stoppingToken)
    {
        if (queueEntry.Status == QueueStatus.Queued)
        {
            Database.Instance.Mutex.WaitOne();
            
            await Database.Instance.QueueSchema.SetQueueStatus(QueueStatus.Downloading, queueEntry.UserId, queueEntry.Name);
            
            Database.Instance.Mutex.ReleaseMutex();
            Task _ = Task.Run(async () =>
            {
                var downloader = new Downloader();
                Downloader.VideoDownloadResult result = await downloader.RunDownload(queueEntry.Url, m_currentProgress, cancellationToken:stoppingToken);
                if (result.Result.Success)
                {
                    Directory.CreateDirectory(Helpers.GetVideoDiskFolderPathForUser(queueEntry.UserId));
                 
                    string moveToPath = Helpers.GetVideoDiskFilePath(queueEntry.UserId, result.Metadata.VideoId);
                    File.Move(result.Path, moveToPath);

                    Database.Instance.Mutex.WaitOne();

                    await Database.Instance.QueueSchema.RemoveFirstQueueEntry();
                    await Database.Instance.LinksSchema.AddEntry(new LinksSchema.Entry
                    {
                        UserId = queueEntry.UserId,
                        Name = queueEntry.Name,
                        VideoId = result.Metadata.VideoId,
                        MessageId = queueEntry.MessageId,
                        DeleteAfter = queueEntry.Persistent ? DateTime.MaxValue : DateTime.Now + TimeSpan.FromDays(Config.Current.ExpirationDays)
                    });
                    await Database.Instance.LinkMetadataSchema.AddEntry(result.Metadata);
                    
                    Database.Instance.Mutex.ReleaseMutex();
                }
                
                await new DownloadResultPipeClient().Send(new DownloadResultPipeMessage
                {
                    UserId = queueEntry.UserId,
                    Name = queueEntry.Name,
                    OriginalUrl = queueEntry.Url,
                    MessageId = queueEntry.MessageId,
                    Result = result.Result,
                });
            }, stoppingToken);
        }
    }
    
    private void Subscribe()
    {
        QueuePipeServer server = new();
        server.Start();
    }
    
    
    
}