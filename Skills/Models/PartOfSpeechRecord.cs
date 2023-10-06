using CsvHelper.Configuration.Attributes;

namespace SKPersona.Skills.Models;

public class PartOfSpeechRecord
{
    [Name("Id")]
    public int Id { get; set; }

    [Name("Pos")]
    public string Pos { get; set; } = string.Empty;

    [Name("Value")]
    public string Value { get; set; } = string.Empty;
}
