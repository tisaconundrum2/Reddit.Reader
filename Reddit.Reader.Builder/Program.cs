using Reddit.Reader.Builder;
using Reddit.Reader.Builder.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IRedditService, RedditService>();
builder.Services.AddSingleton<ITextCleaningService, TextCleaningService>();
builder.Services.AddSingleton<ITtsService, TtsService>();
builder.Services.AddSingleton<IRssFeedService, RssFeedService>();
builder.Services.AddSingleton<ICatalogService, CatalogService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
