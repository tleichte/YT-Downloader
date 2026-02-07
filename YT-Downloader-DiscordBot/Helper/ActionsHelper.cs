using Common;
using YT_Downloader_DiscordBot.Database.Schemas;

namespace YT_Downloader_DiscordBot.Helper;

public static class ActionsHelper
{
    public static async Task DeleteLink(LinksSchema.Entry entry)
    {
        Database.Database.Instance.Mutex.WaitOne();
        await Database.Database.Instance.RemoveLink(entry.UserId, entry.Name);
        Database.Database.Instance.Mutex.ReleaseMutex();

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