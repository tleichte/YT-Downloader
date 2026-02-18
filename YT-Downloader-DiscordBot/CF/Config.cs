using Newtonsoft.Json;

namespace YT_Downloader_DiscordBot.CF;

public struct Config
{
    public static Config Current { get; private set; }

    public static void Create()
    {
        string configPath = $@"{AppContext.BaseDirectory}\config\config.json"; 
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file at path not found! {configPath}");
        }
        string configContentsJson = File.ReadAllText(configPath);
        Current = JsonConvert.DeserializeObject<Config>(configContentsJson);
    }
    
    public string BotToken;                         // The API token for the discord bot
    public ulong BotOwnerId;                        // The owner ID for the discord bot

    public string DatabasePath;                     // The path to where the database file is stored
    public string TempDownloadsPath;                // The path to where temporary youtube download files are stored 
    public string DownloadOutputPath;               // The path to where download outputs are stored

    public bool ShouldAlwaysUseCookies;             // Whether the bot should always use cookies as opposed to a fallback (timeout issue?)
    public string CookiesPath;                      // The path to the cookies file for yt-dlp
    
    public string ProcessingScriptPath;             // The path for the powershell script that does script processing
    public string ProcessingScriptArguments;        // The arguments to use for the processing script. Includes {0} and {1} for input and output path
    
    public string DiskUrlPrefix;                    // The prefix to use for the on-disk URL
    public string ServerUrlPrefix;                  // The prefix to use for the server URL

}