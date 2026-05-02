using System.Text.Json;
using System.Text.Json.Serialization;
using Reddit.Reader.Builder.Models;

namespace Reddit.Reader.Builder.Services;

public sealed class CatalogService(
    IConfiguration config,
    ILogger<CatalogService> logger) : ICatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public async Task AddEntryAsync(RedditPost post, FileInfo mp3File, string mp3Url, CancellationToken ct = default)
    {
        var catalogFile = GetCatalogPath();
        var entries = await LoadAsync(catalogFile, ct);

        if (entries.Any(e => e.PostId == post.PostId))
        {
            logger.LogInformation("[catalog] {PostId} already catalogued, skipping.", post.PostId);
            return;
        }

        entries.Add(new CatalogEntry
        {
            PostId      = post.PostId,
            Subreddit   = post.Subreddit,
            Title       = post.Title,
            Author      = post.Author,
            Permalink   = post.Permalink,
            Score       = post.Score,
            NumComments = post.NumComments,
            Mp3FileName = mp3File.Name,
            Mp3Url      = mp3Url,
            ProcessedAt = DateTimeOffset.UtcNow
        });

        await SaveAsync(catalogFile, entries, ct);
        logger.LogInformation("[catalog] Recorded {PostId} → {FileName}", post.PostId, mp3File.Name);
    }

    public async Task<bool> ExistsAsync(string postId, CancellationToken ct = default)
    {
        var entries = await LoadAsync(GetCatalogPath(), ct);
        return entries.Any(e => e.PostId == postId);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private string GetCatalogPath()
    {
        var outputDir = config["Pipeline:OutputDir"] ?? "output";
        Directory.CreateDirectory(outputDir);
        return Path.Combine(outputDir, "catalog.json");
    }

    private static async Task<List<CatalogEntry>> LoadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return [];

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<CatalogEntry>>(stream, JsonOptions, ct)
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static async Task SaveAsync(string path, List<CatalogEntry> entries, CancellationToken ct)
    {
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, ct);
    }
}
