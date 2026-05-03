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
        var postLimit = int.TryParse(config["Pipeline:PostLimit"], out var l) ? l : 1;
        var subreddits = config.GetSection("Reddit:Subreddits").Get<string[]>()
            ?? ["MaliciousCompliance"];
        var filter = config["Reddit:Filter"] ?? "hot";

        try
        {
            await RunPipelineAsync(subreddits, filter, postLimit, stoppingToken);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private async Task RunPipelineAsync(string[] subreddits, string filter, int postLimit, CancellationToken ct)
    {
        logger.LogInformation("=== Step 1: Fetch posts (limit: {Limit}) ===", postLimit);
        var allPosts = new List<RedditPost>();

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

        // Report how many fetched posts still need conversion
        var unconverted = new List<RedditPost>();
        foreach (var p in allPosts)
        {
            if (!await catalogService.ExistsAsync(p.PostId, ct))
                unconverted.Add(p);
        }

        logger.LogInformation(
            "Catalog status: {Total} fetched | {Already} already converted | {Pending} pending conversion.",
            allPosts.Count,
            allPosts.Count - unconverted.Count,
            unconverted.Count);

        if (unconverted.Count == 0)
        {
            logger.LogInformation("All fetched posts are already converted. Nothing to do.");
            return;
        }

        // Work only from posts not yet in the catalog
        allPosts = unconverted;

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
                ? mp3File.FullName
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
