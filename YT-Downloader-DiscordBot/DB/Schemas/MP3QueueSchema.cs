using Microsoft.Data.Sqlite;
using YT_Downloader_DiscordBot.Helper;

namespace YT_Downloader_DiscordBot.DB.Schemas;

public class MP3QueueSchema : DatabaseSchema<MP3QueueSchema.Entry>
{
    public struct Entry
    {
        public ulong UserId;
        public string Url;
        public ulong MessageId;
        public QueueStatus Status;
        public string Progress;
    }

    public MP3QueueSchema(SqliteConnection connection) : base(connection)
    {
    }

    public override string CreateTableCommand => """
                                                 CREATE TABLE IF NOT EXISTS MP3Queue (
                                                     user_id TEXT,
                                                     url TEXT,
                                                     message_id TEXT,
                                                     status INTEGER,
                                                     progress TEXT,
                                                     PRIMARY KEY (user_id, url)
                                                 )
                                                 """;
    protected override string InsertCommand => """
                                               INSERT INTO MP3Queue 
                                                    (user_id, url, message_id, status, progress)
                                               values 
                                                   ($user_id, $url, $message_id, $status, $progress)
                                               """;
    protected override void AddInsertParameters(SqliteParameterCollection parameters, Entry entry)
    {
        parameters.AddULong("$user_id", entry.UserId);
        parameters.AddWithValue("$url", entry.Url);
        parameters.AddULong("$message_id", entry.MessageId);
        parameters.AddWithValue("$status", (int)entry.Status);
        parameters.AddWithValue("$progress", entry.Progress);
    }

    protected override Entry ParseEntry(SqliteDataReader reader) => new()
    {
        UserId = reader.GetULong(reader.GetOrdinal("user_id")),
        Url = reader.GetString(reader.GetOrdinal("url")),
        MessageId = reader.GetULong(reader.GetOrdinal("message_id")),
        Status = (QueueStatus)reader.GetInt32(reader.GetOrdinal("status")),
        Progress =  reader.GetString(reader.GetOrdinal("progress")),
    };
    
    public async ValueTask<Entry?> GetEntryForUrl(ulong userId, string url)
    {
        const string QUERY = """
                             SELECT 
                                 *
                             FROM
                                  MP3Queue
                             WHERE
                                  user_id = $user_id AND
                                  url = $url
                             LIMIT 1;
                             """;
        return await GetEntry(QUERY, (parameters) =>
        {
            DatabaseHelpers.AddULong(parameters, "$user_id", userId);
            parameters.AddWithValue("$url", url);
        });
    }
    
    public async ValueTask<Entry?> GetFirstEntry()
    {
        const string QUERY = """
                             SELECT * FROM MP3Queue ORDER BY ROWID ASC LIMIT 1;
                             """;
        return await GetEntry(QUERY, _ => { });
    }

    public async Task RemoveFirstQueueEntry()
    {
        const string COMMAND = """
                               DELETE FROM MP3Queue 
                                      WHERE 
                                          rowid IN 
                                          (SELECT rowid FROM MP3Queue ORDER BY ROWID ASC LIMIT 1);
                               """;
        await Execute(COMMAND, _ => { });
    }
    
    public async Task SetQueueStatus(QueueStatus status, ulong userId, string url)
    {
        const string COMMAND = """
                               UPDATE MP3Queue SET status = $status WHERE user_id = $user_id AND url = $url
                               """;
        await Execute(COMMAND, parameters =>
        {
            parameters.AddWithValue("$status", (int)status);
            DatabaseHelpers.AddULong(parameters, "$user_id", userId);
            parameters.AddWithValue("url", url);
        });
    }
    
    public async Task SetQueueProgress(ulong userId, string url, string progress)
    {
        const string COMMAND = """
                               UPDATE MP3Queue SET progress = $progress WHERE user_id = $user_id AND url = $url
                               """;
        await Execute(COMMAND, parameters =>
        {
            parameters.AddWithValue("$progress", progress);
            DatabaseHelpers.AddULong(parameters, "$user_id", userId);
            parameters.AddWithValue("$url", url);
        });
    }
    
    public async ValueTask<Entry?> GetEntryForMessageId(ulong messageId)
    {
        const string QUERY = """
                             SELECT * FROM MP3Queue WHERE message_id = $message_id LIMIT 1;
                             """;
        return await GetEntry(QUERY, parameters =>
        {
            parameters.AddULong("$message_id", messageId);
        });
    }
}