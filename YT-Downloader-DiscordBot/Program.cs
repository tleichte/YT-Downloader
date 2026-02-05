// See https://aka.ms/new-console-template for more information

using Common;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YT_Downloader_DiscordBot;


Config.Create();

await Host.CreateDefaultBuilder()
    .UseConsoleLifetime()
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<BotWorker>();
        services.AddDiscordClient(Config.Current.BotToken, DiscordIntents.AllUnprivileged);    
        services.AddCommandsExtension((provider, extension) =>
        {
            extension.AddCommands<Commands>();
        });
    })
    .RunConsoleAsync();

await Task.Delay(-1);