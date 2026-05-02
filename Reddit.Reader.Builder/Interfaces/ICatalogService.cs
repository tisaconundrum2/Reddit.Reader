using Reddit.Reader.Builder.Models;

namespace Reddit.Reader.Builder.Services;

public interface ICatalogService
{
    /// <summary>
    /// Appends a processed episode to catalog.json. No-ops if the post ID is already present.
    /// </summary>
    Task AddEntryAsync(RedditPost post, FileInfo mp3File, string mp3Url, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the post ID already has a catalog entry (i.e. an MP3 was already created).
    /// </summary>
    Task<bool> ExistsAsync(string postId, CancellationToken ct = default);
}
