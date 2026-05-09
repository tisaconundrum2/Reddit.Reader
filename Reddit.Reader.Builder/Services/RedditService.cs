using System.Text.Json;
using Reddit.Reader.Builder.Models;

namespace Reddit.Reader.Builder.Services;

public sealed class RedditService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<RedditService> logger) : IRedditService
{
    private const string RedditBase = "https://www.reddit.com/r/";

    public async Task<List<RedditPost>> FetchNewPostsAsync(string subreddit, string filter = "hot", CancellationToken ct = default)
    {
        var userAgent = config["Reddit:UserAgent"]
            ?? throw new InvalidOperationException("Reddit:UserAgent is not configured.");

        var topPeriod = config["Reddit:TopPeriod"] ?? "month";
        var minWordCount = int.TryParse(config["Reddit:MinWordCount"], out var mwc) ? mwc : 0;

        var requestUrl = filter.Equals("top", StringComparison.OrdinalIgnoreCase)
            ? $"{RedditBase}{subreddit}/{filter}.json?t={topPeriod}&raw_json=1&limit=25"
            : $"{RedditBase}{subreddit}/{filter}.json?raw_json=1&limit=25";

        using var client = httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("User-Agent", userAgent);

        logger.LogInformation("[fetch] r/{Subreddit} — requesting posts (filter: {Filter})", subreddit, filter);
        var response = await client.SendAsync(request, ct);
        logger.LogInformation("[fetch] r/{Subreddit} — HTTP {StatusCode}", subreddit, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("[fetch] r/{Subreddit} — non-success status, skipping.", subreddit);
            return [];
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            logger.LogWarning("[fetch] r/{Subreddit} — empty response body, skipping.", subreddit);
            return [];
        }

        RedditListing? listing;
        try
        {
            listing = JsonSerializer.Deserialize<RedditListing>(body);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[fetch] r/{Subreddit} — failed to parse response JSON.", subreddit);
            return [];
        }

        var children = listing?.Data?.Children ?? [];
        var newPosts = new List<RedditPost>();

        foreach (var child in children)
        {
            var post = child.Data;
            if (post is null) continue;

            var postId = post.PostId.Trim();
            if (string.IsNullOrEmpty(postId) || string.IsNullOrWhiteSpace(post.Title))
                continue;

            if (minWordCount > 0)
            {
                var wordCount = post.Selftext.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount < minWordCount)
                {
                    logger.LogInformation("[fetch] r/{Subreddit} — skipping \"{Title}\" ({WordCount} words < {Min} minimum)",
                        subreddit, post.Title[..Math.Min(50, post.Title.Length)], wordCount, minWordCount);
                    continue;
                }
            }

            newPosts.Add(post);
        }

        logger.LogInformation("[fetch] r/{Subreddit} — {Count} new post(s)", subreddit, newPosts.Count);
        return newPosts;
    }


}
