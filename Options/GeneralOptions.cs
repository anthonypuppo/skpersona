namespace SKPersona.Options;

public class GeneralOptions
{
    public const string General = "General";

    public enum PersonaType
    {
        Base = 1,
        Punctuation,
        Random,
        Trained
    }

    public PersonaType Persona { get; set; } = PersonaType.Random;
}
