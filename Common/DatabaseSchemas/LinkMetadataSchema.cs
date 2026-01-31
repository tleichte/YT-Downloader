using Microsoft.Data.Sqlite;

namespace Common.DatabaseSchemas;

public class LinkMetadataSchema : DatabaseSchema<LinkMetadataSchema.Entry>
{
    public struct Entry
    {
        public string VideoId;
        public string VideoTitle;
        public float Duration;
    }

    public LinkMetadataSchema(SqliteConnection connection) : base(connection)
    {
    }

    public override string CreateTableCommand { get; }
    public override string InsertCommand { get; }
    public override void AddInsertParameters(SqliteParameterCollection parameters, Entry entry)
    {
        throw new NotImplementedException();
    }

    protected override Entry ParseEntry(SqliteDataReader reader)
    {
        throw new NotImplementedException();
    }

    public void RemoveLinkEntry(string videoId)
    {
        
    }
}