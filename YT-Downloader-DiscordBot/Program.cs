// See https://aka.ms/new-console-template for more information

using Common;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using YT_Downloader_DiscordBot;


Config.Create();

DiscordConfiguration config = new()
{
    Token = Config.Current.BotToken,
    Intents = DiscordIntents.AllUnprivileged,
};


DiscordClient client = new(config);

SlashCommandsExtension slash = client.UseSlashCommands();
slash.RegisterCommands<SlashCommands>();

DiscordActivity status = new("Give me links :>", ActivityType.Custom);

await client.ConnectAsync(status, UserStatus.Online);

await Task.Delay(-1);