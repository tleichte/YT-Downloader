using Microsoft.Data.Sqlite;

namespace Common.DatabaseSchemas;

// public enum DBColumnType
// {
//     INTEGER,
//     FLOAT,
//     TEXT
// }
// [AttributeUsage(AttributeTargets.Field)]
// public class DBColumnAttribute : Attribute
// {
//     public string ColumnName { get; private set; }
//     public DBColumnAttribute(string columnName, DBColumnType type)
//     {
//         ColumnName = columnName;
//     }
// }

public interface IDatabaseSchema<T> where T : struct
{
    static abstract string GetCreateTableCommand();
    static abstract Task ExecuteInsert(SqliteConnection connection, T entry);
    static abstract bool ParseEntry(SqliteDataReader reader, out T entry);
    
    static abstract Task<T?> ParseSingleEntry(SqliteDataReader reader);
    
    static abstract Task<T[]> ParseEntries(SqliteDataReader reader);
}

public abstract class DatabaseSchema<T> where T : struct
{
    private SqliteConnection m_connection;
    protected DatabaseSchema(SqliteConnection connection)
    {
        m_connection = connection;
    }

    public abstract string CreateTableCommand { get; }
    protected abstract string InsertCommand { get; }
    protected abstract void AddInsertParameters(SqliteParameterCollection parameters, T entry);

    public async Task AddEntry(T entry)
    {
        await using var command = new SqliteCommand(InsertCommand, m_connection);
        AddInsertParameters(command.Parameters, entry);
        await command.ExecuteNonQueryAsync();
    }

    protected abstract T ParseEntry(SqliteDataReader reader);
    private bool TryParseEntry(SqliteDataReader reader, out T entry)
    {
        try
        {
            entry = ParseEntry(reader);
            return true;
        }
        catch (Exception)
        {
            // ignored
        }

        entry = default;
        return false;
    }

    protected async Task Execute(string commandText, Action<SqliteParameterCollection> addParams)
    {
        await using var command = new SqliteCommand(commandText, m_connection);
        addParams(command.Parameters);
        await command.ExecuteNonQueryAsync();
    }
    
    protected async Task<T[]> GetEntries(string commandText, Action<SqliteParameterCollection> addParams)
    {
        await using var command =  new SqliteCommand(commandText, m_connection);
        addParams(command.Parameters);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        return await ParseEntries(reader);
    }

    protected async Task<T?> GetEntry(string queryText, Action<SqliteParameterCollection> addParams)
    {
        await using var command = new SqliteCommand(queryText, m_connection);
        addParams(command.Parameters);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        return await ParseSingleEntry(reader);
    }
    
    private async Task<T?> ParseSingleEntry(SqliteDataReader reader)
    {
        if (!await reader.ReadAsync())
        {
            return null;
        }

        if (!TryParseEntry(reader, out T entry))
        {
            return null;
        }

        return entry;
    }

    private async Task<T[]> ParseEntries(SqliteDataReader reader)
    {
        List<T> entries = new();
        while (await reader.ReadAsync())
        {
            if (TryParseEntry(reader, out T entry))
            {
                entries.Add(entry);
            }
        }

        return entries.ToArray();
    }
}