namespace AzureOpenAiBenchmark;

public abstract record DeploymentConfig(string Name);

/// <summary>
/// Any Foundry deployment reachable via the OpenAI-compatible chat completions
/// endpoint (`/openai/deployments/&lt;name&gt;/chat/completions`). Works for OpenAI
/// models (gpt-*), Microsoft-direct models (gpt-oss-*), and partner models that
/// Foundry exposes through the same surface (DeepSeek, Kimi).
/// </summary>
/// <param name="Name">Deployment name in the Foundry resource.</param>
/// <param name="ReasoningEffort">
/// Optional value for the `reasoning_effort` parameter. Accepted values:
/// <c>"none"</c>, <c>"minimal"</c>, <c>"low"</c>, <c>"medium"</c>, <c>"high"</c>.
/// Null = don't send the parameter (use model default).
/// </param>
public sealed record AzureOpenAiDeployment(string Name, string? ReasoningEffort = null)
    : DeploymentConfig(Name);

public sealed record ClaudeFoundryDeployment(string Name)
    : DeploymentConfig(Name);

public static class BenchmarkConfig
{
    public const int WarmupCallsPerModel = 1;
    public const int Iterations = 10;
    public const int CallsPerIteration = 5;
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(25);

    public static readonly DeploymentConfig[] Deployments =
    [
        new AzureOpenAiDeployment("gpt-5.4", ReasoningEffort: "none"),
        new AzureOpenAiDeployment("gpt-5.4-mini", ReasoningEffort: "none"),
        new AzureOpenAiDeployment("gpt-5.4-nano", ReasoningEffort: "none"),

        // gpt-oss does not accept reasoning_effort=none; "low" is the lowest supported level.
        new AzureOpenAiDeployment("gpt-oss-120b", ReasoningEffort: "low"),

        // Temporarily disabled — uncomment to re-enable.
        // new AzureOpenAiDeployment("gpt-5.1", ReasoningEffort: "none"),
        // new AzureOpenAiDeployment("gpt-4.1"),
        // new AzureOpenAiDeployment("gpt-4.1-mini"),
        // new AzureOpenAiDeployment("gpt-4.1-nano"),

        new AzureOpenAiDeployment("Kimi-K2.6"),
        new AzureOpenAiDeployment("DeepSeek-V4-Flash"),

        new ClaudeFoundryDeployment("claude-sonnet-4-6"),
        new ClaudeFoundryDeployment("claude-haiku-4-5"),
    ];

    public const string Prompt =
        "Reply with exactly four paragraphs of two sentences each. " +
        "Briefly explain: (1) what photosynthesis is, (2) its main stages, " +
        "(3) its importance for life on Earth, (4) how it is applied in agriculture. " +
        "Each sentence must contain between 15 and 20 words. " +
        "Reply in English. Do not use lists, headings, or any Markdown formatting.";
}
