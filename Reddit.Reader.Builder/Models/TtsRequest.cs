using System.Text.Json.Serialization;

namespace Reddit.Reader.Builder.Models;

public sealed class TtsRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("voice")]
    public string Voice { get; set; } = "af_heart";

    [JsonPropertyName("speed")]
    public double Speed { get; set; } = 1.0;
}
