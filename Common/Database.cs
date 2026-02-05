using Common.DatabaseSchemas;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace Common;

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
    public static Database Instance { get; private set; }
    static Database()
    {
        Instance = new Database(Config.Current.DatabasePath);
        Instance.InitializeDb();
    }

    public readonly Mutex Mutex = new();
    
    private readonly SqliteConnection m_connection;

    private Database(string connectionString)
    {
        m_connection = new SqliteConnection(connectionString);
        m_connection.Open();
        
        QueueSchema = new QueueSchema(m_connection);
        QueueProgressSchema = new QueueProgressSchema(m_connection);
        LinksSchema = new LinksSchema(m_connection);
        LinkMetadataSchema = new LinkMetadataSchema(m_connection);

    }
    ~Database()
    {
        m_connection.Close();
    }

    public readonly QueueSchema QueueSchema;
    public readonly QueueProgressSchema QueueProgressSchema;
    public readonly LinksSchema LinksSchema;
    public readonly LinkMetadataSchema LinkMetadataSchema;
    
    private void InitializeDb()
    {

        string[] createTableCommands =
        [
            QueueSchema.CreateTableCommand,
            QueueProgressSchema.CreateTableCommand,
            LinksSchema.CreateTableCommand,
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