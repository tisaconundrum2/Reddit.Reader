using Reddit.Reader.Builder.Models;

namespace Reddit.Reader.Builder.Services;

public interface IRedditService
{
    Task<List<RedditPost>> FetchNewPostsAsync(string subreddit, string filter = "hot", CancellationToken ct = default);
    void MarkSeen(string postId);
}
