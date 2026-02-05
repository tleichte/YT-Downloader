using System.IO.Pipes;
using Newtonsoft.Json;

namespace Common;

public abstract class PipeServer<TReceive, TResponse>
{
    protected abstract string PipeName { get; }
    
    public void Start()
    {
        Task.Run(async () =>
        {
            var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut);
            
            while (true)
            {
                try
                {
                    await server.WaitForConnectionAsync();

                    StreamWriter writer = new(server);
                    StreamReader reader = new(server);

                    string? line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        continue;
                    }

                    var deserializedObject = JsonConvert.DeserializeObject<TReceive>(line);
                    if (deserializedObject == null)
                    {
                        continue;
                    }

                    TResponse response = await Handle(deserializedObject);

                    await writer.WriteLineAsync(JsonConvert.SerializeObject(response));
                    await writer.FlushAsync();
                }
                catch (Exception e)
                {
                    // LOG ERROR??
                }
                finally
                {
                    // This will only ever run on windows, disable warning
#pragma warning disable CA1416 
                    server.WaitForPipeDrain();
#pragma warning restore CA1416
                    if (server.IsConnected)
                    {
                        server.Disconnect();
                    }
                }
            }
        });
    }

    protected abstract ValueTask<TResponse> Handle(TReceive output);

}