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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subreddits = config.GetSection("Reddit:Subreddits").Get<string[]>()
            ?? ["MaliciousCompliance"];
        var filter = config["Reddit:Filter"] ?? "hot";

        try
        {
            if (config.GetValue<bool>("Pipeline:SeedCatalog"))
            {
                await SeedCatalogAsync(subreddits, filter, stoppingToken);
            }
            else
            {
                var postLimit = int.TryParse(config["Pipeline:PostLimit"], out var l) ? l : 1;
                await RunPipelineAsync(subreddits, filter, postLimit, stoppingToken);
            }
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private async Task SeedCatalogAsync(string[] subreddits, string filter, CancellationToken ct)
    {
        logger.LogInformation("=== Seed mode: cataloguing posts as pending (no TTS/RSS) ===");
        int seeded = 0;

        foreach (var subreddit in subreddits)
        {
            List<RedditPost> posts;
            try
            {
                posts = await redditService.FetchNewPostsAsync(subreddit, filter, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[seed] ERROR fetching r/{Subreddit}", subreddit);
                continue;
            }

            foreach (var post in posts)
            {
                try
                {
                    await catalogService.SeedEntryAsync(post, ct);
                    seeded++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[seed] ERROR seeding {PostId}", post.PostId);
                }
            }
        }

        logger.LogInformation("=== Seed done === {Seeded} post(s) added as pending.", seeded);
    }

    private async Task RunPipelineAsync(string[] subreddits, string filter, int postLimit, CancellationToken ct)
    {
        logger.LogInformation("=== Step 1: Resolve posts (limit: {Limit}) ===", postLimit);
        var allowRedditApiFallback = config.GetValue("Pipeline:AllowRedditApiFallback", false);

        // Prefer pending catalog entries so GHA never needs to call the Reddit API.
        var allPosts = await catalogService.GetPendingPostsAsync(ct);

        if (allPosts.Count > 0)
        {
            logger.LogInformation(
                "Using {Count} pending post(s) from catalog (skipping Reddit API).",
                allPosts.Count);
        }
        else
        {
            if (!allowRedditApiFallback)
            {
                logger.LogInformation(
                    "No pending catalog entries and Reddit API fallback is disabled (Pipeline:AllowRedditApiFallback=false). Exiting.");
                return;
            }

            logger.LogInformation("No pending catalog entries — fetching from Reddit API.");

            foreach (var subreddit in subreddits)
            {
                try
                {
                    var posts = await redditService.FetchNewPostsAsync(subreddit, filter, ct);
                    allPosts.AddRange(posts);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[fetch] ERROR r/{Subreddit}", subreddit);
                }
            }

            if (allPosts.Count == 0)
            {
                logger.LogInformation("No new posts found.");
                return;
            }

            // Filter out posts already in catalog (pending or completed).
            var unconverted = new List<RedditPost>();
            foreach (var p in allPosts)
            {
                if (!await catalogService.ExistsAsync(p.PostId, ct))
                    unconverted.Add(p);
            }

            logger.LogInformation(
                "Catalog status: {Total} fetched | {Already} already in catalog | {Pending} to convert.",
                allPosts.Count,
                allPosts.Count - unconverted.Count,
                unconverted.Count);

            if (unconverted.Count == 0)
            {
                logger.LogInformation("All fetched posts are already in catalog. Nothing to do.");
                return;
            }

            allPosts = unconverted;
        }

        if (postLimit > 0 && allPosts.Count > postLimit)
        {
            logger.LogInformation("Capping to {Limit} post(s) ({Pending} pending).", postLimit, allPosts.Count);
            allPosts = allPosts[..postLimit];
        }

        logger.LogInformation("Processing {Count} post(s).", allPosts.Count);

        var feedBaseUrl = config["Pipeline:FeedBaseUrl"] ?? string.Empty;
        int succeeded = 0, failed = 0;

        for (int i = 0; i < allPosts.Count; i++)
        {
            var post = allPosts[i];
            logger.LogInformation("--- Post {Index}/{Total}: {PostId} ---", i + 1, allPosts.Count, post.PostId);
            logger.LogInformation("    {Title}", post.Title[..Math.Min(70, post.Title.Length)]);

            // Step 2: Clean
            string cleanedText;
            try
            {
                cleanedText = await cleaningService.CleanAsync(post.Title, post.Selftext, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "  [clean] FAILED for {PostId}", post.PostId);
                failed++;
                continue;
            }

            // Step 3: TTS → save MP3 locally
            // Prepend the title with a pause before the cleaned body
            var ttsText = $"{post.Title}.\n\n{cleanedText}";
            FileInfo mp3File;
            try
            {
                mp3File = await ttsService.GenerateMp3Async(post.PostId, ttsText, null, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "  [tts] FAILED for {PostId}", post.PostId);
                failed++;
                continue;
            }

            var mp3Url = string.IsNullOrWhiteSpace(feedBaseUrl)
                ? mp3File.Name
                : $"{feedBaseUrl.TrimEnd('/')}/{mp3File.Name}";

            // Step 4: Update RSS feed — persisted immediately; failure here does not abort the post
            try
            {
                await rssFeedService.AddEpisodeAsync(post, mp3File, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "  [rss] FAILED for {PostId}", post.PostId);
            }

            // Step 5: Record to catalog
            try
            {
                await catalogService.AddEntryAsync(post, mp3File, mp3Url, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "  [catalog] FAILED for {PostId}", post.PostId);
            }

            redditService.MarkSeen(post.PostId);
            succeeded++;
        }

        logger.LogInformation("=== Done === Succeeded: {Succeeded} | Failed: {Failed}", succeeded, failed);
    }
}
