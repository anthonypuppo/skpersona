using System.Text.Json.Serialization;

namespace SKPersona.Skills.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChatMessageRole
{
    User = 1,
    Assistant
}
