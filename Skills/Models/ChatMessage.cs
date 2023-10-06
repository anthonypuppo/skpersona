using System.Text.Json.Serialization;

namespace SKPersona.Skills.Models;

public class ChatMessage
{
    [JsonPropertyName("role")]
    public ChatMessageRole? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
