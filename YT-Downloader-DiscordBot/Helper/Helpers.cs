using YT_Downloader_DiscordBot.CF;

namespace YT_Downloader_DiscordBot.Helper;

public static class Helpers
{

    public static string GetVideoDiskFolderPathForUser(ulong userId)
    {
        return @$"{Config.Current.DiskUrlPrefix}\videos\{userId}\";
    }
    
    public static string GetMP3DiskFolderPathForUser(ulong userId)
    {
        return @$"{GetVideoDiskFolderPathForUser(userId)}mp3\";
    }
    
    public static string GetVideoDiskFilePath(ulong userId, string videoId)
    {
        return @$"{GetVideoDiskFolderPathForUser(userId)}{videoId}.mp4";
    }

    public static string GetMP3DiskFilePath(ulong userid, string videoId)
    {
        return @$"{GetMP3DiskFolderPathForUser(userid)}{videoId}.mp3";
    }
    
    public static string GetServerUrl(ulong userId, string videoId)
    {
        return @$"{Config.Current.ServerUrlPrefix}/videos/{userId}/{videoId}.mp4";
    }

    public static string GetMP3ServerUrl(ulong userId, string videoId)
    {
        return @$"{Config.Current.ServerUrlPrefix}/videos/{userId}/mp3/{videoId}.mp3";
    }
}