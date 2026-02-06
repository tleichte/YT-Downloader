using Common;
using Common.DatabaseSchemas;

namespace YT_Downloader_Service;

public static class ServiceHelpers
{
    
    public static async Task DeleteExpiredLinks()
    {
        LinksSchema.Entry[] expiredLinks = await Database.Instance.LinksSchema.GetExpiredLinks();
        
        foreach(LinksSchema.Entry expiredLink in expiredLinks)
        {
            File.Delete(Helpers.GetVideoDiskFilePath(expiredLink.UserId, expiredLink.VideoId));
            
            await Database.Instance.RemoveLink(expiredLink.UserId, expiredLink.VideoId);
            
            // Remove discord message
            
        }
    }


    public static async Task<QueueSchema.Entry?> CheckForQueuedVideo()
    {
        QueueSchema.Entry? entry = await Database.Instance.QueueSchema.GetFirstEntry();
        if (entry == null)
        {
            return null;
        }

        if (entry.Value.Status != QueueStatus.Queued)
        {
            // TODO check for throttling
            return null;
        }

        return entry;
    }
}