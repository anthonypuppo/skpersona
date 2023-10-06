using System.Text.Json.Serialization;

namespace SKPersona.Skills.Models;

public class LogitBias
{
    [JsonPropertyName("token")]
    public int Token { get; set; }

    [JsonPropertyName("bias")]
    public int Bias { get; set; }
}
