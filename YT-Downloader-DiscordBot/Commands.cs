using System.ComponentModel;
using Common;
using Common.DatabaseSchemas;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.Metadata;
using DSharpPlus.Entities;

namespace YT_Downloader_DiscordBot;

public class Commands
{
    [Command("save")]
    [Description("Saves a link to the media server.")]
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
        modalBuilder.AddSelectMenu(new DiscordSelectComponent(Constants.ModalExpiresId, "Standard", new DiscordSelectComponentOption[]
        {
            new("Standard", Constants.ModalExpireOptionStandardId),
            new("Never", Constants.ModalExpireOptionNeverId),
        }, required: true), "Expiration", "When this video should expire. By default videos expire in 2 weeks.");
        await ctx.RespondWithModalAsync(modalBuilder);
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