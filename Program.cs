using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Reliability.Basic;
using SKPersona.Options;
using SKPersona.Skills;
using SKPersona.Skills.Models;
using SKPersona.Utilities;
using DataNamespaceMarker = SKPersona.Data.NamespaceMarker;

Console.WriteLine("Starting...");

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();
var loggingConfigurationSection = configuration.GetSection("Logging");
var generalOptions = new GeneralOptions();
var openAIOptions = new OpenAIOptions();

configuration
    .GetSection(GeneralOptions.General)
    .Bind(generalOptions);
configuration
    .GetSection(OpenAIOptions.OpenAI)
    .Bind(openAIOptions);

if (!TryValidateObject(openAIOptions, out var openAIOptionsErrorMessages))
{
    foreach (var errorMessage in openAIOptionsErrorMessages)
    {
        Console.WriteLine(errorMessage);
    }

    Console.ReadKey();

    return;
}

var chatSettings = generalOptions.Persona switch
{
    GeneralOptions.PersonaType.Base => new ChatSettings() { Temperature = 0 },
    GeneralOptions.PersonaType.Punctuation => GetChatSettingsPunctuation(),
    GeneralOptions.PersonaType.Random => await GetChatSettingsRandom(loggingConfigurationSection, openAIOptions),
    GeneralOptions.PersonaType.Trained => await GetChatSettingsExtract(loggingConfigurationSection, openAIOptions),
    _ => throw new NotImplementedException(),
};
var chatHistory = new List<ChatMessage>();

Console.WriteLine("Ready!");
Console.WriteLine();

while (true)
{
    Console.Write("User: ");

    var input = Console.ReadLine();

    if (input is not { Length: > 0 })
    {
        Console.WriteLine();

        continue;
    }

    Console.Write("Assistant: ");

    var response = await Chat(loggingConfigurationSection, openAIOptions, chatSettings, chatHistory, input, Console.Write);

    Console.WriteLine();
    chatHistory.Add(new()
    {
        Role = ChatMessageRole.User,
        Content = input,
    });
    chatHistory.Add(new()
    {
        Role = ChatMessageRole.Assistant,
        Content = response,
    });
}

static bool TryValidateObject(object instance, [NotNullWhen(false)] out List<string>? errorMessages)
{
    var validationContext = new ValidationContext(instance);
    var validationResults = new List<ValidationResult>();

    if (!Validator.TryValidateObject(instance, validationContext, validationResults, validateAllProperties: true))
    {
        errorMessages = validationResults
            .Select((x) => $"DataAnnotation validation failed for '{instance.GetType()}': '{string.Join(",", x.MemberNames)}' with the error: '{x.ErrorMessage}'.")
            .ToList();

        return false;
    }

    errorMessages = null;

    return true;
}

static ChatSettings GetChatSettingsPunctuation()
{
    var negate = false;
    var chatSettings = new ChatSettings()
    {
        Temperature = 0
    };
    var punctuation = new[] { ".", "!", "?" };
    var logitBiases = punctuation
        .SelectMany((x) => CL100kTokenizer.GetAugmentedTokens(x).Select((y) => (Token: y, Bias: 10 * (negate ? -1 : 1))))
        .DistinctBy((x) => x.Token)
        .OrderByDescending((x) => x.Token)
        .ToDictionary((x) => x.Token, (x) => x.Bias);

    if (logitBiases is not null)
    {
        chatSettings.LogitBiases = logitBiases;
    }

    return chatSettings;
}

static async Task<ChatSettings> GetChatSettingsRandom(IConfigurationSection loggingConfigurationSection, OpenAIOptions openAIOptions)
{
    var chatSettings = new ChatSettings()
    {
        Temperature = 0
    };
    var trainText = ReadEmbeddedResource($"{typeof(DataNamespaceMarker).Namespace}.parts-of-speech.csv");
    var kernel = BuildKernel(loggingConfigurationSection, openAIOptions);

    kernel.ImportSkill(new StylometrySkill(), nameof(StylometrySkill));

    var function = kernel.Skills.GetFunction(nameof(StylometrySkill), nameof(StylometrySkill.GetRandomLogitBiases));
    var contextVariables = new ContextVariables(trainText);
    var result = await function.InvokeAsync(contextVariables);
    var logitBiases = JsonSerializer.Deserialize<List<LogitBias>>(result.Result)
        ?.ToDictionary((x) => x.Token, (x) => x.Bias);

    if (logitBiases is not null)
    {
        chatSettings.LogitBiases = logitBiases;
    }

    return chatSettings;
}

static async Task<ChatSettings> GetChatSettingsExtract(IConfigurationSection loggingConfigurationSection, OpenAIOptions openAIOptions)
{
    var chatSettings = new ChatSettings()
    {
        Temperature = 0
    };
    var trainText = ReadEmbeddedResource($"{typeof(DataNamespaceMarker).Namespace}.persona.txt");
    var kernel = BuildKernel(loggingConfigurationSection, openAIOptions);

    kernel.ImportSkill(new StylometrySkill(), nameof(StylometrySkill));

    var function = kernel.Skills.GetFunction(nameof(StylometrySkill), nameof(StylometrySkill.ExtractLogitBiases));
    var contextVariables = new ContextVariables(trainText);
    var result = await function.InvokeAsync(contextVariables);
    var logitBiases = JsonSerializer.Deserialize<List<LogitBias>>(result.Result)
        ?.ToDictionary((x) => x.Token, (x) => x.Bias);

    if (logitBiases is not null)
    {
        chatSettings.LogitBiases = logitBiases;
    }

    return chatSettings;
}

static async Task<string> Chat(
    IConfigurationSection loggingConfigurationSection,
    OpenAIOptions openAIOptions,
    ChatSettings chatSettings,
    List<ChatMessage> chatHistory,
    string input,
    Action<string> onTokenReceived)
{
    var channel = Channel.CreateUnbounded<string>();
    var kernel = BuildKernel(loggingConfigurationSection, openAIOptions);
    var contextVariables = new ContextVariables(input);

    kernel.ImportSkill(new ChatSkill(kernel, channel.Writer), nameof(ChatSkill));
    contextVariables.Set(ChatSkill.Params.ChatSettingsJson, JsonSerializer.Serialize(chatSettings));
    contextVariables.Set(ChatSkill.Params.ChatHistoryJson, JsonSerializer.Serialize(chatHistory));

    var function = kernel.Skills.GetFunction(nameof(ChatSkill), nameof(ChatSkill.Chat));
    var functionTask = function
        .InvokeAsync(contextVariables)
        .ContinueWith((task) =>
        {
            channel.Writer.TryComplete(task.Exception);

            return task.Result;
        });
    var channelReaderTask = Task.Run(async () =>
    {
        while (await channel.Reader.WaitToReadAsync())
        {
            while (channel.Reader.TryRead(out var token))
            {
                onTokenReceived(token);
            }
        }
    });

    await Task.WhenAll(functionTask, channelReaderTask);

    return functionTask.Result.Result;
}

static IKernel BuildKernel(IConfigurationSection loggingConfigurationSection, OpenAIOptions openAIOptions)
{
    var kernel = Kernel.Builder
        .WithLoggerFactory(GetLoggerFactory(loggingConfigurationSection))
        .WithRetryBasic(GetBasicRetryConfig())
        .WithOpenAIChatCompletionService(openAIOptions.Model, openAIOptions.Key)
        .Build();

    return kernel;
}

static ILoggerFactory GetLoggerFactory(IConfiguration configuration)
{
    return LoggerFactory.Create((builder) =>
        builder
            .AddConfiguration(configuration)
            .AddConsole());
}

static BasicRetryConfig GetBasicRetryConfig()
{
    return new BasicRetryConfig()
    {
        MaxRetryCount = 3,
        UseExponentialBackoff = true,
    };
}

static string ReadEmbeddedResource(string embeddedResourceFullyQualifiedName)
{
    var assembly = Assembly.GetExecutingAssembly();
    using var stream = assembly.GetManifestResourceStream(embeddedResourceFullyQualifiedName)!;
    using var reader = new StreamReader(stream);
    var embeddedResourceContent = reader.ReadToEnd();

    return embeddedResourceContent;
}
