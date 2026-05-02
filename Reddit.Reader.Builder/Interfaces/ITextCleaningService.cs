namespace Reddit.Reader.Builder.Services;

public interface ITextCleaningService
{
    Task<string> CleanAsync(string title, string selftext, CancellationToken ct = default);
}
