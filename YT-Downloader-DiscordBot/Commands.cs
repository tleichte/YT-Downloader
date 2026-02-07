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
        modalBuilder.AddTextInput(new DiscordTextInputComponent(Constants.ModalNameId, placeholder:"My Cool Video"), "Name",
            "The user-defined name for IDing this video. Must be unique.");
        modalBuilder.AddSelectMenu(new DiscordSelectComponent(Constants.ModalExpiresId, "placeholder (you shouldn't see me)", new DiscordSelectComponentOption[]
        {
            new("Standard", Constants.ModalExpireOptionStandardId, isDefault:true),
            new("Never", Constants.ModalExpireOptionNeverId),
        }, required: true), "Expiration", "When this video should expire. By default videos expire in 2 weeks.");
        await ctx.RespondWithModalAsync(modalBuilder);
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
        if (ctx.User.Id != Config.Current.BotOwnerId)
        {
            await ctx.RespondAsync("You're not the boss of me!");
            return;
        }
        
        await ctx.RespondAsync(":wave:");
        ctx.Client.Dispose();
        Environment.Exit(0);
    }
    
}