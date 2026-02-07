using Common;
using Common.DatabaseSchemas;

namespace YT_Downloader_DiscordBot.Helper;

public static class ActionsHelper
{
    public static async Task DeleteLink(LinksSchema.Entry entry)
    {
        Database.Instance.Mutex.WaitOne();
        await Database.Instance.RemoveLink(entry.UserId, entry.Name);
        Database.Instance.Mutex.ReleaseMutex();

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