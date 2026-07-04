using System.Text.Json;
using Reddit.Reader.Builder.Models;

namespace Reddit.Reader.Builder.Services;

public sealed class RapidApiRedditService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<RapidApiRedditService> logger) : IRedditService
{
    private const string BaseUrl = "https://reddit3.p.rapidapi.com/v1/reddit/posts";

    public async Task<List<RedditPost>> FetchNewPostsAsync(string subreddit, string filter = "hot", CancellationToken ct = default)
    {
        var period = config["Reddit:TopPeriod"] ?? "month";
        var apiKey = config["RAPIDAPI_KEY"]
            ?? throw new InvalidOperationException("RAPIDAPI_KEY is not configured.");
        var apiHost = config["RapidApi:Host"] ?? "reddit3.p.rapidapi.com";
        var minWordCount = int.TryParse(config["Reddit:MinWordCount"], out var mwc) ? mwc : 0;

        // The API endpoint is a little weird... MaliciousCompliance will return 0 results if it doesn't have a /?t=month query parameter.
        // https://reddit3.p.rapidapi.com/v1/reddit/posts?url=https%3A%2F%2Fwww.reddit.com%2Fr%2FMaliciousCompliance%2F%3Ft%3Dmonth&filter=top
        var redditUrl = $"https://www.reddit.com/r/{subreddit}/?t={period}";
        var requestUrl = $"{BaseUrl}?url={Uri.EscapeDataString(redditUrl)}&filter={filter}";

        using var client = httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("x-rapidapi-key", apiKey);
        request.Headers.Add("x-rapidapi-host", apiHost);

        logger.LogInformation("[rapid-fetch] r/{Subreddit} — requesting posts (filter: {Filter})", subreddit, filter);
        var response = await client.SendAsync(request, ct);
        logger.LogInformation("[rapid-fetch] r/{Subreddit} — HTTP {StatusCode}", subreddit, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("[rapid-fetch] r/{Subreddit} — non-success status, skipping.", subreddit);
            return [];
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            logger.LogWarning("[rapid-fetch] r/{Subreddit} — empty response body, skipping.", subreddit);
            return [];
        }

        RapidApiPost? apiResponse;
        try
        {
            apiResponse = JsonSerializer.Deserialize<RapidApiPost>(body);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[rapid-fetch] r/{Subreddit} — failed to parse response JSON.", subreddit);
            return [];
        }

        var posts = apiResponse?.Body ?? [];
        var newPosts = new List<RedditPost>();

        foreach (var post in posts)
        {
            var postId = post.Id?.Trim();
            if (string.IsNullOrEmpty(postId) || string.IsNullOrWhiteSpace(post.Title))
                continue;

            if (minWordCount > 0)
            {
                var selftext = post.Selftext ?? string.Empty;
                var wordCount = selftext.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount < minWordCount)
                {
                    logger.LogInformation("[rapid-fetch] r/{Subreddit} — skipping \"{Title}\" ({WordCount} words < {Min} minimum)",
                        subreddit, post.Title[..Math.Min(50, post.Title.Length)], wordCount, minWordCount);
                    continue;
                }
            }

            newPosts.Add(new RedditPost
            {
                PostId = postId,
                Subreddit = post.Subreddit ?? subreddit,
                Title = post.Title,
                Selftext = post.Selftext ?? string.Empty,
                Author = post.Author ?? string.Empty,
                Permalink = post.Permalink ?? string.Empty,
                Score = post.Score,
                NumComments = post.NumComments
            });
        }

        logger.LogInformation("[rapid-fetch] r/{Subreddit} — {Count} new post(s)", subreddit, newPosts.Count);
        return newPosts;
    }
}
