using DSharpPlus.Entities;

namespace YT_Downloader_DiscordBot;

public static class Constants
{
    public const string SaveLinkModalId = "saveLinkId";
    public const string ModalURLId = "url";
    public const string ModalNameId = "name";
    public const string ModalExpiresId = "expires";
    
    public const string ModalExpireOptionStandardId = "standard";
    public const string ModalExpireOptionNeverId = "never";

    public const int ExpirationDays = 14;

    public const int ErrorMessageTimeoutMinutes = 1;
    
    public static readonly DiscordEmoji EmojiToCheck = DiscordEmoji.FromUnicode("❌");
    public static readonly DiscordEmoji SafetyDeleteEmoji = DiscordEmoji.FromUnicode("🆑");
}