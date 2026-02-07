using System.Text;
using Common;
using DSharpPlus;
using DSharpPlus.Entities;
using YT_Downloader_DiscordBot.DB;
using YT_Downloader_DiscordBot.DB.Schemas;

namespace YT_Downloader_DiscordBot.Helper;

public static class BotHelpers
{

    public static async ValueTask<DiscordMessage?> GetMessage(DiscordUser user, ulong messageId)
    {
        try
        {
            DiscordDmChannel channel = await user.CreateDmChannelAsync();
            return await channel.GetMessageAsync(messageId);
        }
        catch (Exception)
        {
            return null;
        }
    }
    
    public static async ValueTask<Uri?> GetJumpLink(DiscordUser user, ulong messageId)
    {
        DiscordMessage? message = await GetMessage(user, messageId);
        return message?.JumpLink;
    }
    
    public static string GetQueueMessage(QueueSchema.Entry entry)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"## {entry.Name}");

        builder.AppendLine($"**Original:**");
        builder.AppendLine($"`{entry.Url}`");
        
        string statusString;
        switch (entry.Status)
        {
            case QueueStatus.Queued:
                statusString = "QUEUED";
                break;
            case QueueStatus.Downloading:
            case QueueStatus.Copying:
                statusString = $"IN PROGRESS - {entry.Progress}";
                break;
            default:
                statusString = "UNKNOWN";
                break;
        }
        
        builder.AppendLine($"### Status: _{statusString}_");
        
        return builder.ToString();
    }

    public static string GetLinkFailedMessage(DownloadResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"## {result.Name}");
        builder.AppendLine($"**Original:**");
        builder.AppendLine($"`{result.OriginalUrl}`");

        string failReason;
        switch (result.Result.FailValue)
        {
            case DownloadFailReason.VideoFetchError:
                failReason = "Video does not exist";
                break;
            default:
                failReason = "Download error";
                break;
        }
        builder.AppendLine($"### FAILED - _{failReason}_");
        
        return builder.ToString();
    }

    public static string GetLinkMessageString(LinksSchema.Entry entry, LinkMetadataSchema.Entry metadata)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"## {entry.Name}");
        
        builder.AppendLine($"**Original:**");

        List<string> titleStrings = [];
        if (!string.IsNullOrEmpty(metadata.VideoAuthor))
        {
            titleStrings.Add(metadata.VideoAuthor);
        }
        if (!string.IsNullOrEmpty(metadata.VideoTitle))
        {
            titleStrings.Add(metadata.VideoTitle);
        }
        if (titleStrings.Count > 0)
        {
            builder.AppendLine($"_{string.Join(" - ", titleStrings)}_");
        }

        builder.AppendLine($"`{metadata.OriginalUrl}`");

        builder.AppendLine($"```{Helpers.GetServerUrl(entry.UserId, entry.VideoId)}```");
        
        if (entry.DeleteAfter != DateTime.MaxValue)
        {
            int seconds = (int)(entry.DeleteAfter.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds;
            builder.AppendLine($"Expires: <t:{seconds}:f>");
        }
        
        return builder.ToString();
    }
    
    
    public static async Task TryUpdateQueueMessage(DiscordClient client)
    {
        QueueSchema.Entry? entry = await Database.Current.QueueSchema.GetFirstEntry();
        if (entry == null || entry.Value.Status == QueueStatus.Queued)
        {
            return;
        }
        
        DiscordUser user = await client.GetUserAsync(entry.Value.UserId);
        DiscordDmChannel dmChannelAsync = await user.CreateDmChannelAsync();
        DiscordMessage message = await dmChannelAsync.GetMessageAsync(entry.Value.MessageId);
        
        string messageString = GetQueueMessage(entry.Value);
        
        await message.ModifyAsync(messageString);
    }

    public static async Task CheckForDownloadResults(DiscordClient client)
    {
        while (!AsyncQueues.DownloadResults.IsEmpty)
        {
            AsyncQueues.DownloadResults.TryDequeue(out DownloadResult downloadResult);
            await UpdateResultMessage(client, downloadResult);
        }
    }

    private static async Task UpdateResultMessage(DiscordClient client, DownloadResult downloadResult)
    {
        DiscordUser user = await client.GetUserAsync(downloadResult.UserId);
        
        DiscordMessage? message = await GetMessage(user, downloadResult.MessageId);
        if (message == null)
        {
            return;
        }
        
        if (!downloadResult.Result.Success)
        {
            // Have different versions depending on type
            
            string failMessageString = GetLinkFailedMessage(downloadResult);
            await message.ModifyAsync(failMessageString);

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(Constants.ErrorMessageTimeoutMinutes));
                
                AsyncQueues.RemoveMessageQueue.Enqueue(new RemoveMessagePayload
                {
                    UserId = downloadResult.UserId,
                    MessageId = downloadResult.MessageId,
                });
            });

            return;
        }
        
        Database.Current.Mutex.WaitOne();
        LinksSchema.Entry? entry = await Database.Current.LinksSchema.GetEntryForName(downloadResult.UserId, downloadResult.Name);
        Database.Current.Mutex.ReleaseMutex();
        if (entry == null)
        {
            return;
        }
        
        await UpdateMessageForLink(message, entry.Value);

        await message.CreateReactionAsync(Constants.EmojiToCheck);
    }
    
    public static async Task UpdateMessageForLink(DiscordMessage message, LinksSchema.Entry entry)
    {
        Database.Current.Mutex.WaitOne();
        LinkMetadataSchema.Entry? metadata =
            await Database.Current.LinkMetadataSchema.GetMetadataForVideo(entry.VideoId);
        Database.Current.Mutex.ReleaseMutex();
        if (metadata == null)
        {
            return;
        }
        
        string messageString = GetLinkMessageString(entry, metadata.Value);
        await message.ModifyAsync(messageString);
    }
    
    public static async Task CheckForRemoveMessages(DiscordClient client)
    {
        while (!AsyncQueues.RemoveMessageQueue.IsEmpty)
        {
            AsyncQueues.RemoveMessageQueue.TryDequeue(out RemoveMessagePayload removedMessage);
            
            DiscordUser user = await client.GetUserAsync(removedMessage.UserId);
            DiscordDmChannel dmChannelAsync = await user.CreateDmChannelAsync();
            DiscordMessage message = await dmChannelAsync.GetMessageAsync(removedMessage.MessageId);

            await message.DeleteAsync();
        }
    }
}