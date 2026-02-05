using YT_Downloader_Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();