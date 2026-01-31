using Microsoft.Data.Sqlite;

namespace Common.DatabaseSchemas;

public class QueueSchema : DatabaseSchema<QueueSchema.Entry>
{
    public QueueSchema(SqliteConnection connection) : base(connection)
    {
    }
    
    public struct Entry
    {
        public ulong UserId;
        public ulong MessageId;
        public Guid LinkId;
        public string Name;
        public string Url;
        public bool Persistent;
        public QueueStatus Status;
    }

    public override string CreateTableCommand => """
                                                 CREATE TABLE IF NOT EXISTS Queue (
                                                     user_id TEXT,
                                                     link_id TEXT,
                                                     name TEXT,
                                                     url TEXT,
                                                     persistent INTEGER,
                                                     status INTEGER
                                                 )
                                                 PRIMARY KEY (user_id, LinkId)
                                                 """;

    public override string InsertCommand => """
                                            INSERT INTO Queue 
                                                 (user_id, link_id, name, url, persistent, status)
                                            values 
                                                ($user_id, $link_id, $name, $url, $persistent, $status)
                                            """;
    
    public override void AddInsertParameters(SqliteParameterCollection parameters, Entry entry)
    {
        parameters.AddULong("$user_id", entry.UserId);
        parameters.AddWithValue("$link_id", entry.LinkId);
        parameters.AddWithValue("$name", entry.Name);
        parameters.AddWithValue("$url", entry.Url);
        parameters.AddWithValue("$persistent", entry.Persistent);
        parameters.AddWithValue("$status", (int)entry.Status);
    }

    protected override Entry ParseEntry(SqliteDataReader reader) => new()
    {
        UserId = reader.GetULong(reader.GetOrdinal("user_id")),
        LinkId = reader.GetGuid(reader.GetOrdinal("link_id")),
        Url = reader.GetString(reader.GetOrdinal("url")),
        Name = reader.GetString(reader.GetOrdinal("name")),
        Persistent = reader.GetBoolean(reader.GetOrdinal("persistent")),
        Status = (QueueStatus)reader.GetInt32(reader.GetOrdinal("status"))
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
                parameters.AddULong("$user_id", userId);
                parameters.AddWithValue("$name", name);
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
            parameters.AddULong("$user_id", userId);
            parameters.AddWithValue("$name", name);
        });
    }
}