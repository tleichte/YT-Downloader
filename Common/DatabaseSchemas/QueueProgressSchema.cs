using Microsoft.Data.Sqlite;

namespace Common.DatabaseSchemas;

public class QueueProgressSchema : DatabaseSchema<QueueProgressSchema.Entry>
{
    public struct Entry
    {
        public string VideoId;
        public string Progress;
    }

    public QueueProgressSchema(SqliteConnection connection) : base(connection)
    {
    }

    public override string CreateTableCommand { get; }
    protected override string InsertCommand { get; }

    protected override void AddInsertParameters(SqliteParameterCollection parameters, Entry entry)
    {
        throw new NotImplementedException();
    }

    protected override Entry ParseEntry(SqliteDataReader reader)
    {
        throw new NotImplementedException();
    }
    
    public void SetProgressString(string userId, string videoId, string progressString)
    {
        
    }
}