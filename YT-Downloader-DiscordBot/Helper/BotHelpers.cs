using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using YT_Downloader_DiscordBot.DB;
using YT_Downloader_DiscordBot.DB.Schemas;

namespace YT_Downloader_DiscordBot.Helper;

public static class BotHelpers
{

    public static async Task AddToQueue(string url, string? name, bool persistent, DiscordUser user)
    {
        string preQueueMessage = $"Adding `{url}` to queue...";
        DiscordMessage message = await user.SendMessageAsync(preQueueMessage);
        
        // Check if valid URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            var response = $"`{url}` is not a valid URL!";
            await message.ModifyAsync(response);
            RunDeleteAfter(user.Id, message.Id, Constants.ErrorMessageTimeoutMinutes);
            return;
        }
        
        ulong userId = user.Id;

        bool nameless = string.IsNullOrWhiteSpace(name);
        
        if (!nameless)
        {
            // Check if name already exists in queue
            Database.Current.Mutex.WaitOne();
            QueueSchema.Entry? nameQueueEntry = await Database.Current.QueueSchema.GetEntryForName(userId, name);
            Database.Current.Mutex.ReleaseMutex();
            if (nameQueueEntry != null)
            {
                DiscordDmChannel channel = await user.CreateDmChannelAsync();
                DiscordMessage existingMessage = await channel.GetMessageAsync(nameQueueEntry.Value.MessageId);
                var response = $"You already have a video queued with the name {name}!\n{existingMessage.JumpLink}";
                await message.ModifyAsync(response);
                RunDeleteAfter(user.Id, message.Id, Constants.ErrorMessageTimeoutMinutes);
                return;
            }
            
            // Check if name already exists in links
            Database.Current.Mutex.WaitOne();
            LinksSchema.Entry? nameLinksEntry = await Database.Current.LinksSchema.GetEntryForName(userId, name);
            Database.Current.Mutex.ReleaseMutex();
            if (nameLinksEntry != null)
            {
                DiscordDmChannel channel = await user.CreateDmChannelAsync();
                DiscordMessage existingMessage = await channel.GetMessageAsync(nameLinksEntry.Value.MessageId);
                var response = $"You already have a video with the name {name}!\n{existingMessage.JumpLink}";
                await message.ModifyAsync(response);
                RunDeleteAfter(user.Id, message.Id, Constants.ErrorMessageTimeoutMinutes);
                return;
            }
        }
        else
        {
            name = new Random().NextInt64().ToString();
        }

        Database.Current.Mutex.WaitOne();

        await Database.Current.QueueSchema.AddEntry(new QueueSchema.Entry
        {
            UserId = userId,
            Name = name ?? "",
            Nameless = nameless,
            Url = url,
            MessageId = message.Id,
            Persistent = persistent,
            Status = QueueStatus.Queued,
            Progress = ""
        });
        
        Database.Current.Mutex.ReleaseMutex();
        
        var builder = new StringBuilder();

        if (!nameless)
        {
            builder.AppendLine($"## {name}");
        }
        builder.AppendLine("**Original:**");
        builder.AppendLine($"`{url}`");
        builder.AppendLine();
        builder.AppendLine("**Status:**");
        builder.AppendLine("_QUEUED_");
        
        await message.ModifyAsync(builder.ToString());
    }

    public static async Task AddToMP3Queue(string url, DiscordUser user)
    {
        string preQueueMessage = $"Adding `{url}` to queue...";
        DiscordMessage message = await user.SendMessageAsync(preQueueMessage);
        
        // Check if valid URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            var response = $"`{url}` is not a valid URL!";
            await message.ModifyAsync(response);
            RunDeleteAfter(user.Id, message.Id, Constants.ErrorMessageTimeoutMinutes);
            return;
        }
        
        ulong userId = user.Id;
        
        // Check if name already exists in queue
        Database.Current.Mutex.WaitOne();
        MP3QueueSchema.Entry? urlQueueEntry = await Database.Current.MP3QueueSchema.GetEntryForUrl(userId, url);
        Database.Current.Mutex.ReleaseMutex();
        if (urlQueueEntry != null)
        {
            DiscordDmChannel channel = await user.CreateDmChannelAsync();
            DiscordMessage existingMessage = await channel.GetMessageAsync(urlQueueEntry.Value.MessageId);
            var response = $"You already have an mp3 download queued with that url!\n{existingMessage.JumpLink}";
            await message.ModifyAsync(response);
            RunDeleteAfter(user.Id, message.Id, Constants.ErrorMessageTimeoutMinutes);
            return;
        }

        Database.Current.Mutex.WaitOne();

        await Database.Current.MP3QueueSchema.AddEntry(new MP3QueueSchema.Entry
        {
            UserId = userId,
            Url = url,
            MessageId = message.Id,
            Status = QueueStatus.Queued,
            Progress = ""
        });
        
        Database.Current.Mutex.ReleaseMutex();
        
        var builder = new StringBuilder();
        
        builder.AppendLine($"## MP3 Download");
        builder.AppendLine("**Original:**");
        builder.AppendLine($"`{url}`");
        builder.AppendLine();
        builder.AppendLine("**Status:**");
        builder.AppendLine("_QUEUED_");
        
        await message.ModifyAsync(builder.ToString());
    }
    
    
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
    
    public static async ValueTask<Uri?> GetJumpLink(DiscordClient client, ulong userId, ulong messageId)
    {
        DiscordUser user = await client.GetUserAsync(userId);
        return await GetJumpLink(user, messageId);
    }
    public static async ValueTask<Uri?> GetJumpLink(DiscordUser user, ulong messageId)
    {
        DiscordMessage? message = await GetMessage(user, messageId);
        return message?.JumpLink;
    }
    
    public static string GetQueueMessage(QueueSchema.Entry entry)
    {
        var builder = new StringBuilder();

        if (!entry.Nameless)
        {
            builder.AppendLine($"## {entry.Name}");
        }

        builder.AppendLine($"**Original:**");
        builder.AppendLine($"`{entry.Url}`");
        builder.AppendLine();
        
        builder.AppendLine($"**Status:**");
        string statusString;
        switch (entry.Status)
        {
            case QueueStatus.Queued:
                statusString = "QUEUED...";
                break;
            case QueueStatus.Fetching:
                statusString = "FETCHING...";
                break;
            case QueueStatus.Downloading:
                statusString = $"DOWNLOADING - {entry.Progress}";
                break;
            case QueueStatus.Processing:
                statusString = "PROCESSING... (This could take a while)";
                break;
            default:
                statusString = "UNKNOWN";
                break;
        }
        
        builder.AppendLine($"_{statusString}_");
        
        return builder.ToString();
    }
    
    public static string GetMP3QueueMessage(MP3QueueSchema.Entry entry)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"## MP3 Download Result");

        builder.AppendLine($"**Original:**");
        builder.AppendLine($"`{entry.Url}`");
        builder.AppendLine();
        
        builder.AppendLine($"**Status:**");
        string statusString;
        switch (entry.Status)
        {
            case QueueStatus.Queued:
                statusString = "QUEUED...";
                break;
            case QueueStatus.Fetching:
                statusString = "FETCHING...";
                break;
            case QueueStatus.Downloading:
                statusString = $"DOWNLOADING - {entry.Progress}";
                break;
            case QueueStatus.Processing:
                statusString = "PROCESSING... (This could take a while)";
                break;
            default:
                statusString = "UNKNOWN";
                break;
        }
        
        builder.AppendLine($"_{statusString}_");
        
        return builder.ToString();
    }

    private static async ValueTask<string> GetLinkFailedMessage(DiscordClient client, DownloadResult result)
    {
        var builder = new StringBuilder();
        
        builder.AppendLine($"## {result.Name}");
        builder.AppendLine($"**Original:**");
        builder.AppendLine($"`{result.OriginalUrl}`");

        builder.AppendLine($"**FAILED**");
        switch (result.Result.FailValue)
        {
            case DownloadFailReason.VideoFetchError:
                builder.AppendLine("That URL does not contain a valid video, or the bot is on cooldown. Please try again in a bit.");
                break;
            case DownloadFailReason.VideoBotError:
            {
                builder.AppendLine("Video downloads are throttled. Please try again later.");
                break;
            }
            case DownloadFailReason.VideoTooLong:
            {
                builder.AppendLine($@"Video is too long! Max video length: {TimeSpan.FromMinutes(Constants.MaxVideoDurationInMinutes):h\:mm\:ss}");
                break;
            }
            case DownloadFailReason.VideoAlreadyDownloaded:
            {
                if (result.VideoId != null)
                {
                    Database.Current.Mutex.WaitOne();
                    LinksSchema.Entry? entryForVideoId = await Database.Current.LinksSchema.GetEntryForVideoId(result.UserId, result.VideoId);
                    Database.Current.Mutex.ReleaseMutex();
                    if (entryForVideoId != null)
                    {
                        Uri? jumpLink = await GetJumpLink(client, result.UserId, entryForVideoId.Value.MessageId);
                        builder.AppendLine("You have already downloaded that URL:");
                        builder.AppendLine($"{jumpLink}");
                        break;
                    }
                }
                builder.AppendLine("Unknown video download error.");
                foreach (string error in result.ErrorMessages)
                {
                    builder.AppendLine($"`{error}`");
                }
                break;
            }
            case DownloadFailReason.VideoDownloadError:
            default:
            {
                builder.AppendLine("Unknown video download error.");
                foreach (string error in result.ErrorMessages)
                {
                    builder.AppendLine($"`{error}`");
                }
                break;
            }
        }
        
        return builder.ToString();
    }
    
    private static async ValueTask<string> GetMP3LinkFailedMessage(DiscordClient client, MP3DownloadResult result)
    {
        var builder = new StringBuilder();
        
        builder.AppendLine($"## MP3 Download Result");
        builder.AppendLine($"**Original:**");
        builder.AppendLine($"`{result.OriginalUrl}`");

        builder.AppendLine($"**FAILED**");
        switch (result.Result.FailValue)
        {
            case DownloadFailReason.VideoFetchError:
                builder.AppendLine("That URL does not contain a valid video, or the bot is on cooldown. Please try again in a bit.");
                break;
            case DownloadFailReason.VideoBotError:
            {
                builder.AppendLine("Video downloads are throttled. Please try again later.");
                break;
            }
            case DownloadFailReason.VideoTooLong:
            {
                builder.AppendLine($@"Video is too long! Max video length: {TimeSpan.FromMinutes(Constants.MaxVideoDurationInMinutes):h\:mm\:ss}");
                break;
            }
            case DownloadFailReason.VideoAlreadyDownloaded:
            {
                if (result.VideoId != null)
                {
                    Database.Current.Mutex.WaitOne();
                    MP3LinksSchema.Entry? entryForVideoId = await Database.Current.MP3LinksSchema.GetEntryForVideoId(result.UserId, result.VideoId);
                    Database.Current.Mutex.ReleaseMutex();
                    if (entryForVideoId != null)
                    {
                        Uri? jumpLink = await GetJumpLink(client, result.UserId, entryForVideoId.Value.MessageId);
                        builder.AppendLine("You have already downloaded that URL:");
                        builder.AppendLine($"{jumpLink}");
                        break;
                    }
                }
                builder.AppendLine("Unknown video download error.");
                foreach (string error in result.ErrorMessages)
                {
                    builder.AppendLine($"`{error}`");
                }
                break;
            }
            case DownloadFailReason.VideoDownloadError:
            default:
            {
                builder.AppendLine("Unknown video download error.");
                foreach (string error in result.ErrorMessages)
                {
                    builder.AppendLine($"`{error}`");
                }
                break;
            }
        }
        
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
        builder.AppendLine();
        builder.AppendLine("**New URL:**");
        builder.AppendLine($"{Helpers.GetServerUrl(entry.UserId, entry.VideoId)}");
        
        if (entry.DeleteAfter != DateTime.MaxValue)
        {
            int seconds = (int)(entry.DeleteAfter.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds;
            builder.AppendLine($"Expires: <t:{seconds}:f>");
        }
        
        return builder.ToString();
    }
    
    public static string GetMP3LinkMessageString(MP3LinksSchema.Entry entry)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"## MP3 Download Result");
        
        builder.AppendLine($"**Original:**");

        builder.AppendLine($"`{entry.Url}`");
        builder.AppendLine();
        builder.AppendLine("**MP3 URL:**");
        builder.AppendLine($"{Helpers.GetMP3ServerUrl(entry.UserId, entry.VideoId)}");
        
        if (entry.DeleteAfter != DateTime.MaxValue)
        {
            int seconds = (int)(entry.DeleteAfter.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds;
            builder.AppendLine($"Expires: <t:{seconds}:f>");
        }
        
        return builder.ToString();
    }


    private static QueueSchema.Entry _prevEntryValue;
    public static async Task TryUpdateQueueMessage(DiscordClient client)
    {
        Database.Current.Mutex.WaitOne();
        QueueSchema.Entry? entry = await Database.Current.QueueSchema.GetFirstEntry();
        Database.Current.Mutex.ReleaseMutex();
        
        if (entry == null || entry.Value.Status == QueueStatus.Queued)
        {
            return;
        }

        if (entry.Value.MessageId ==  _prevEntryValue.MessageId &&
            entry.Value.Status == _prevEntryValue.Status &&
            entry.Value.Progress == _prevEntryValue.Progress)
        {
            // Don't update message if it matches the previous cached entry value
            return;
        }

        _prevEntryValue = entry.Value;
        
            
            
        DiscordUser user = await client.GetUserAsync(entry.Value.UserId);
        DiscordDmChannel dmChannelAsync = await user.CreateDmChannelAsync();
        DiscordMessage message = await dmChannelAsync.GetMessageAsync(entry.Value.MessageId);

        if (entry.Value.Status == QueueStatus.Processing)
        {
            if (message.Content.Contains("PROCESSING"))
            {
                // Don't re-update a message that has processing already
                return;
            }
        }
        
        string messageString = GetQueueMessage(entry.Value);
        
        await message.ModifyAsync(messageString);
    }
    
    private static MP3QueueSchema.Entry _prevMP3EntryValue;
    public static async Task TryUpdateMP3QueueMessage(DiscordClient client)
    {
        Database.Current.Mutex.WaitOne();
        MP3QueueSchema.Entry? entry = await Database.Current.MP3QueueSchema.GetFirstEntry();
        Database.Current.Mutex.ReleaseMutex();
        
        if (entry == null || entry.Value.Status == QueueStatus.Queued)
        {
            return;
        }

        if (entry.Value.MessageId ==  _prevEntryValue.MessageId &&
            entry.Value.Status == _prevEntryValue.Status &&
            entry.Value.Progress == _prevEntryValue.Progress)
        {
            // Don't update message if it matches the previous cached entry value
            return;
        }

        _prevMP3EntryValue = entry.Value;
            
        DiscordUser user = await client.GetUserAsync(entry.Value.UserId);
        DiscordDmChannel dmChannelAsync = await user.CreateDmChannelAsync();
        DiscordMessage message = await dmChannelAsync.GetMessageAsync(entry.Value.MessageId);
        
        string messageString = GetMP3QueueMessage(entry.Value);
        
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

    public static async Task CheckForMP3DownloadResults(DiscordClient client)
    {
        while (!AsyncQueues.MP3DownloadResults.IsEmpty)
        {
            AsyncQueues.MP3DownloadResults.TryDequeue(out MP3DownloadResult downloadResult);
            await UpdateMP3ResultMessage(client, downloadResult);
        }
    }

    public static void RunDeleteAfter(ulong userId, ulong messageId, int minutes)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(minutes));
                
            AsyncQueues.RemoveMessageQueue.Enqueue(new RemoveMessagePayload
            {
                UserId = userId,
                MessageId = messageId,
            });
        });
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
            
            string failMessageString = await GetLinkFailedMessage(client, downloadResult);
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
    
    private static async Task UpdateMP3ResultMessage(DiscordClient client, MP3DownloadResult downloadResult)
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
            
            string failMessageString = await GetMP3LinkFailedMessage(client, downloadResult);
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
        MP3LinksSchema.Entry? entry = await Database.Current.MP3LinksSchema.GetEntryForVideoId(downloadResult.UserId, downloadResult.VideoId);
        Database.Current.Mutex.ReleaseMutex();
        if (entry == null)
        {
            return;
        }
        
        await UpdateMessageForMP3Link(message, entry.Value);

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
    
    public static async Task UpdateMessageForMP3Link(DiscordMessage message, MP3LinksSchema.Entry entry)
    {
        string messageString = GetMP3LinkMessageString(entry);
        await message.ModifyAsync(messageString);
    }
    
    public static async Task CheckForRemoveMessages(DiscordClient client)
    {
        while (!AsyncQueues.RemoveMessageQueue.IsEmpty)
        {
            try
            {
                AsyncQueues.RemoveMessageQueue.TryDequeue(out RemoveMessagePayload removedMessage);
            
                DiscordUser user = await client.GetUserAsync(removedMessage.UserId);
                DiscordDmChannel dmChannelAsync = await user.CreateDmChannelAsync();
                DiscordMessage message = await dmChannelAsync.GetMessageAsync(removedMessage.MessageId);

                await message.DeleteAsync();
            }
            catch (Exception)
            {
                //
            }
        }
    }

    public struct FindResultPage
    {
        public bool Success;
        public string Content;
        public DiscordButtonComponent[] Buttons;

        public static FindResultPage CreateFailure()
        {
            return new FindResultPage { Success = false };
        }
    }
    
    public static async Task<FindResultPage> CreateFindResultPage(DiscordUser user, string name, int page)
    {
        Database.Current.Mutex.WaitOne();
        LinksSchema.Entry[] entries = await Database.Current.LinksSchema.GetEntriesLikeName(user.Id, name);
        Database.Current.Mutex.ReleaseMutex();
        
        if (entries.Length == 0)
        {
            return FindResultPage.CreateFailure();
        }
        
        StringBuilder outString = new();
        outString.AppendLine($"Found the following links matching name _{name}_:");

        int startIndex = page * Constants.FindPageSize;
        int endIndex = startIndex + Constants.FindPageSize;
        for (int i = startIndex; i < endIndex; i++)
        {
            if (i >= entries.Length)
            {
                break;
            }
            
            LinksSchema.Entry entry = entries[i];
            Uri? jumpLink = await GetJumpLink(user, entry.MessageId);
            outString.AppendLine($"- {entry.Name}");
            outString.AppendLine($"  - {jumpLink}");
        }
        
        int totalPages = (int)Math.Ceiling(entries.Length / (double)Constants.FindPageSize);
        outString.AppendLine($"Page {page+1}/{totalPages}");
        
        var prevButton = new DiscordButtonComponent(
            DiscordButtonStyle.Primary,
            Constants.FindResultsPrevButtonId,
            "<-",
            disabled: page == 0);
        
        var nextButton = new DiscordButtonComponent(
            DiscordButtonStyle.Primary,
            Constants.FindResultsNextButtonId,
            "->",
            disabled: page+1 >= totalPages);

        return new FindResultPage
        {
            Success = true,
            Content = outString.ToString(),
            Buttons = [prevButton, nextButton]
        };
    }

    public static async Task AttemptGoPreviousFindResultPage(ComponentInteractionCreatedEventArgs eventArgs)
    {
        string messageContent = eventArgs.Message.Content;
        if (ExtractResultsPageNumber(messageContent, out int pageNumber))
        {
            await AttemptGoFindResultPageInner(eventArgs, pageNumber - 1);
        }
    }
    
    public static async Task AttemptGoNextFindResultPage(ComponentInteractionCreatedEventArgs eventArgs)
    {
        string messageContent = eventArgs.Message.Content;
        if (ExtractResultsPageNumber(messageContent, out int pageNumber))
        {
            await AttemptGoFindResultPageInner(eventArgs, pageNumber + 1);
        }
    }
    
    private static async Task AttemptGoFindResultPageInner(ComponentInteractionCreatedEventArgs eventArgs, int pageNumber)
    {
        await eventArgs.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
        
        DiscordWebhookBuilder deferredBuilder = new DiscordWebhookBuilder();
        deferredBuilder.WithContent("Grabbing results...");
        await eventArgs.Interaction.EditOriginalResponseAsync(deferredBuilder);
        
        string name = ExtractFindResultsName(eventArgs.Message.Content);

        FindResultPage page = await CreateFindResultPage(eventArgs.User, name, pageNumber);
        
        if (page.Success)
        {
            DiscordWebhookBuilder builder = new DiscordWebhookBuilder();
            builder.WithContent(page.Content);
            builder.AddActionRowComponent(page.Buttons);
            
            await eventArgs.Interaction.EditOriginalResponseAsync(builder);
        }
    }
    
    private static string ExtractFindResultsName(string messageContent)
    {
        string searchString = "matching name _";
        int nameIndex = messageContent.IndexOf(searchString, StringComparison.InvariantCultureIgnoreCase);
        string nameString = messageContent.Substring(nameIndex + searchString.Length).Split("_")[0];
        return nameString;
    }

    private static bool ExtractResultsPageNumber(string messageContent, out int pageNumber)
    {
        string searchString = "Page ";
        int pageIndex = messageContent.LastIndexOf(searchString, StringComparison.InvariantCultureIgnoreCase);
        string pageNumberString = messageContent.Substring(pageIndex + searchString.Length).Split("/")[0];

        bool parsed = int.TryParse(pageNumberString, out pageNumber);
        if (!parsed)
        {
            return false;
        }

        pageNumber -= 1;
        return true;
    }
}