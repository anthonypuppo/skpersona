using System.Globalization;
using SharpToken;

namespace SKPersona.Utilities;

public static class CL100kTokenizer
{
    private static readonly GptEncoding encoding;

    static CL100kTokenizer()
    {
        encoding = GptEncoding.GetEncoding("cl100k_base");
    }

    public static List<int> Encode(string text)
    {
        var tokens = encoding.Encode(text);

        return tokens;
    }

    public static List<int> GetAugmentedTokens(string text)
    {
        var space = Encode($" {text}");
        var lower = Encode(text.ToLower());
        var upper = Encode(text.ToUpper());
        var capitalize = text is { Length: > 1 } ? Encode($"{char.ToUpper(text[0])}{text[1..]}") : null;
        var title = text.Contains(' ') ? Encode(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text)) : null;
        var tokens = new List<List<int>?>() { space, lower, upper, capitalize, title };

        return tokens
            .SelectMany((x) => x ?? Enumerable.Empty<int>())
            .Distinct()
            .ToList();
    }
}
