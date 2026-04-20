using DSharpPlus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YT_Downloader_DiscordBot.BackgroundTasks;
using YT_Downloader_DiscordBot.Helper;

namespace YT_Downloader_DiscordBot;

public class BotWorker : BackgroundService
{
    private DiscordClient m_client;
    private readonly ILogger<BotWorker> m_logger;
    private readonly IHostApplicationLifetime m_applicationLifetime;
    
    public BotWorker(ILogger<BotWorker> logger, IHostApplicationLifetime lifetime, DiscordClient client)
    {
        m_logger = logger;
        m_applicationLifetime = lifetime;
        m_client = client;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await m_client.ConnectAsync();

        new QueueDownloaderBackgroundTask().Start(m_logger, stoppingToken);
        new MP3QueueDownloaderBackgroundTask().Start(m_logger, stoppingToken);
        new DeleteExpiredLinksBackgroundTask().Start(m_logger, stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await BotHelpers.TryUpdateQueueMessage(m_client);
            await BotHelpers.TryUpdateMP3QueueMessage(m_client);
            await BotHelpers.CheckForDownloadResults(m_client);
            await BotHelpers.CheckForMP3DownloadResults(m_client);
            await BotHelpers.CheckForRemoveMessages(m_client);
            
            await Task.Delay(3000, stoppingToken); // Update messages max every 3 seconds
        }
        
        await m_client.DisconnectAsync();
    }
}