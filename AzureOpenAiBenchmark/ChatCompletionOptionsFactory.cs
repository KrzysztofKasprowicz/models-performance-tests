using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

/// <summary>
/// Builds <see cref="ChatCompletionOptions"/> for a benchmark cell: reasoning effort is always
/// set (every cell in the matrix specifies one), and the priority service tier is requested
/// only for "-prio" deployments — see
/// https://learn.microsoft.com/azure/foundry/openai/concepts/priority-processing.
/// </summary>
public static class ChatCompletionOptionsFactory
{
    public static ChatCompletionOptions Create(string reasoningEffort, bool isPriority)
    {
        var options = new ChatCompletionOptions
        {
            ReasoningEffortLevel = ToReasoningEffortLevel(reasoningEffort),
        };

        if (isPriority)
        {
            options.ServiceTier = "priority";
        }

        return options;
    }

    private static ChatReasoningEffortLevel ToReasoningEffortLevel(string value) => value switch
    {
        "none" => ChatReasoningEffortLevel.None,
        "low" => ChatReasoningEffortLevel.Low,
        "medium" => ChatReasoningEffortLevel.Medium,
        _ => throw new InvalidOperationException($"Unsupported reasoning effort: '{value}'."),
    };
}
