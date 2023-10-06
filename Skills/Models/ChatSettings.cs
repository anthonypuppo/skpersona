using System.Text.Json.Serialization;

namespace SKPersona.Skills.Models;

public class ChatSettings
{
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    [JsonPropertyName("logitBiases")]
    public IDictionary<int, int> LogitBiases { get; set; } = new Dictionary<int, int>();
}
