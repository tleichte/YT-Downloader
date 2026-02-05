using Common;
using Common.DatabaseSchemas;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YT_Downloader_DiscordBot.Pipes;

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

        var downloadResultServer = new DownloadResultPipeServer();
        downloadResultServer.Start();
        
        while (!stoppingToken.IsCancellationRequested)
        {

            await BotHelpers.TryUpdateQueueMessage(m_client);
            await BotHelpers.CheckForDownloadResults(m_client);
            
            await Task.Delay(5000, stoppingToken);
        }
        
        await m_client.DisconnectAsync();
    }
}