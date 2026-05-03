using System.Text.Json.Serialization;

namespace Reddit.Reader.Builder.Models;

public sealed class CatalogEntry
{
    [JsonPropertyName("postId")]
    public string PostId { get; set; } = string.Empty;

    [JsonPropertyName("subreddit")]
    public string Subreddit { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("permalink")]
    public string Permalink { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("numComments")]
    public int NumComments { get; set; }

    [JsonPropertyName("mp3FileName")]
    public string Mp3FileName { get; set; } = string.Empty;

    [JsonPropertyName("mp3Url")]
    public string Mp3Url { get; set; } = string.Empty;

    [JsonPropertyName("processedAt")]
    public DateTimeOffset ProcessedAt { get; set; }

    /// <summary>True when this entry was seeded locally but not yet processed by the full pipeline.</summary>
    [JsonPropertyName("pending")]
    public bool Pending { get; set; }

    /// <summary>Raw post body stored during seeding so the pipeline never needs to call the Reddit API.</summary>
    [JsonPropertyName("selftext")]
    public string? Selftext { get; set; }
}
