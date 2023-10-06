using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using SKPersona.Extensions;
using SKPersona.Skills.Models;
using SKPersona.Utilities;

namespace SKPersona.Skills;

public class ChatSkill
{
    public static class Params
    {
        public const string ChatSettingsJson = "chatSettingsJson";
        public const string ChatHistoryJson = "chatHistoryJson";
    }

    private readonly IKernel kernel;
    private readonly ChannelWriter<string> channelWriter;

    public ChatSkill(IKernel kernel, ChannelWriter<string> channelWriter)
    {
        this.kernel = kernel;
        this.channelWriter = channelWriter;
    }

    [SKFunction, Description("Chat with the assistant")]
    public async Task<SKContext> Chat(
        SKContext context,
        [Description("The current user message")] string input,
        [SKName(Params.ChatSettingsJson), Description("Optional, the chat settings as a JSON string")] string? chatSettingsJson = null,
        [SKName(Params.ChatHistoryJson), Description("Optional, the chat history as a JSON string")] string? chatHistoryJson = null,
        CancellationToken cancellationToken = default)
    {
        var chatSettings = chatSettingsJson is { Length: > 0 }
            ? JsonSerializer.Deserialize<ChatSettings>(chatSettingsJson)
            : null;
        var chatHistoryMessages = chatHistoryJson is { Length: > 0 }
            ? JsonSerializer.Deserialize<List<ChatMessage>>(chatHistoryJson)
            : null;
        var chatRequestSettings = new ChatRequestSettings()
        {
            Temperature = chatSettings?.Temperature ?? 0,
            TokenSelectionBiases = chatSettings?.LogitBiases is { Count: > 0 }
                ? chatSettings.LogitBiases
                : new Dictionary<int, int>(),
        };
        var chatCompletionService = kernel.GetService<IChatCompletion>();
        var chatHistory = chatCompletionService.CreateNewChat();
        var chatMessageStringBuilder = new StringBuilder();

        if (chatHistoryMessages is { Count: > 0 })
        {
            foreach (var chatMessage in chatHistoryMessages
                .TakeWhileAggregate(
                    seed: 0,
                    aggregator: (tokens, next) => tokens + (next.Content is { Length: > 0 }
                        ? CL100kTokenizer.Encode(next.Content).Count
                        : 0),
                    predicate: (tokens) => tokens <= 2048))
            {
                switch (chatMessage.Role)
                {
                    case ChatMessageRole.User:
                        chatHistory.AddUserMessage(chatMessage.Content ?? String.Empty);

                        break;
                    case ChatMessageRole.Assistant:
                        chatHistory.AddAssistantMessage(chatMessage.Content ?? String.Empty);

                        break;
                }
            }
        }

        chatHistory.AddUserMessage(input);

        await foreach (var token in chatCompletionService
            .GenerateMessageStreamAsync(chatHistory, chatRequestSettings, cancellationToken))
        {
            await channelWriter.WriteAsync(token, cancellationToken);
            chatMessageStringBuilder.Append(token);
        }

        context.Variables.Update(chatMessageStringBuilder.ToString());

        return context;
    }
}
