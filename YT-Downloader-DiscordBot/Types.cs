// namespace YT_Downloader_DiscordBot;
//
// public class Config
// {
//     public static Config Current { get; set; }
//     
//     public string Token;
//     public ulong OwnerId;
//
//     public string DatabasePath;
//     public string QueueFolderPath;
// }
//
//
// public struct Result
// {
//     public bool Success;
//     public string FailReason;
//
//     public static implicit operator bool(Result result)
//     {
//         return result.Success;
//     }
//     
//     public static Result MakeSuccess()
//     {
//         return new Result
//         {
//             Success = true,
//             FailReason = ""
//         };
//     }
//
//     public static Result MakeFail(string reason)
//     {
//         return new Result
//         {
//             Success = false,
//             FailReason = reason
//         };
//     }
// }
//
//
//
// public struct QueueEntry
// {
//     public string Name;
//     public string Url;
//     public bool Persistent;
// }
//
// public struct LinkEntry
// {
//     public string Name;
//     public string Url;
//     public string DiskUrl;
//     public bool Persistent;
//     public DateTime DeleteAfter;
// }