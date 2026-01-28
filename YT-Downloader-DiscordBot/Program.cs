// See https://aka.ms/new-console-template for more information

using Common;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using YT_Downloader_DiscordBot;

// Read Config
JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));

string TOKEN = "TODO";

DiscordConfiguration config = new()
{
    Token = TOKEN,
    Intents = DiscordIntents.AllUnprivileged,
};

DiscordClient client = new(config);

var slash = client.UseSlashCommands();
slash.RegisterCommands<SlashCommands>();

DiscordActivity status = new("with myself", ActivityType.Playing);

await client.ConnectAsync(status, UserStatus.Online);

await Task.Delay(-1);