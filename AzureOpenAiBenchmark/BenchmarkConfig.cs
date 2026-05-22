namespace AzureOpenAiBenchmark;

public abstract record DeploymentConfig(string Name);

public sealed record AzureOpenAiDeployment(string Name, bool IsReasoningModel)
    : DeploymentConfig(Name);

public sealed record FoundryInferenceDeployment(string Name)
    : DeploymentConfig(Name);

public sealed record ClaudeFoundryDeployment(string Name)
    : DeploymentConfig(Name);

public static class BenchmarkConfig
{
    public const int WarmupCallsPerModel = 1;
    public const int Iterations = 10;
    public const int CallsPerIteration = 5;

    public static readonly DeploymentConfig[] Deployments =
    [
        new AzureOpenAiDeployment("gpt-5.4", IsReasoningModel: true),
        new AzureOpenAiDeployment("gpt-5.4-mini", IsReasoningModel: true),
        new AzureOpenAiDeployment("gpt-5.4-nano", IsReasoningModel: true),

        // Temporarily disabled — uncomment to re-enable.
        // new AzureOpenAiDeployment("gpt-5.1", IsReasoningModel: true),
        // new AzureOpenAiDeployment("gpt-4.1", IsReasoningModel: false),
        // new AzureOpenAiDeployment("gpt-4.1-mini", IsReasoningModel: false),
        // new AzureOpenAiDeployment("gpt-4.1-nano", IsReasoningModel: false),

        new FoundryInferenceDeployment("Kimi-K2.6"),
        new FoundryInferenceDeployment("DeepSeek-V4-Flash"),
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
