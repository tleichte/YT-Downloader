using Common;
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
}