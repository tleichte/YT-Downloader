using Microsoft.Data.Sqlite;

namespace YT_Downloader_DiscordBot.DB.Schemas;

public class LinkMetadataSchema : DatabaseSchema<LinkMetadataSchema.Entry>
{
    public struct Entry
    {
        public string VideoId;
        public string VideoTitle;
        public string VideoAuthor;
        public string OriginalUrl;
        public float Duration;
    }

    public LinkMetadataSchema(SqliteConnection connection) : base(connection)
    {
    }

    public override string CreateTableCommand => """
                                                 CREATE TABLE IF NOT EXISTS LinkMetadata (
                                                     video_id TEXT PRIMARY KEY,
                                                     video_title TEXT,
                                                     video_author TEXT,
                                                     original_url TEXT,
                                                     duration FLOAT
                                                 )
                                                 """;

    protected override string InsertCommand => """
                                               INSERT INTO LinkMetadata 
                                                   (video_id, video_title, video_author, original_url, duration)
                                               VALUES 
                                                   ($video_id, $video_title, $video_author, $original_url, $duration)
                                               """;

    protected override void AddInsertParameters(SqliteParameterCollection parameters, Entry entry)
    {
        parameters.AddWithValue("$video_id", entry.VideoId);
        parameters.AddWithValue("$video_title", entry.VideoTitle);
        parameters.AddWithValue("$video_author", entry.VideoAuthor);
        parameters.AddWithValue("$original_url", entry.OriginalUrl);
        parameters.AddWithValue("$duration", entry.Duration);
    }

    protected override Entry ParseEntry(SqliteDataReader reader) => new()
    {
        VideoId = reader.GetString(reader.GetOrdinal("video_id")),
        VideoTitle = reader.GetString(reader.GetOrdinal("video_title")),
        VideoAuthor = reader.GetString(reader.GetOrdinal("video_author")),
        OriginalUrl = reader.GetString(reader.GetOrdinal("original_url")),
        Duration = reader.GetFloat(reader.GetOrdinal("duration"))
    };

    public async Task<Entry?> GetMetadataForVideo(string videoId)
    {
        const string QUERY = """
                               SELECT * FROM LinkMetadata WHERE  video_id = $video_id LIMIT 1
                               """;
        return await GetEntry(QUERY, parameters =>
        {
            parameters.AddWithValue("$video_id", videoId);
        });
    }
    
    public async Task RemoveLinkEntry(string videoId)
    {
        const string COMMAND = """
                               DELETE FROM LinkMetadata WHERE video_id = $video_id
                               """;
        await Execute(COMMAND, parameters =>
        {
            parameters.AddWithValue("$video_id", videoId);
        });
    }
}