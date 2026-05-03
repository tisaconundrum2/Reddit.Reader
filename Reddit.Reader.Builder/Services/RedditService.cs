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
        var seenIdsPath = config["Pipeline:SeenIdsFile"] ?? "seen_ids.json";

        var requestUrl = $"{RedditBase}{subreddit}/{filter}.json?raw_json=1&limit=25";

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
        var seen = LoadSeenIds(seenIdsPath);
        var newPosts = new List<RedditPost>();

        foreach (var child in children)
        {
            var post = child.Data;
            if (post is null) continue;

            var postId = post.PostId.Trim();
            if (string.IsNullOrEmpty(postId) || seen.Contains(postId) || string.IsNullOrWhiteSpace(post.Title))
                continue;

            post.Subreddit = subreddit;
            newPosts.Add(post);
            seen.Add(postId);
        }

        SaveSeenIds(seenIdsPath, seen);
        logger.LogInformation("[fetch] r/{Subreddit} — {Count} new post(s)", subreddit, newPosts.Count);
        return newPosts;
    }

    private static HashSet<string> LoadSeenIds(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            var json = File.ReadAllText(path);
            var ids = JsonSerializer.Deserialize<List<string>>(json);
            return ids is null ? [] : [.. ids];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveSeenIds(string path, HashSet<string> ids)
    {
        var sorted = ids.OrderBy(x => x).ToList();
        File.WriteAllText(path, JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true }));
    }
}
