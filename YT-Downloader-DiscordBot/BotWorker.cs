using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        while (!stoppingToken.IsCancellationRequested)
        {
            
            
            await Task.Delay(1000, stoppingToken);
        }
        
        await m_client.DisconnectAsync();
    }
}