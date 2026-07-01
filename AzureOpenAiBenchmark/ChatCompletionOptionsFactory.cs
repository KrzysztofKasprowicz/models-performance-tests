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
    /// <summary>
    /// Caps reasoning + visible output tokens per call. Without this, a single reasoning-heavy
    /// call has no upper bound on hidden reasoning-token spend, which can burn through a
    /// deployment's per-minute token quota and throttle every other concurrent cell sharing
    /// that same deployment — turning a quota problem into noise in the latency numbers.
    /// </summary>
    private const int MaxOutputTokens = 4096;

    public static ChatCompletionOptions Create(string reasoningEffort, bool isPriority)
    {
        var options = new ChatCompletionOptions
        {
            ReasoningEffortLevel = ToReasoningEffortLevel(reasoningEffort),
            MaxOutputTokenCount = MaxOutputTokens,
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
