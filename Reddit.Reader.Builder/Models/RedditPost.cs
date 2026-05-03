using System.Text.Json.Serialization;

namespace Reddit.Reader.Builder.Models;

public sealed record RedditPost
{
    [JsonPropertyName("id")]
    public string PostId { get; init; } = string.Empty;

    [JsonPropertyName("subreddit")]
    public string Subreddit { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("selftext")]
    public string Selftext { get; init; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; init; } = string.Empty;

    [JsonPropertyName("permalink")]
    public string Permalink { get; init; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("num_comments")]
    public int NumComments { get; init; }
}

public sealed record RedditChild
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public RedditPost? Data { get; init; }
}

public sealed record RedditListingData
{
    [JsonPropertyName("children")]
    public IReadOnlyList<RedditChild> Children { get; init; } = [];

    [JsonPropertyName("after")]
    public string? After { get; init; }
}

public sealed record RedditListing
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public RedditListingData? Data { get; init; }
}
