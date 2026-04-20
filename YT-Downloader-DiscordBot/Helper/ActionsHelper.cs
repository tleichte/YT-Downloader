using YT_Downloader_DiscordBot.DB;
using YT_Downloader_DiscordBot.DB.Schemas;

namespace YT_Downloader_DiscordBot.Helper;

public static class ActionsHelper
{
    public static async Task DeleteLink(LinksSchema.Entry entry)
    {
        Database.Current.Mutex.WaitOne();
        await Database.Current.RemoveLink(entry.UserId, entry.Name);
        Database.Current.Mutex.ReleaseMutex();

        try
        {
            File.Delete(Helpers.GetVideoDiskFilePath(entry.UserId, entry.VideoId));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    public static async Task DeleteMP3Link(MP3LinksSchema.Entry entry)
    {
        Database.Current.Mutex.WaitOne();
        await Database.Current.MP3LinksSchema.RemoveLink(entry.UserId, entry.VideoId);
        Database.Current.Mutex.ReleaseMutex();

        try
        {
            File.Delete(Helpers.GetMP3DiskFilePath(entry.UserId, entry.VideoId));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}