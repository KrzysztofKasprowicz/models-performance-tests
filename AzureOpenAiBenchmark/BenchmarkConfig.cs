namespace AzureOpenAiBenchmark;

public static class BenchmarkConfig
{
    public const int WarmupCallsPerModel = 1;
    public const int Iterations = 10;
    public const int CallsPerIteration = 5;

    public static readonly string[] Deployments =
    [
        "gpt-5.4",
        "gpt-5.4-mini",
        "gpt-5.4-nano",
    ];

    public const string Prompt =
        "Reply with exactly four paragraphs of two sentences each. " +
        "Briefly explain: (1) what photosynthesis is, (2) its main stages, " +
        "(3) its importance for life on Earth, (4) how it is applied in agriculture. " +
        "Each sentence must contain between 15 and 20 words. " +
        "Reply in English. Do not use lists, headings, or any Markdown formatting.";
}
