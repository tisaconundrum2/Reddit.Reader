using Reddit.Reader.Builder;
using Reddit.Reader.Builder.Services;

var builder = Host.CreateApplicationBuilder(args);

// Allow `--seed-catalog` as a standalone flag (no value required).
if (args.Contains("--seed-catalog"))
    builder.Configuration["Pipeline:SeedCatalog"] = "true";

builder.Services.AddHttpClient();
builder.Services.ConfigureHttpClientDefaults(b => b
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(10))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        ConnectCallback = async (context, ct) =>
        {
            var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            socket.NoDelay = true;
            await socket.ConnectAsync(context.DnsEndPoint, ct);
            return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
        }
    }));

builder.Services.AddSingleton<IRedditService, RedditService>();
builder.Services.AddSingleton<ITextCleaningService, TextCleaningService>();
builder.Services.AddSingleton<ITtsService, TtsService>();
builder.Services.AddSingleton<IRssFeedService, RssFeedService>();
builder.Services.AddSingleton<ICatalogService, CatalogService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
