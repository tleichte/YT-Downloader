using System.IO.Pipes;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Common;

public abstract class PipeClient<TSend, TResponse>
{
    protected abstract string PipeName { get; }
    
    protected abstract PipeOptions PipeOptions { get; }
    
    public async ValueTask<TResponse> Send(TSend sendObject)
    {
        var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions);

        if (!client.IsConnected)
        {
            await client.ConnectAsync();
        }
        
        var reader = new StreamReader(client);
        var writer = new StreamWriter(client);
        
        await writer.WriteLineAsync(JsonConvert.SerializeObject(sendObject));
        await writer.FlushAsync();
        
        // Only runnable on windows, disabling warning
#pragma warning disable CA1416
        client.WaitForPipeDrain();
#pragma warning restore CA1416
        
        string? line = await reader.ReadLineAsync();
        if (line == null)
        {
            throw new EndOfStreamException();
        }

        TResponse? response = JsonConvert.DeserializeObject<TResponse>(line);
        if (response == null)
        {
            throw new SerializationException();
        }
            
        return response;
    }
}