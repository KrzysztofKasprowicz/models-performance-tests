using System.Runtime.CompilerServices;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

public sealed class AzureOpenAiChatModel : IChatModel
{
    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _options;

    public AzureOpenAiChatModel(AzureOpenAIClient client, string deploymentName, string reasoningEffort)
    {
        _chatClient = client.GetChatClient(deploymentName);
        _options = ChatCompletionOptionsFactory.Create(reasoningEffort);
    }

    public async IAsyncEnumerable<string> StreamReplyTextAsync(
        string systemPrompt,
        IReadOnlyList<ConversationMessage> conversation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = conversation
            .Select(ToChatMessage)
            .Prepend(new SystemChatMessage(systemPrompt))
            .ToArray();

        await foreach (var update in _chatClient
                           .CompleteChatStreamingAsync(messages, _options, cancellationToken)
                           .ConfigureAwait(false))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return part.Text;
                }
            }
        }
    }

    private static ChatMessage ToChatMessage(ConversationMessage message) => message.Role switch
    {
        ConversationRole.User => new UserChatMessage(message.Text),
        ConversationRole.Assistant => new AssistantChatMessage(message.Text),
        _ => throw new InvalidOperationException($"Unknown role: {message.Role}"),
    };
}
