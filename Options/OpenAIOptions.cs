using System.ComponentModel.DataAnnotations;

namespace SKPersona.Options;

public class OpenAIOptions
{
    public const string OpenAI = "OpenAI";

    [Required]
    public string Model { get; set; } = String.Empty;

    [Required]
    public string Key { get; set; } = String.Empty;
}
