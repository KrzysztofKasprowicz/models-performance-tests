using System.Diagnostics;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

public sealed class AzureOpenAiInvoker : IModelInvoker
{
    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _options;
    private readonly ChatMessage[] _messages;

    public string Deployment { get; }

    public AzureOpenAiInvoker(AzureOpenAiDeployment deployment, AzureOpenAIClient client, string prompt)
    {
        Deployment = deployment.Name;
        _chatClient = client.GetChatClient(deployment.Name);
        _options = new ChatCompletionOptions();
        if (TryGetReasoningEffortLevel(deployment.ReasoningEffort, out var level))
        {
            _options.ReasoningEffortLevel = level;
        }
        _messages = [new UserChatMessage(prompt)];
    }

    public async Task<CallResult> MeasureSingleCallAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        TimeSpan? timeToFirstToken = null;
        var responseLength = 0;

        await foreach (var update in _chatClient
                           .CompleteChatStreamingAsync(_messages, _options, cancellationToken)
                           .ConfigureAwait(false))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (string.IsNullOrEmpty(part.Text))
                {
                    continue;
                }

                timeToFirstToken ??= stopwatch.Elapsed;
                responseLength += part.Text.Length;
            }
        }

        stopwatch.Stop();
        return new CallResult(
            timeToFirstToken ?? stopwatch.Elapsed,
            stopwatch.Elapsed,
            responseLength);
    }

    private static bool TryGetReasoningEffortLevel(string? value, out ChatReasoningEffortLevel level)
    {
        switch (value)
        {
            case "none":
                level = ChatReasoningEffortLevel.None;
                return true;
            case "minimal":
                level = ChatReasoningEffortLevel.Minimal;
                return true;
            case "low":
                level = ChatReasoningEffortLevel.Low;
                return true;
            case "medium":
                level = ChatReasoningEffortLevel.Medium;
                return true;
            case "high":
                level = ChatReasoningEffortLevel.High;
                return true;
            case null:
                level = default;
                return false;
            default:
                throw new InvalidOperationException($"Unknown reasoning effort level: '{value}'.");
        }
    }
}
