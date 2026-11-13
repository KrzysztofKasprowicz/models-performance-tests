using System.Runtime.CompilerServices;
using Anthropic;
using Anthropic.Models.Messages;

namespace AzureOpenAiBenchmark;

/// <summary>
/// Claude deployment on Foundry, called through the Anthropic Messages API. Reasoning effort
/// "none" disables thinking; "low"/"medium" run adaptive thinking at that effort level.
/// </summary>
public sealed class ClaudeChatModel : IChatModel
{
    private const int MaxOutputTokens = 4096;

    private readonly IAnthropicClient _client;
    private readonly string _deploymentName;
    private readonly string _reasoningEffort;

    public ClaudeChatModel(IAnthropicClient client, string deploymentName, string reasoningEffort)
    {
        _client = client;
        _deploymentName = deploymentName;
        _reasoningEffort = reasoningEffort;
    }

    public async IAsyncEnumerable<string> StreamReplyTextAsync(
        string systemPrompt,
        IReadOnlyList<ConversationMessage> conversation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var parameters = CreateParameters(systemPrompt, conversation);

        await foreach (var streamEvent in _client.Messages
                           .CreateStreaming(parameters, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (streamEvent.TryPickContentBlockDelta(out var blockDelta) &&
                blockDelta.Delta.TryPickText(out var textDelta) &&
                !string.IsNullOrEmpty(textDelta.Text))
            {
                yield return textDelta.Text;
            }
        }
    }

    private MessageCreateParams CreateParameters(string systemPrompt, IReadOnlyList<ConversationMessage> conversation)
    {
        var messages = conversation.Select(ToMessageParam).ToList();

        return _reasoningEffort switch
        {
            "none" => new MessageCreateParams
            {
                Model = _deploymentName,
                MaxTokens = MaxOutputTokens,
                System = systemPrompt,
                Messages = messages,
                Thinking = new ThinkingConfigDisabled(),
            },
            "low" or "medium" => new MessageCreateParams
            {
                Model = _deploymentName,
                MaxTokens = MaxOutputTokens,
                System = systemPrompt,
                Messages = messages,
                Thinking = new ThinkingConfigAdaptive(),
                OutputConfig = new OutputConfig { Effort = _reasoningEffort == "low" ? Effort.Low : Effort.Medium },
            },
            _ => throw new InvalidOperationException($"Unsupported reasoning effort: '{_reasoningEffort}'."),
        };
    }

    private static MessageParam ToMessageParam(ConversationMessage message) => message.Role switch
    {
        ConversationRole.User => new UserMessageParam { Content = message.Text },
        ConversationRole.Assistant => new AssistantMessageParam { Content = message.Text },
        _ => throw new InvalidOperationException($"Unknown role: {message.Role}"),
    };
}
