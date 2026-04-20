using Microsoft.Data.Sqlite;

namespace YT_Downloader_DiscordBot.DB.Schemas;

public class MP3LinksSchema : DatabaseSchema<MP3LinksSchema.Entry>
{
    public struct Entry
    {
        public ulong UserId;
        public string VideoId;
        public string Url;
        public ulong MessageId;
        public DateTime DeleteAfter;
    }

    public MP3LinksSchema(SqliteConnection connection) : base(connection)
    {
    }

    public override string CreateTableCommand => """
                                                 CREATE TABLE IF NOT EXISTS MP3Links (
                                                     user_id TEXT,
                                                     video_id TEXT,
                                                     url TEXT,
                                                     message_id TEXT,
                                                     delete_after INTEGER,
                                                     PRIMARY KEY (user_id, video_id)
                                                 )
                                                 """;
    protected override string InsertCommand => """
                                               INSERT INTO MP3Links
                                                   (user_id, video_id, url, message_id, delete_after)
                                               values 
                                                   ($user_id, $video_id, $url, $message_id, $delete_after)
                                               """;

    protected override void AddInsertParameters(SqliteParameterCollection parameters, Entry entry)
    {
        parameters.AddULong("$user_id", entry.UserId);
        parameters.AddWithValue("$video_id", entry.VideoId);
        parameters.AddWithValue("$url", entry.Url);
        parameters.AddULong("$message_id", entry.MessageId);
        parameters.AddWithValue("$delete_after", entry.DeleteAfter);
    }

    protected override Entry ParseEntry(SqliteDataReader reader) => new()
    {
        UserId = reader.GetULong(reader.GetOrdinal("user_id")),
        VideoId = reader.GetString(reader.GetOrdinal("video_id")),
        Url = reader.GetString(reader.GetOrdinal("url")),
        MessageId = reader.GetULong(reader.GetOrdinal("message_id")),
        DeleteAfter = reader.GetDateTime(reader.GetOrdinal("delete_after")),
    };
    
    public async Task RemoveLink(ulong userId, string videoid)
    {
        const string COMMAND = """
                               DELETE FROM MP3Links
                               WHERE
                                    user_id = $user_id AND
                                    video_id = $video_id
                               """;
        await Execute(COMMAND, parameters =>
        {
            parameters.AddULong("$user_id", userId);
            parameters.AddWithValue("$video_id", videoid);
        });
    }
    
    public async Task<Entry[]> GetExpiredLinks()
    {
        const string COMMAND = """
                                  SELECT * FROM MP3Links 
                                  WHERE
                                      delete_after < $currentDate
                               """;
        return await GetEntries(COMMAND, parameters =>
        {
            parameters.AddWithValue("$currentDate", DateTime.Now);
        });
    }
    
    public async ValueTask<Entry?> GetEntryForVideoId(ulong userId, string videoId)
    {
        const string QUERY = """
                             SELECT * FROM MP3Links
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
                             SELECT * FROM MP3Links
                             WHERE
                                  video_id = $video_id
                             """;
        return await GetEntries(QUERY, parameters =>
        {
            parameters.AddWithValue("$video_id", videoId);
        });
    }
    
    public async ValueTask<Entry?> GetEntryForMessageId(ulong messageId)
    {
        const string QUERY = """
                             SELECT * FROM MP3Links 
                             WHERE message_id = $message_id
                             LIMIT 1;
                             """;
        return await GetEntry(QUERY, parameters =>
        {
            parameters.AddULong("$message_id", messageId);
        });
    }
}