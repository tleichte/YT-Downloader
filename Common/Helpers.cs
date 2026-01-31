namespace Common;

public static class Helpers
{

    public static string GetVideoDiskPath(ulong userId, string videoId)
    {
        return @$"{Config.Current.DiskUrlPrefix}\videos\{userId}\{videoId}.mp4";
    }
    
    public static string GetServerUrl(ulong userId, string videoId)
    {
        return @$"{Config.Current.ServerUrlPrefix}/videos/{userId}/{videoId}.mp4";
    }
}