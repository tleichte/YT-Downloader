using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace YT_Downloader_DiscordBot.DB.VersionMigrations;

public abstract class VersionMigration
{
    public abstract string[] Commands { get; }

    public readonly int Version;

    public VersionMigration(int version)
    {
        Version = version;
    }
    
    public async Task <bool> DoMigration(SqliteConnection connection, ILogger logger)
    {
        try
        {
            foreach (string commandStr in Commands)
            {
                await using var command = new SqliteCommand(commandStr, connection);
                await command.ExecuteNonQueryAsync();
            }
            
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during migration {Version}!", Version);
            return false;
        }
    }
}