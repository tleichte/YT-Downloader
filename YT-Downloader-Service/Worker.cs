using Common;
using Common.DatabaseSchemas;
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
                    await ServiceHelpers.TryRunDownload(entry.Value, m_currentProgress, stoppingToken);
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
    
    private void Subscribe()
    {
        QueuePipeServer server = new();
        server.Start();
    }
}