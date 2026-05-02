using System.Net.Http.Json;
using Reddit.Reader.Builder.Models;

namespace Reddit.Reader.Builder.Services;

public sealed class TtsService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<TtsService> logger) : ITtsService
{
    public async Task<FileInfo> GenerateMp3Async(string postId, string text, string? voice = null, CancellationToken ct = default)
    {
        var baseUrl = config["KokoroTts:BaseUrl"] ?? "http://localhost:5000";
        var defaultVoice = config["KokoroTts:Voice"] ?? "af_heart";
        var speed = double.TryParse(config["KokoroTts:Speed"], out var s) ? s : 1.0;
        var outputDir = config["Pipeline:OutputDir"] ?? "output";

        Directory.CreateDirectory(outputDir);
        var outPath = Path.Combine(outputDir, $"{postId}.mp3");

        var ttsRequest = new TtsRequest
        {
            Text = text,
            Voice = voice ?? defaultVoice,
            Speed = speed
        };

        using var client = httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/tts")
        {
            Content = JsonContent.Create(ttsRequest)
        };

        logger.LogInformation("[tts] Generating MP3 for {PostId}...", postId);
        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        await File.WriteAllBytesAsync(outPath, bytes, ct);

        var fileInfo = new FileInfo(outPath);
        logger.LogInformation("[tts] Wrote {Path} ({Bytes} bytes)", outPath, bytes.Length);
        return fileInfo;
    }
}
