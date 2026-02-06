using System.Collections.Concurrent;
using Common;
using Common.DatabaseSchemas;

namespace YT_Downloader_DiscordBot.Helper;

public struct LinkRemovedMessage
{
    public ulong UserId { get; init; }
    public ulong MessageId { get; init; }
}

public static class AsyncQueues
{
    public static readonly ConcurrentQueue<DownloadResult> Results = new();
    public static readonly ConcurrentQueue<LinkRemovedMessage> ExpiredLinkMessages = new();
}