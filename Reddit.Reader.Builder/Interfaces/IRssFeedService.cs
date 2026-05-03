using Reddit.Reader.Builder.Models;

namespace Reddit.Reader.Builder.Services;

public interface IRssFeedService
{
    /// <summary>
    /// Appends a new episode entry to feed-0001.xml. No-ops if the post ID is already present.
    /// </summary>
    Task AddEpisodeAsync(RedditPost post, FileInfo mp3File, CancellationToken ct = default);
}
