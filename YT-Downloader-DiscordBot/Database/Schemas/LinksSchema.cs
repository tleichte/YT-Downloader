using Microsoft.Data.Sqlite;

namespace YT_Downloader_DiscordBot.Database.Schemas;

public class LinksSchema : DatabaseSchema<LinksSchema.Entry>
{
    public struct Entry
    {
        public ulong UserId;
        public string Name;
        public string VideoId;
        public ulong MessageId;
        public DateTime DeleteAfter;
    }

    public LinksSchema(SqliteConnection connection) : base(connection)
    {
    }

    public override string CreateTableCommand => """
                                                 CREATE TABLE IF NOT EXISTS Links (
                                                     user_id TEXT,
                                                     name TEXT,
                                                     video_id TEXT,
                                                     message_id TEXT,
                                                     delete_after INTEGER,
                                                     PRIMARY KEY (user_id, name)
                                                 )
                                                 """;

    protected override string InsertCommand => """
                                               INSERT INTO Links
                                                   (user_id, name, video_id, message_id, delete_after)
                                               values 
                                                   ($user_id, $name, $video_id, $message_id, $delete_after)
                                               """;

    protected override void AddInsertParameters(SqliteParameterCollection parameters, Entry entry)
    {
        parameters.AddULong("$user_id", entry.UserId);
        parameters.AddWithValue("$name", entry.Name);
        parameters.AddWithValue("$video_id", entry.VideoId);
        parameters.AddULong("$message_id", entry.MessageId);
        parameters.AddWithValue("$delete_after", entry.DeleteAfter);
    }

    protected override Entry ParseEntry(SqliteDataReader reader) => new()
    {
        UserId = reader.GetULong(reader.GetOrdinal("user_id")),
        Name = reader.GetString(reader.GetOrdinal("name")),
        VideoId = reader.GetString(reader.GetOrdinal("video_id")),
        MessageId = reader.GetULong(reader.GetOrdinal("message_id")),
        DeleteAfter = reader.GetDateTime(reader.GetOrdinal("delete_after")),
    };

    public async Task RemoveLink(ulong userId, string name)
    {
        const string COMMAND = """
                             DELETE FROM Links
                             WHERE
                                  user_id = $user_id AND
                                  name = $name
                             """;
        await Execute(COMMAND, parameters =>
        {
            parameters.AddULong("$user_id", userId);
            parameters.AddWithValue("$name", name);
        });
    }

    public async Task<Entry[]> GetExpiredLinks()
    {
        const string COMMAND = """
                                        SELECT * FROM Links 
                                        WHERE
                                            delete_after < $currentDate
                                     """;
        return await GetEntries(COMMAND, parameters =>
        {
            parameters.AddWithValue("$currentDate", DateTime.Now);
        });
    }
    
    public async ValueTask<Entry?> GetEntryForName(ulong userId, string name)
    {
        const string QUERY = """
                             SELECT * FROM Links
                             WHERE
                                  user_id = $user_id AND
                                  name = $name
                             """;
        return await GetEntry(QUERY, parameters =>
        {
            parameters.AddULong("$user_id", userId);
            parameters.AddWithValue("$name", name);
        });
    }
    
    public async ValueTask<Entry?> GetEntryForVideoId(ulong userId, string videoId)
    {
        const string QUERY = """
                             SELECT * FROM Links
                             WHERE
                                  user_id = $user_id AND
                                  video_id = $video_id
                             """;
        return await GetEntry(QUERY, parameters =>
        {
            parameters.AddULong("$user_id", userId);
            parameters.AddWithValue("$video_id", videoId);
        });
    }

    public async ValueTask<Entry[]> GetEntriesForVideoId(string videoId)
    {
        const string QUERY = """
                             SELECT * FROM Links
                             WHERE
                                  video_id = $video_id
                             """;
        return await GetEntries(QUERY, parameters =>
        {
            parameters.AddWithValue("$video_id", videoId);
        });
    }


    public async ValueTask<Entry[]> GetLinkEntriesLikeName(ulong userId,
        string name)
    {
        const string QUERY = """
                             SELECT * FROM Links
                             WHERE
                                 user_id = $user_id AND
                                 name like "%$name%"
                             """;
        return await GetEntries(QUERY, parameters =>
        {
            parameters.AddULong("$user_id", userId);
            parameters.AddWithValue("$name", name);
        });
    }

    public async ValueTask<Entry?> GetEntryForMessageId(ulong messageId)
    {
        const string QUERY = """
                             SELECT * FROM Links 
                             WHERE message_id = $message_id
                             LIMIT 1;
                             """;
        return await GetEntry(QUERY, parameters =>
        {
            parameters.AddULong("$message_id", messageId);
        });
    }
}