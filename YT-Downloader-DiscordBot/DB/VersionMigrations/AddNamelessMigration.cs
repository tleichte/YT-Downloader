namespace YT_Downloader_DiscordBot.DB.VersionMigrations;

public class AddNamelessMigration(int version) : VersionMigration(version)
{
    public override string[] Commands =>
    [
        """
        ALTER TABLE Queue ADD COLUMN nameless INTEGER
        """
    ];
}