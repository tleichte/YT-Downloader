using Microsoft.Data.Sqlite;
using YT_Downloader_DiscordBot.Helper;

namespace YT_Downloader_DiscordBot.DB.Schemas;

public class QueueSchema : DatabaseSchema<QueueSchema.Entry>
{
    public QueueSchema(SqliteConnection connection) : base(connection)
    {
    }
    
    public struct Entry
    {
        public ulong UserId;
        public string Name;
        public string Url;
        public ulong MessageId;
        public bool Persistent;
        public QueueStatus Status;
        public string Progress;
        public bool Nameless;
    }

    public override string CreateTableCommand => """
                                                 CREATE TABLE IF NOT EXISTS Queue (
                                                     user_id TEXT,
                                                     name TEXT,
                                                     url TEXT,
                                                     message_id TEXT,
                                                     persistent INTEGER,
                                                     status INTEGER,
                                                     progress TEXT,
                                                     nameless INTEGER,
                                                     PRIMARY KEY (user_id, name)
                                                 )
                                                 """;

    protected override string InsertCommand => """
                                               INSERT INTO Queue 
                                                    (user_id, name, url, message_id, persistent, status, progress, nameless)
                                               values 
                                                   ($user_id, $name, $url, $message_id, $persistent, $status, $progress, $nameless)
                                               """;

    protected override void AddInsertParameters(SqliteParameterCollection parameters, Entry entry)
    {
        parameters.AddULong("$user_id", entry.UserId);
        parameters.AddWithValue("$name", entry.Name);
        parameters.AddWithValue("$url", entry.Url);
        parameters.AddULong("$message_id", entry.MessageId);
        parameters.AddWithValue("$persistent", entry.Persistent);
        parameters.AddWithValue("$status", (int)entry.Status);
        parameters.AddWithValue("$progress", entry.Progress);
        parameters.AddWithValue("$nameless", entry.Nameless);
    }

    protected override Entry ParseEntry(SqliteDataReader reader) => new()
    {
        UserId = reader.GetULong(reader.GetOrdinal("user_id")),
        Name = reader.GetString(reader.GetOrdinal("name")),
        Url = reader.GetString(reader.GetOrdinal("url")),
        MessageId = reader.GetULong(reader.GetOrdinal("message_id")),
        Persistent = reader.GetBoolean(reader.GetOrdinal("persistent")),
        Status = (QueueStatus)reader.GetInt32(reader.GetOrdinal("status")),
        Progress =  reader.GetString(reader.GetOrdinal("progress")),
        Nameless = reader.GetBoolean(reader.GetOrdinal("nameless")),
    };

    public async ValueTask<Entry?> GetEntryForName(ulong userId, string name)
    {
        const string QUERY = """
                                    SELECT 
                                        *
                                    FROM
                                         Queue
                                    WHERE
                                         user_id = $user_id AND
                                         name = $name
                                    LIMIT 1;
                                    """;
        return await GetEntry(QUERY, (parameters) =>
            {
                DatabaseHelpers.AddULong(parameters, "$user_id", userId);
                parameters.AddWithValue("$name", name);
            });
    }

    public async Task ConvertNameless(ulong userId, string oldName, string newName)
    {
        const string COMMAND = """
                               UPDATE Queue
                               SET nameless = $nameless,
                                   name = $new_name
                               WHERE 
                                   user_id = $user_id AND
                                   name = $old_name
                               """;
        await Execute(COMMAND, parameters =>
        {
            parameters.AddULong("$user_id", userId);
            parameters.AddWithValue("$old_name", oldName);
            parameters.AddWithValue("$new_name", newName);
            parameters.AddWithValue("$nameless", false);
        });
    }

    public async ValueTask<Entry?> GetFirstEntry()
    {
        const string QUERY = """
                             SELECT * FROM Queue ORDER BY ROWID ASC LIMIT 1;
                             """;
        return await GetEntry(QUERY, _ => { });
    }

    public async Task RemoveFirstQueueEntry()
    {
        const string COMMAND = """
                               DELETE FROM Queue 
                                      WHERE 
                                          rowid IN 
                                          (SELECT rowid FROM QUEUE ORDER BY ROWID ASC LIMIT 1);
                               """;
        await Execute(COMMAND, _ => { });
    }

    public async Task SetQueueStatus(QueueStatus status, ulong userId, string name)
    {
        const string COMMAND = """
                               UPDATE Queue SET status = $status WHERE user_id = $user_id AND name = $name
                               """;
        await Execute(COMMAND, parameters =>
        {
            parameters.AddWithValue("$status", (int)status);
            DatabaseHelpers.AddULong(parameters, "$user_id", userId);
            parameters.AddWithValue("$name", name);
        });
    }

    public async Task SetQueueProgress(ulong userId, string name, string progress)
    {
        const string COMMAND = """
                               UPDATE Queue SET progress = $progress WHERE user_id = $user_id AND name = $name
                               """;
        await Execute(COMMAND, parameters =>
        {
            parameters.AddWithValue("$progress", progress);
            DatabaseHelpers.AddULong(parameters, "$user_id", userId);
            parameters.AddWithValue("$name", name);
        });
    }

    public async ValueTask<Entry?> GetEntryForMessageId(ulong messageId)
    {
        const string QUERY = """
                             SELECT * FROM Queue WHERE message_id = $message_id LIMIT 1;
                             """;
        return await GetEntry(QUERY, parameters =>
        {
            parameters.AddULong("$message_id", messageId);
        });
    }
}