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
        var normal = Encode(text);
        var spacePrefix = Encode($" {text}");
        var tokens = new List<List<int>>() { normal, spacePrefix };

        return tokens.SelectMany((x) => x).Distinct().ToList();
    }
}
