using Microsoft.Data.Sqlite;

namespace YT_Downloader_DiscordBot.DB.Schemas;

public class VersionSchema : DatabaseSchema<VersionSchema.Entry>
{
    public struct Entry
    {
        public int Version;
    }

    public VersionSchema(SqliteConnection connection) : base(connection) { }

    public override string CreateTableCommand => """
                                                 CREATE TABLE IF NOT EXISTS Version (version)
                                                 """;

    protected override string InsertCommand => """
                                               INSERT INTO Version (version) VALUES ($version)
                                               """;
    protected override void AddInsertParameters(SqliteParameterCollection parameters, Entry entry)
    {
        parameters.AddWithValue("$version", entry.Version);
    }

    protected override Entry ParseEntry(SqliteDataReader reader) => new()
    {
        Version = reader.GetInt32(reader.GetOrdinal("version"))
    };

    public async Task<int?> GetVersion()
    {
        const string QUERY = """
                             SELECT version FROM Version LIMIT 1;
                             """;
        Entry? entry = await GetEntry(QUERY, _ => { });
        return entry?.Version;
    }

    public async Task SetVersion(int version)
    {
        int? currentVersion = await GetVersion();
        if (currentVersion == null)
        {
            await AddEntry(new Entry { Version = 0 });
        }
        
        const string COMMAND = """
                               UPDATE Version SET Version = $version;
                               """;
        await Execute(COMMAND, parameters =>
        {
            parameters.AddWithValue("$version", version);
        });
    }
}