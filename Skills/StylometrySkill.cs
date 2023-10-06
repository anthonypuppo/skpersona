using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Catalyst;
using CsvHelper;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Mosaik.Core;
using SKPersona.Skills.Models;
using SKPersona.Utilities;

namespace SKPersona.Skills;

public class StylometrySkill
{
    static StylometrySkill()
    {
        Catalyst.Models.English.Register();
    }

    [SKFunction, Description("Gets random logit biases")]
    public SKContext GetRandomLogitBiases(
        SKContext context,
        [Description("The CSV with part of speech records")] string input)
    {
        using var stringReader = new StringReader(input);
        using var csvReader = new CsvReader(stringReader, CultureInfo.InvariantCulture);
        var records = csvReader.GetRecords<PartOfSpeechRecord>().ToList();
        var randomLogitBiases = records
            .SelectMany((x) => CL100kTokenizer.GetAugmentedTokens(x.Value)
                .Select((y) => new LogitBias()
                {
                    Token = y,
                    Bias = Random.Shared.Next(-10, 10),
                }))
            .DistinctBy((x) => x.Token)
            .OrderByDescending((x) => x.Token)
            .ToList();
        var logitBiasesJson = JsonSerializer.Serialize(randomLogitBiases);

        context.Variables.Update(logitBiasesJson);

        return context;
    }

    [SKFunction, Description("Extracts the logit biases for each part of speech in the input text")]
    public async Task<SKContext> ExtractLogitBiases(
        SKContext context,
        [Description("The input text")] string input,
        CancellationToken cancellationToken = default)
    {
        var pipeline = await Pipeline.ForAsync(Language.English);
        var document = new Document(input, Language.English);

        pipeline.ProcessSingle(document, cancellationToken);

        var calculatedLogitBiases = CalculateLogitBiases(document);
        var logitBiases = calculatedLogitBiases
            .Where((x) => x.Key is PartOfSpeech.ADJ or PartOfSpeech.ADV or PartOfSpeech.PRON or PartOfSpeech.PUNCT or PartOfSpeech.VERB)
            .SelectMany((x) => x.Value
                .SelectMany((y) => CL100kTokenizer.GetAugmentedTokens(y.Key)
                    .Select((z) => new LogitBias()
                    {
                        Token = z,
                        Bias = (int)Math.Round(Math.Clamp(y.Value * -1, 0, 10), MidpointRounding.AwayFromZero)
                    }))
                .OrderByDescending((y) => y.Bias)
                .Take(10))
            .DistinctBy((x) => x.Token)
            .OrderByDescending((x) => x.Token)
            .Take(50)
            .ToList();
        var logitBiasesJson = JsonSerializer.Serialize(logitBiases);

        context.Variables.Update(logitBiasesJson);

        return context;
    }

    private static Dictionary<PartOfSpeech, Dictionary<string, double>> CalculateLogitBiases(Document document)
    {
        var totalWordCount = document.Spans.SelectMany((x) => x.Tokens).Count();
        var posCounts = CountPartsOfSpeech(document);
        var posCountsSums = posCounts.ToDictionary((x) => x.Key, (x) => x.Value.Values.Sum());
        var logitBiases = new Dictionary<PartOfSpeech, Dictionary<string, double>>();

        foreach (var posCount in posCounts)
        {
            var pos = posCount.Key;
            var totalPosWords = posCountsSums[pos];

            logitBiases[pos] = new Dictionary<string, double>();

            foreach (var wordCount in posCount.Value)
            {
                var word = wordCount.Key;
                var count = wordCount.Value;
                var otherPosWordCount = totalWordCount - count;
                var otherPosCount = totalWordCount - totalPosWords;
                var logitBias = Math.Log((double)count / otherPosWordCount) - Math.Log((double)totalPosWords / otherPosCount);

                logitBiases[pos][word] = logitBias;
            }
        }

        return logitBiases;
    }

    private static Dictionary<PartOfSpeech, Dictionary<string, int>> CountPartsOfSpeech(Document document)
    {
        var posCounts = new Dictionary<PartOfSpeech, Dictionary<string, int>>();

        foreach (var sentence in document)
        {
            foreach (var word in sentence)
            {
                var pos = word.POS;
                var value = word.Value.ToLower();

                if (!posCounts.ContainsKey(pos))
                {
                    posCounts[pos] = new Dictionary<string, int>();
                }

                if (!posCounts[pos].ContainsKey(value))
                {
                    posCounts[pos][value] = 0;
                }

                posCounts[pos][value]++;
            }
        }

        return posCounts;
    }
}
