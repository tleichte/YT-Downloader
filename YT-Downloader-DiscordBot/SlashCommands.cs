using Common;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace YT_Downloader_DiscordBot;

public class SlashCommands : ApplicationCommandModule
{

    private static async void Respond(InteractionContext ctx)
    {
        // if (ctx.GetOriginalResponseAsync())
    }
    
    [SlashCommand("save", "Requests a link to be saved to the ")]
    public async Task SaveCommand(InteractionContext ctx, string name, string link)
    {
        await ctx.DeferAsync(true);


        // Create file
        
        
        // Check if name already exists
        
        
        // Poll and wait for queue from db
        
        
        // Have a timeout of 15 seconds

        string response = "";
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(response));
    }

    [SlashCommand("status", "Check the status of a given name")]
    public async Task StatusCommand(InteractionContext ctx, string name)
    {
        // Check queries db
    }

    [SlashCommand("get", "Gets the link for a given name. Will return multiple if query is similar")]
    public async Task GetCommand(InteractionContext ctx, string name)
    {
        
    }


    [SlashCommand("bedtime", "[ADMIN] Shuts down the bot")]
    public async Task BedtimeCommand(InteractionContext ctx)
    {
        if (ctx.User.Id != Config.Current.OwnerId)
        {
            await ctx.CreateResponseAsync("You're not the boss of me!");
            return;
        }
        
        await ctx.CreateResponseAsync(":wave:");
        ctx.Client.Dispose();
        Environment.Exit(0);
    }
    
}