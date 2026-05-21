using System.ComponentModel;
using System.Text;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.Metadata;
using DSharpPlus.Entities;
using YT_Downloader_DiscordBot.CF;
using YT_Downloader_DiscordBot.DB;
using YT_Downloader_DiscordBot.DB.Schemas;
using YT_Downloader_DiscordBot.Helper;

namespace YT_Downloader_DiscordBot;

public class Commands
{
    [Command("save")]
    [Description("Saves a link to the media server.")]
    [InteractionAllowedContexts(DiscordInteractionContextType.BotDM, DiscordInteractionContextType.Guild)]
    public async Task SaveCommand(SlashCommandContext ctx)
    {
        DiscordModalBuilder modalBuilder = new();
        modalBuilder.WithCustomId(Constants.SaveLinkModalId);
        modalBuilder.WithTitle("Save Video Link");
        modalBuilder.AddTextInput(
            new DiscordTextInputComponent(Constants.ModalURLId, placeholder: "https://www.youtube.com/watch?v=dQw4w9WgXcQ"), "URL",
            "The URL to download the video from. Must be supported by yt-dlp.");
        modalBuilder.AddTextInput(new DiscordTextInputComponent(Constants.ModalNameId, placeholder:"My Cool Video", required: false), "Name",
            "The optional user-defined name for IDing this video. If used, must be unique.");
        modalBuilder.AddSelectMenu(new DiscordSelectComponent(Constants.ModalExpiresId, "placeholder (you shouldn't see me)", new DiscordSelectComponentOption[]
        {
            new("Standard", Constants.ModalExpireOptionStandardId, isDefault:true),
            new("Never", Constants.ModalExpireOptionNeverId),
        }, required: true), "Expiration", "When this video should expire. By default videos expire in 30 days.");
        await ctx.RespondWithModalAsync(modalBuilder);
    }

    [Command("save_file")]
    [Description("UNIMPLEMENTED: Saves a file to the media server. Must be a video type. Limited by your Nitro upload maximum.")]
    [InteractionAllowedContexts(DiscordInteractionContextType.BotDM, DiscordInteractionContextType.Guild)]
    public async Task SaveFileCommand(SlashCommandContext ctx, DiscordAttachment attachment, string name, ExpirationPolicy expirationPolicy)
    {
        // TODO remove when command is confirmed working
        await ctx.RespondAsync("This command is still WIP! Check back later.", ephemeral: true);
        return;
        
        await ctx.DeferResponseAsync(true);

        if (!attachment.MediaType?.StartsWith("video") ?? false)
        {
            await ctx.EditResponseAsync("Provided attachment is not a video!");
            return;
        }

        if (attachment.Url == null)
        {
            await ctx.EditResponseAsync("Couldn't parse the attachment!");
            return;
        }
        
        await BotHelpers.AddToQueue(attachment.Url, name, expirationPolicy == ExpirationPolicy.Never, ctx.User);
        await ctx.DeleteResponseAsync();
    }

    [Command("save_mp3")]
    [Description("Converts a video link to mp3. Provides a temp download link when complete.")]
    [InteractionAllowedContexts(DiscordInteractionContextType.BotDM, DiscordInteractionContextType.Guild)]
    public async Task SaveMP3Command(SlashCommandContext ctx, string url)
    {
        await ctx.DeferResponseAsync(true);
        
        await BotHelpers.AddToMP3Queue(url, ctx.User);
        
        await ctx.DeleteResponseAsync();
    }
    
    [Command("find")]
    [Description("Attepts to find link messages that match the given name.")]
    [InteractionAllowedContexts(DiscordInteractionContextType.BotDM, DiscordInteractionContextType.Guild)]
    public async Task FindCommand(SlashCommandContext ctx, string name)
    {
        await ctx.DeferResponseAsync(true);
        
        Database.Current.Mutex.WaitOne();
        LinksSchema.Entry[] entries = await Database.Current.LinksSchema.GetEntriesLikeName(ctx.User.Id, name);
        Database.Current.Mutex.ReleaseMutex();

        if (entries.Length == 0)
        {
            await ctx.RespondAsync($"No links found matching _{name}_!");
            return;
        }
        
        StringBuilder outString = new();
        outString.AppendLine($"Found the following links matching name _{name}_:");
        foreach (LinksSchema.Entry entry in entries)
        {
            Uri? jumpLink = await BotHelpers.GetJumpLink(ctx.User, entry.MessageId);
            outString.AppendLine($"- {entry.Name}");
            outString.AppendLine($"  - {jumpLink}");
        }
        
        await ctx.EditResponseAsync(outString.ToString());
    }

    [Command("rename")]
    [Description("Renames a link's given name to a new one.")]
    [InteractionAllowedContexts(DiscordInteractionContextType.BotDM, DiscordInteractionContextType.Guild)]
    public async Task RenameCommand(SlashCommandContext ctx, string oldName, string newName)
    {
        await ctx.DeferResponseAsync(true);

        Database.Current.Mutex.WaitOne();
        LinksSchema.Entry? entry = await Database.Current.LinksSchema.GetEntryForName(ctx.User.Id, oldName);
        Database.Current.Mutex.ReleaseMutex();
        
        if (entry == null)
        {
            await ctx.EditResponseAsync($"No entry exists with the name {oldName}!");
            return;
        }

        Database.Current.Mutex.WaitOne();
        await Database.Current.LinksSchema.Rename(ctx.User.Id, oldName, newName);
        LinksSchema.Entry? newEntry = await Database.Current.LinksSchema.GetEntryForName(ctx.User.Id, newName);
        Database.Current.Mutex.ReleaseMutex();

        if (newEntry == null)
        {
            await ctx.EditResponseAsync($"Failed to rename to _{newName}_!");
            return;
        }

        DiscordMessage? message = await BotHelpers.GetMessage(ctx.User, newEntry.Value.MessageId);
        if (message == null)
        {
            return;
        }

        await BotHelpers.UpdateMessageForLink(message, newEntry.Value);
        await ctx.EditResponseAsync($"Successfully renamed to {newName}!\n{message.JumpLink}");
    }

    [Command("bedtime")]
    [Description("[ADMIN] Shuts down the bot")]
    [InteractionAllowedContexts(DiscordInteractionContextType.BotDM)]
    public async Task BedtimeCommand(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync(true);
        
        if (ctx.User.Id != Config.Current.BotOwnerId)
        {
            await ctx.EditResponseAsync("You're not the boss of me!");
            return;
        }
        
        await ctx.EditResponseAsync(":wave:");
        ctx.Client.Dispose();
        Environment.Exit(0);
    }
    
    [Command("reload_config")]
    [Description("[ADMIN] Reloads the config file for on-the-fly configuration updates")]
    [InteractionAllowedContexts(DiscordInteractionContextType.BotDM)]
    public async Task ReloadCommand(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync(true);
        
        if (ctx.User.Id != Config.Current.BotOwnerId)
        {
            await ctx.EditResponseAsync("You're not the boss of me!");
            return;
        }
        
        Config.Create();

        await ctx.EditResponseAsync("Reloaded config.");
    }

    [Command("extend")]
    [Description("Refreshes a link's expiration counter. Can optionally change to never expire.")]
    [InteractionAllowedContexts(DiscordInteractionContextType.BotDM)]
    public async Task ExtendCommand(SlashCommandContext ctx, string name, bool permanent = false)
    {
        await ctx.DeferResponseAsync(true);

        Database.Current.Mutex.WaitOne();
        LinksSchema.Entry? entry = await Database.Current.LinksSchema.GetEntryForName(ctx.User.Id, name);
        Database.Current.Mutex.ReleaseMutex();
        
        if (entry == null)
        {
            await ctx.EditResponseAsync($"No entry exists with the name {name}!");
            return;
        }

        if (entry.Value.DeleteAfter == DateTime.MaxValue)
        {
            await ctx.EditResponseAsync($"The link with name *{name}* is already permanent!");
        }

        DateTime newDeleteAfter =
            permanent ? DateTime.MaxValue : DateTime.Now + TimeSpan.FromDays(Constants.ExpirationDays); 
        
        Database.Current.Mutex.WaitOne();
        await Database.Current.LinksSchema.SetDeleteAfter(ctx.User.Id, name, newDeleteAfter);
        LinksSchema.Entry? newEntry = await Database.Current.LinksSchema.GetEntryForName(ctx.User.Id, name);
        Database.Current.Mutex.ReleaseMutex();
        
        if (newEntry == null)
        {
            await ctx.EditResponseAsync($"Failed to extend _{name}_!");
            return;
        }
        
        DiscordMessage? message = await BotHelpers.GetMessage(ctx.User, newEntry.Value.MessageId);
        if (message == null)
        {
            return;
        }

        await BotHelpers.UpdateMessageForLink(message, newEntry.Value);

        if (permanent)
        {
            await ctx.EditResponseAsync($"Successfully set _{name}_ to never expire!\n{message.JumpLink}");
        }
        else
        {
            var seconds = (int)(newEntry.Value.DeleteAfter.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds;
            await ctx.EditResponseAsync($"Successfully extended the expiration of _{name}_ to <t:{seconds}:f>!\n{message.JumpLink}");
        }
    }
}