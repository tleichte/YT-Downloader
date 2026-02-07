using Common;
using Microsoft.Extensions.Logging;
using YT_Downloader_DiscordBot.Database.Schemas;
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
            Database.Database.Instance.Mutex.WaitOne();
            LinksSchema.Entry[] expiredLinks = await Database.Database.Instance.LinksSchema.GetExpiredLinks();
            Database.Database.Instance.Mutex.ReleaseMutex();
            
            foreach(LinksSchema.Entry expiredLink in expiredLinks)
            {
                await ActionsHelper.DeleteLink(expiredLink);
                
                AsyncQueues.RemoveMessageQueue.Enqueue(new RemoveMessagePayload
                {
                    UserId = expiredLink.UserId,
                    MessageId = expiredLink.MessageId,
                });
            }
            
            await Task.Delay(30000, stoppingToken);
        }
    }
}