using Newtonsoft.Json;

namespace Common;

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

    public string CookiesPath;                      // The path to the cookies file for yt-dlp
    
    public string DiskUrlPrefix;                    // The prefix to use for the on-disk URL
    public string ServerUrlPrefix;                  // The prefix to use for the server URL
}