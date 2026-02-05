// namespace Common;

using Common.DatabaseSchemas;
using Newtonsoft.Json;

namespace Common;

public enum QueueStatus
{
    Queued,
    Downloading,
    Copying,
    Throttled,
}

public enum AddToQueueFailReason
{
    None,
    FailedDb,
    PipeFail
}

public static class Result
{
    public static Result<TFail> MakeSuccess<TFail>() where TFail : struct
    {
        return new Result<TFail>
        {
            Success = true,
            FailValue = default
        };
    }
    
    public static Result<TFail> MakeFail<TFail>(TFail failValue) where TFail : struct
    {
        return new Result<TFail>
        {
            Success = false,
            FailValue = failValue
        };
    }
}

public struct Result<TFail> where TFail : struct
{
    public bool Success;
    public TFail FailValue;
    
    public static implicit operator bool(Result<TFail> result)
    {
        return result.Success;
    }
}




public struct QueueUpdatePayload
{
    public ulong UserId;
    public ulong MessageId;
    public int Percent;
    
}