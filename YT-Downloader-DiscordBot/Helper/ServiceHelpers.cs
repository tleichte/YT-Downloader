using Common;
using Common.DatabaseSchemas;
using YT_Downloader_DiscordBot.Helper;

namespace YT_Downloader_Service;

public static class ServiceHelpers
{
    
    public static async Task DeleteExpiredLinks()
    {
        
    }


    public static async Task TryRunDownload(QueueSchema.Entry queueEntry, VideoDownloadProgress progress, CancellationToken stoppingToken)
    {
        if (queueEntry.Status == QueueStatus.Queued)
        {
            Database.Instance.Mutex.WaitOne();
            
            await Database.Instance.QueueSchema.SetQueueStatus(QueueStatus.Downloading, queueEntry.UserId, queueEntry.Name);
            
            Database.Instance.Mutex.ReleaseMutex();
            Task _ = Task.Run(async () =>
            {
                var downloader = new Downloader();
                Downloader.VideoDownloadResult result = await downloader.RunDownload(queueEntry.Url, progress, cancellationToken:stoppingToken);
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
                
                AsyncQueues.Results.Enqueue(new DownloadResult
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
    
}