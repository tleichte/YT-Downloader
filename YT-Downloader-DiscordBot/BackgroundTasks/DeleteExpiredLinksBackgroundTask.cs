using Common;
using Common.DatabaseSchemas;
using Microsoft.Extensions.Logging;
using YT_Downloader_DiscordBot.Helper;

namespace YT_Downloader_DiscordBot.BackgroundTasks;

public class DeleteExpiredLinksBackgroundTask
{
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
            Database.Instance.Mutex.WaitOne();
            LinksSchema.Entry[] expiredLinks = await Database.Instance.LinksSchema.GetExpiredLinks();
            Database.Instance.Mutex.ReleaseMutex();
            
            foreach(LinksSchema.Entry expiredLink in expiredLinks)
            {
                Database.Instance.Mutex.WaitOne();
                await Database.Instance.RemoveLink(expiredLink.UserId, expiredLink.VideoId);
                Database.Instance.Mutex.ReleaseMutex();
                
                File.Delete(Helpers.GetVideoDiskFilePath(expiredLink.UserId, expiredLink.VideoId));
                
                AsyncQueues.ExpiredLinkMessages.Enqueue(new LinkRemovedMessage
                {
                    UserId = expiredLink.UserId,
                    MessageId = expiredLink.MessageId,
                });
            }
            
            await Task.Delay(30000, stoppingToken);
        }
    }
}