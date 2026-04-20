using Microsoft.Data.Sqlite;
using YT_Downloader_DiscordBot.CF;
using YT_Downloader_DiscordBot.DB.Schemas;

namespace YT_Downloader_DiscordBot.DB;

public static class DatabaseHelpers
{
    public static void AddULong(this SqliteParameterCollection parameters, string paramName, ulong value)
    {
        parameters.AddWithValue(paramName, value.ToString());
    }

    public static ulong GetULong(this SqliteDataReader reader, int ordinal)
    {
        return ulong.Parse(reader.GetString(ordinal));
    }
}

public class Database
{
    public static Database Current { get; private set; }
    static Database()
    {
        Current = new Database(Config.Current.DatabasePath);
        Current.InitializeDb();
    }

    public readonly Mutex Mutex = new();
    
    private readonly SqliteConnection m_connection;

    private Database(string dbFolderPath)
    {
        Directory.CreateDirectory(dbFolderPath);
        m_connection = new SqliteConnection($@"Data Source={dbFolderPath}\database.db;");
        m_connection.Open();
        
        QueueSchema = new QueueSchema(m_connection);
        MP3QueueSchema = new MP3QueueSchema(m_connection);
        LinksSchema = new LinksSchema(m_connection);
        MP3LinksSchema = new MP3LinksSchema(m_connection);
        LinkMetadataSchema = new LinkMetadataSchema(m_connection);

    }
    ~Database()
    {
        m_connection.Close();
    }

    public readonly QueueSchema QueueSchema;
    public readonly MP3QueueSchema MP3QueueSchema;
    public readonly LinksSchema LinksSchema;
    public readonly MP3LinksSchema MP3LinksSchema;
    public readonly LinkMetadataSchema LinkMetadataSchema;
    
    private void InitializeDb()
    {

        string[] createTableCommands =
        [
            QueueSchema.CreateTableCommand,
            MP3QueueSchema.CreateTableCommand,
            LinksSchema.CreateTableCommand,
            MP3LinksSchema.CreateTableCommand,
            LinkMetadataSchema.CreateTableCommand,
        ];

        foreach (string createTableCommand in createTableCommands)
        {
            using var command = new SqliteCommand(createTableCommand, m_connection);
            command.ExecuteNonQuery();
        }
    }
   
    public async Task RemoveLink(ulong userId, string name)
    {
        LinksSchema.Entry? entry = await LinksSchema.GetEntryForName(userId, name);
        if (entry == null)
        {
            return;
        }
        string videoId = entry.Value.VideoId;

        await LinksSchema.RemoveLink(userId, name);

        LinksSchema.Entry[] remainingEntries = await LinksSchema.GetEntriesForVideoId(videoId);
        if (remainingEntries.Length == 0)
        {
            await LinkMetadataSchema.RemoveLinkEntry(videoId);
        }
    }
}