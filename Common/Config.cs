using Newtonsoft.Json;

namespace Common;

public struct Config
{
    public static Config Current { get; private set; }

    public static void Create()
    {
        const string CONFIG_PATH = @"config\config.json"; 
        if (!File.Exists(CONFIG_PATH))
        {
            throw new FileNotFoundException($"Config file at path not found! {CONFIG_PATH}");
        }
        string configContentsJson = File.ReadAllText(CONFIG_PATH);
        Current = JsonConvert.DeserializeObject<Config>(configContentsJson);
    }
    
    public string BotToken;             // The API token for the discord bot
    public ulong BotOwnerId;            // The owner ID for the discord bot

    public string DatabasePath;         // The path to where the database file is stored
    public string QueueTempPath;        // The path to where temporary youtube download files are stored 

    public string CookiesPath;          // The path to the cookies file for yt-dlp
    
    public string DiskUrlPrefix;        // The prefix to use for the on-disk URL
    public string ServerUrlPrefix;      // The prefix to use for the server URL
    
    public string NamedPipeName;        // The name of the IPC pipe to use between the bot and the worker
    
    public int ThrottleRetryTime;       // The amount of time in minutes before retrying to download when throttled by Youtube 
}