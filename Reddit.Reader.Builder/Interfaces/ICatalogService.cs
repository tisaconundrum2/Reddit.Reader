using Reddit.Reader.Builder.Models;

namespace Reddit.Reader.Builder.Services;

public interface ICatalogService
{
    /// <summary>
    /// Appends a processed episode to catalog.json.
    /// If the post already has a pending entry, promotes it to completed.
    /// No-ops if a completed entry already exists.
    /// </summary>
    Task AddEntryAsync(RedditPost post, FileInfo mp3File, string mp3Url, CancellationToken ct = default);

    /// <summary>
    /// Adds a pending catalog entry (no MP3 yet) so the pipeline can pick it up later
    /// without hitting the Reddit API. No-ops if any entry already exists for the post.
    /// </summary>
    Task SeedEntryAsync(RedditPost post, CancellationToken ct = default);

    /// <summary>
    /// Returns all pending catalog entries reconstructed as RedditPosts.
    /// </summary>
    Task<List<RedditPost>> GetPendingPostsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns true if the post ID already has a catalog entry (pending or completed).
    /// </summary>
    Task<bool> ExistsAsync(string postId, CancellationToken ct = default);
}
