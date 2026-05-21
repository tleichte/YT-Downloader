using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using YT_Downloader_DiscordBot.CF;
using YT_Downloader_DiscordBot.DB.Schemas;
using YT_Downloader_DiscordBot.DB.VersionMigrations;

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
    
    private ILogger m_logger;
    private bool m_brandNewDatabase;

    public static async Task<bool> Initialize(ILogger logger)
    {
        Current = new Database(Config.Current.DatabasePath, logger);
        
        Current.Mutex.WaitOne();
        bool success = await Current.InitializeDb();
        Current.Mutex.ReleaseMutex();

        return success;
    }

    public readonly Mutex Mutex = new();
    
    private readonly SqliteConnection m_connection;

    private Database(string dbFolderPath, ILogger logger)
    {
        m_logger = logger;

        var databasePath = @$"{dbFolderPath}\database.db";
        m_brandNewDatabase = !File.Exists(databasePath);
        
        Directory.CreateDirectory(dbFolderPath);
        m_connection = new SqliteConnection($"Data Source={databasePath};");
        m_connection.Open();

        VersionSchema = new VersionSchema(m_connection);
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

    public readonly VersionSchema VersionSchema;
    public readonly QueueSchema QueueSchema;
    public readonly MP3QueueSchema MP3QueueSchema;
    public readonly LinksSchema LinksSchema;
    public readonly MP3LinksSchema MP3LinksSchema;
    public readonly LinkMetadataSchema LinkMetadataSchema;
    
    private async Task<bool> InitializeDb()
    {
        string[] createTableCommands =
        [
            VersionSchema.CreateTableCommand,
            QueueSchema.CreateTableCommand,
            MP3QueueSchema.CreateTableCommand,
            LinksSchema.CreateTableCommand,
            MP3LinksSchema.CreateTableCommand,
            LinkMetadataSchema.CreateTableCommand,
        ];

        try
        {
            foreach (string createTableCommand in createTableCommands)
            {
                await using var command = new SqliteCommand(createTableCommand, m_connection);
                await command.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            m_logger.LogError(ex, "Error initializing database!");
            return false;
        }

        return await DoMigrations();
    }

    private VersionMigration[] Migrations = 
    {
        new AddNamelessMigration(1)
    };
    
    private async Task<bool> DoMigrations()
    {
        if (m_brandNewDatabase)
        {
            int latestMigration = Migrations.Length > 0 ? Migrations.Last().Version : 0;
            await VersionSchema.SetVersion(latestMigration);
            return true;
        }
        
        int? currentVersion = await VersionSchema.GetVersion() ?? 0;

        var didMigration = false;
        foreach (VersionMigration migration in Migrations)
        {
            if (currentVersion < migration.Version)
            {
                bool didCurrentMigration = await migration.DoMigration(m_connection, m_logger);
                if (!didCurrentMigration)
                {
                    return false;
                }

                didMigration = true;
            }
        }

        if (didMigration && Migrations.Length > 0)
        {
            await VersionSchema.SetVersion(Migrations.Last().Version);
        }

        return true;
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