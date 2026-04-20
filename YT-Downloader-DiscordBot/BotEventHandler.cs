using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using YT_Downloader_DiscordBot.DB;
using YT_Downloader_DiscordBot.DB.Schemas;
using YT_Downloader_DiscordBot.Helper;

namespace YT_Downloader_DiscordBot;

public class BotEventHandler : IEventHandler<MessageReactionAddedEventArgs>, IEventHandler<ModalSubmittedEventArgs>
{
    public async Task HandleEventAsync(DiscordClient sender, MessageReactionAddedEventArgs eventArgs)
    {
        if (eventArgs.User.Id == sender.CurrentUser.Id)
        {
            return;
        }

        if (!eventArgs.Emoji.Equals(Constants.EmojiToCheck))
        {
            if (!eventArgs.Emoji.Equals(Constants.SafetyDeleteEmoji))
            {
                return;
            }

            Database.Current.Mutex.WaitOne();
            LinksSchema.Entry? linksCheck = await Database.Current.LinksSchema.GetEntryForMessageId(eventArgs.Message.Id);
            QueueSchema.Entry? queueCheck = await Database.Current.QueueSchema.GetEntryForMessageId(eventArgs.Message.Id);
            Database.Current.Mutex.ReleaseMutex();
            if (linksCheck != null || queueCheck != null)
            {
                return;
            }
            
            try
            {
                await eventArgs.Message.DeleteAsync();
            }
            catch (Exception)
            {
                // ignored
            }
            return;
        }

        Database.Current.Mutex.WaitOne();
        LinksSchema.Entry? existingLink = await Database.Current.LinksSchema.GetEntryForMessageId(eventArgs.Message.Id);
        Database.Current.Mutex.ReleaseMutex();
        
        if (existingLink != null)
        {
            await ActionsHelper.DeleteLink(existingLink.Value);

            await eventArgs.Message.DeleteAsync();
            
            await Task.Yield();
        }

        Database.Current.Mutex.WaitOne();
        MP3LinksSchema.Entry? existingMP3Link = await Database.Current.MP3LinksSchema.GetEntryForMessageId(eventArgs.Message.Id);
        Database.Current.Mutex.ReleaseMutex();
        
        if (existingMP3Link != null)
        {
            await ActionsHelper.DeleteMP3Link(existingMP3Link.Value);

            await eventArgs.Message.DeleteAsync();
            
            await Task.Yield();
        }
        
    }

    private static bool TryGetModalTextValue(string key, ModalSubmittedEventArgs eventArgs, out string? value)
    {
        if (eventArgs.Values.TryGetValue(key, out IModalSubmission? urlSubmission))
        {
            if (urlSubmission is TextInputModalSubmission textSubmission)
            {
                value = textSubmission.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryGetModalSelectValues(string key, ModalSubmittedEventArgs eventArgs, out IReadOnlyList<string>? value)
    {
        if (eventArgs.Values.TryGetValue(key, out IModalSubmission? urlSubmission))
        {
            if (urlSubmission is SelectMenuModalSubmission selectSubmission)
            {
                value = selectSubmission.Values;
                return true;
            }
        }
        value = null;
        return false;
    }
    
    public async Task HandleEventAsync(DiscordClient sender, ModalSubmittedEventArgs eventArgs)
    {
        await eventArgs.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
        
        if (!TryGetModalTextValue(Constants.ModalURLId, eventArgs, out string? urlModalValue))
        {
            return;
        }
        
        if (!TryGetModalTextValue(Constants.ModalNameId, eventArgs, out string? nameModalValue))
        {
            return;
        }

        if (!TryGetModalSelectValues(Constants.ModalExpiresId, eventArgs, out IReadOnlyList<string>? expiresModalValue))
        {
            return;
        }
        
        string url = urlModalValue ?? "";
        string name = nameModalValue ?? "";
        bool persistent = expiresModalValue?.Count == 1 && expiresModalValue[0] == Constants.ModalExpireOptionNeverId;

        await BotHelpers.AddToQueue(url, name, persistent, eventArgs.Interaction.User);
    }
}