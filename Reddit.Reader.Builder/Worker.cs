using Reddit.Reader.Builder.Models;
using Reddit.Reader.Builder.Services;

namespace Reddit.Reader.Builder;

public sealed class Worker(
    IRedditService redditService,
    ITextCleaningService cleaningService,
    ITtsService ttsService,
    IRssFeedService rssFeedService,
    ICatalogService catalogService,
    IConfiguration config,
    IHostApplicationLifetime lifetime,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly List<PipelineItem> _items = [];

    private record PipelineItem(RedditPost Post)
    {
        public string? CleanedText { get; set; }
        public FileInfo? Mp3File { get; set; }
        public string? Mp3Url { get; set; }
        public bool Failed { get; set; }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subreddits = config.GetSection("Reddit:Subreddits").Get<string[]>()
            ?? ["MaliciousCompliance"];
        var filter = config["Reddit:Filter"] ?? "hot";

        try
        {
            await BuildCatalogAsync(subreddits, filter, stoppingToken);
            if (config.GetValue<bool>("Pipeline:SeedCatalog"))
            {
                logger.LogInformation("Seeded catalog only; skipping processing steps.");
                return;
            }

            await CleanPostsAsync(stoppingToken);
            await GenerateTtsAsync(stoppingToken);
            await UpdateRssFeedAsync(stoppingToken);
            await RecordToCatalogAsync(stoppingToken);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private async Task BuildCatalogAsync(string[] subreddits, string filter, CancellationToken ct)
    {
        // --- Seed: fetch from Reddit and add pending entries to catalog ---
        logger.LogInformation("Fetching from Reddit for r/{Subreddits} ({Filter} filter)...", string.Join(", ", subreddits), filter);
        foreach (var subreddit in subreddits)
        {
            try
            {
                var posts = await redditService.FetchNewPostsAsync(subreddit, filter, ct);
                await catalogService.SeedPostsAsync(posts, ct);
                logger.LogInformation("Seeded {Count} posts for r/{Subreddit}.", posts.Count, subreddit);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ERROR seeding r/{Subreddit}", subreddit);
            }
        }

        if (config.GetValue<bool>("Pipeline:SeedCatalog"))
            return;

        // --- Resolve: pull pending entries and populate _items ---
        var postLimit = int.TryParse(config["Pipeline:PostLimit"], out var l) ? l : 1;
        var allPosts = await catalogService.GetPendingPostsAsync(ct);

        logger.LogInformation("Found {Count} pending post(s) in catalog.", allPosts.Count);

        if (allPosts.Count == 0)
        {
            logger.LogInformation("No pending posts to process.");
            return;
        }

        if (postLimit > 0 && allPosts.Count > postLimit)
        {
            logger.LogInformation("Capping to {Limit} post(s) ({Pending} pending).", postLimit, allPosts.Count);
            allPosts = allPosts[..postLimit];
        }

        _items.Clear();
        _items.AddRange(allPosts.Select(p => new PipelineItem(p)));

        logger.LogInformation("Resolved {Count} post(s).", _items.Count);
    }

    private async Task CleanPostsAsync(CancellationToken ct)
    {
        logger.LogInformation("=== Step 2: Clean posts ===");

        foreach (var item in _items.Where(i => !i.Failed))
        {
            try
            {
                item.CleanedText = await cleaningService.CleanAsync(item.Post.Title, item.Post.Selftext, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "  [clean] FAILED for {PostId}", item.Post.PostId);
                item.Failed = true;
            }
        }
    }

    private async Task GenerateTtsAsync(CancellationToken ct)
    {
        logger.LogInformation("=== Step 3: Generate TTS ===");
        var feedBaseUrl = config["Pipeline:FeedBaseUrl"] ?? string.Empty;

        foreach (var item in _items.Where(i => !i.Failed))
        {
            try
            {
                var ttsText = $"{item.Post.Title}.\n\n{item.CleanedText}";
                item.Mp3File = await ttsService.GenerateMp3Async(item.Post.PostId, ttsText, null, ct);
                item.Mp3Url = string.IsNullOrWhiteSpace(feedBaseUrl)
                    ? item.Mp3File.Name
                    : $"{feedBaseUrl.TrimEnd('/')}/{item.Mp3File.Name}";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "  [tts] FAILED for {PostId}", item.Post.PostId);
                item.Failed = true;
            }
        }
    }

    private async Task UpdateRssFeedAsync(CancellationToken ct)
    {
        logger.LogInformation("=== Step 4: Update RSS feed ===");

        foreach (var item in _items.Where(i => !i.Failed))
        {
            try
            {
                await rssFeedService.AddEpisodeAsync(item.Post, item.Mp3File!, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "  [rss] FAILED for {PostId}", item.Post.PostId);
            }
        }
    }

    private async Task RecordToCatalogAsync(CancellationToken ct)
    {
        logger.LogInformation("=== Step 5: Record to catalog ===");
        int succeeded = 0, failed = 0;

        foreach (var item in _items)
        {
            if (item.Failed)
            {
                failed++;
                continue;
            }

            try
            {
                await catalogService.AddEntryAsync(item.Post, item.Mp3File!, item.Mp3Url!, ct);
                succeeded++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "  [catalog] FAILED for {PostId}", item.Post.PostId);
                failed++;
            }
        }

        logger.LogInformation("=== Done === Succeeded: {Succeeded} | Failed: {Failed}", succeeded, failed);
    }
}
