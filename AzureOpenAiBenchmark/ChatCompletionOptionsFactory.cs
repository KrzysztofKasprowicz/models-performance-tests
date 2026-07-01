using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

/// <summary>
/// Builds <see cref="ChatCompletionOptions"/> for a benchmark cell: reasoning effort is always
/// set (every cell in the matrix specifies one). The priority service tier is NOT set here —
/// "-prio" deployments have it configured at the deployment level in Azure, which is enough on
/// its own per https://learn.microsoft.com/azure/foundry/openai/concepts/priority-processing
/// (deployment-level "priority" + request-level "auto" still routes to priority processing).
/// </summary>
public static class ChatCompletionOptionsFactory
{
    /// <summary>
    /// Caps reasoning + visible output tokens per call. Without this, a single reasoning-heavy
    /// call has no upper bound on hidden reasoning-token spend, which can burn through a
    /// deployment's per-minute token quota and throttle every other concurrent cell sharing
    /// that same deployment — turning a quota problem into noise in the latency numbers.
    ///
    /// Set via the raw JSON patch, not <see cref="ChatCompletionOptions.MaxOutputTokenCount"/>:
    /// that property serializes to the legacy "max_tokens" field, which gpt-5.4/gpt-5.5 reject
    /// outright ("Unsupported parameter: 'max_tokens' ... Use 'max_completion_tokens' instead").
    /// The SDK's automatic max_tokens -> max_completion_tokens remapping only kicks in for
    /// model names it recognizes as reasoning models, and doesn't know about gpt-5.4/gpt-5.5.
    /// </summary>
    private const int MaxOutputTokens = 4096;

    public static ChatCompletionOptions Create(string reasoningEffort)
    {
        var options = new ChatCompletionOptions
        {
            ReasoningEffortLevel = ToReasoningEffortLevel(reasoningEffort),
        };
        options.Patch.Set("$.max_completion_tokens"u8, MaxOutputTokens);

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
