using System.Collections;
using Common;
using FFMpegCore;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace YT_Downloader_Service;

public class Downloader
{
    private string m_outputFolderPath;
    private string m_cookiesPath;

    private const string FormatString = "best[height<=1080]";


    public async Task RunFirstQueue(CancellationToken ct)
    {
        Result result = Database.Instance.GetFirstQueueEntry(out QueueEntry entry);
        if (result)
        {
            await RunDownload(entry.Url, ct);
        }
    }
    
    
    public async ValueTask<bool> RunDownload(string url, CancellationToken cancellationToken)
    {

        var ytDL = new YoutubeDL();
        ytDL.OutputFolder = m_outputFolderPath;
        
        var dataResult = await ytDL.RunVideoDataFetch(url, ct:cancellationToken);
        var maxHeight = int.Min(dataResult.Data.Formats.Max(f => f.Height) ?? 0, 1080);

        var options = new OptionSet()
        {
            NoContinue = true,
            RestrictFilenames = true,
            RecodeVideo = VideoRecodeFormat.Mp4,
            Format = FormatString,
            Cookies = m_cookiesPath,
        };
        
        var res = await ytDL.RunVideoDownload(url, overrideOptions: options, ct: cancellationToken);
        if (res.Success)
        {
            string path = res.Data;

            var mediaInfo = await FFProbe.AnalyseAsync(path, cancellationToken: cancellationToken);
            int height = mediaInfo.PrimaryVideoStream?.Height ?? 0;
            if (height == maxHeight)
            {
                // Continue onward, success
            }
        }

        return false;
    }
    
    
}