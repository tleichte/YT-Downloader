namespace YT_Downloader_DiscordBot;

public class VideoDownloadOutput : IProgress<string>
{
    private string m_outputFilePath;
    
    public VideoDownloadOutput(string outputFilePath)
    {
        m_outputFilePath = outputFilePath;
    }
    
    public void Report(string value)
    {
        File.AppendAllLines(m_outputFilePath, [value]);
    }

    public void RemoveOutput()
    {
        File.Delete(m_outputFilePath);
    }
}