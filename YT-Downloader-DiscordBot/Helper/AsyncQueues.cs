using System.Collections.Concurrent;
using Common;

namespace YT_Downloader_DiscordBot.Helper;

public struct RemoveMessagePayload
{
    public ulong UserId { get; init; }
    public ulong MessageId { get; init; }
}

public static class AsyncQueues
{
    public static readonly ConcurrentQueue<DownloadResult> DownloadResults = new();
    public static readonly ConcurrentQueue<RemoveMessagePayload> RemoveMessageQueue = new();
}