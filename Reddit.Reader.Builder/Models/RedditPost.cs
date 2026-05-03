using System.Text.Json.Serialization;

namespace Reddit.Reader.Builder.Models;

public sealed class RedditPost
{
    [JsonPropertyName("id")]
    public string PostId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("selftext")]
    public string Selftext { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("num_comments")]
    public int NumComments { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("permalink")]
    public string Permalink { get; set; } = string.Empty;

    [JsonPropertyName("created_utc")]
    public long CreatedUtc { get; set; }

    /// <summary>Populated by RedditService after deserialization; not part of the API payload.</summary>
    [JsonIgnore]
    public string Subreddit { get; set; } = string.Empty;
}

public sealed class RedditChild
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public RedditPost? Data { get; set; }
}

public sealed class RedditListingData
{
    [JsonPropertyName("children")]
    public List<RedditChild> Children { get; set; } = [];

    [JsonPropertyName("after")]
    public string? After { get; set; }
}

public sealed class RedditListing
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public RedditListingData? Data { get; set; }
}
