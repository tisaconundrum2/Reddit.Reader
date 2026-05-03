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

        // Append a bing tone at the end using ffmpeg
        await AppendBingAsync(outPath, ct);

        var fileInfo = new FileInfo(outPath);
        logger.LogInformation("[tts] Wrote {Path} ({Bytes} bytes)", outPath, fileInfo.Length);
        return fileInfo;
    }

    private async Task AppendBingAsync(string mp3Path, CancellationToken ct)
    {
        var tmpPath = mp3Path + ".bing.tmp.mp3";
        try
        {
            // Generate a 880 Hz sine wave bing (~0.5s with fade-out) and concatenate to the TTS audio
            var args = $"-y -i \"{mp3Path}\" "
                + "-f lavfi -i \"sine=frequency=880:duration=0.5,afade=t=out:st=0.3:d=0.2\" "
                + "-filter_complex \"[0:a][1:a]concat=n=2:v=0:a=1[out]\" "
                + $"-map \"[out]\" \"{tmpPath}\"";

            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            proc.Start();
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode == 0 && File.Exists(tmpPath))
            {
                File.Move(tmpPath, mp3Path, overwrite: true);
                logger.LogInformation("[tts] Bing appended to {Path}", mp3Path);
            }
            else
            {
                var err = await proc.StandardError.ReadToEndAsync(ct);
                logger.LogWarning("[tts] ffmpeg bing failed (exit {Code}): {Err}", proc.ExitCode, err);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[tts] Could not append bing (ffmpeg may not be installed), skipping.");
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
    }
}
