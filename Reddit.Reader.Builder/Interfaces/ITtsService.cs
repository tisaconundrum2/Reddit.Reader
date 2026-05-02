namespace Reddit.Reader.Builder.Services;

public interface ITtsService
{
    Task<FileInfo> GenerateMp3Async(string postId, string text, string? voice = null, CancellationToken ct = default);
}
