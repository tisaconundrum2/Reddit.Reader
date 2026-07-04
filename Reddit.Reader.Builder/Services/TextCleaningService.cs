using System.Net.Http.Json;
using System.Text.Json;
using Reddit.Reader.Builder.Models;

namespace Reddit.Reader.Builder.Services;

public sealed class TextCleaningService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<TextCleaningService> logger) : ITextCleaningService
{
    private const string SystemPrompt =
        "You are preparing story body text for a text-to-speech podcast narrator. " +
        "Fix grammar, punctuation, and spelling. Remove markdown formatting, " +
        "URLs, and anything that would sound awkward when read aloud. " +
        "Expand abbreviations where sensible. " +
        "Return ONLY the cleaned story body text. Do NOT include the title, " +
        "no explanations, no introductions, no meta-commentary.";

    public async Task<string> CleanAsync(string title, string selftext, CancellationToken ct = default)
    {
        var apiKey = config["GEMINI_API_KEY"]
            ?? throw new InvalidOperationException("GEMINI_API_KEY is not configured.");
        var model = config["Gemini:Model"] ?? "gemini-2.5-flash";

        var raw = string.IsNullOrWhiteSpace(selftext)
            ? title
            : selftext;

        var fullPrompt = $"{SystemPrompt}\n\n{raw}";

        var requestBody = new GeminiRequest
        {
            Contents =
            [
                new GeminiContent
                {
                    Parts = [new GeminiPart { Text = fullPrompt }]
                }
            ]
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        using var client = httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(requestBody)
        };

        logger.LogInformation("[clean] Sending to Gemini (model: {Model})...", model);
        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(json);

        var text = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("Gemini returned no text in response.");

        return text.Trim();
    }
}
