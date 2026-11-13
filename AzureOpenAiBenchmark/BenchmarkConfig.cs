namespace AzureOpenAiBenchmark;

public static class BenchmarkConfig
{
    public const int WarmupCallsPerModel = 1;
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(25);

    /// <summary>
    /// Delay between starting successive test cells that share the same underlying deployment.
    /// Without this, all cells for one deployment (reasoning efforts x scenarios) fire
    /// their first calls in the same instant, competing for that one deployment's per-minute
    /// token quota right out of the gate.
    /// </summary>
    public static readonly TimeSpan DeploymentStartStagger = TimeSpan.FromMilliseconds(500);

    private static readonly string[] AllReasoningEfforts = ["none", "low", "medium"];

    public static readonly BenchmarkDeployment[] Deployments =
    [
        new("gpt-5.4-mini", ModelApi.AzureOpenAiChatCompletions, AllReasoningEfforts),
        new("gpt-6-luna", ModelApi.AzureOpenAiChatCompletions, AllReasoningEfforts),
        new("gpt-6.1-terra", ModelApi.AzureOpenAiChatCompletions, AllReasoningEfforts),
        // gpt-oss does not accept reasoning_effort=none.
        new("gpt-oss-120b", ModelApi.AzureOpenAiChatCompletions, ["low", "medium"]),
        new("claude-haiku-5-5", ModelApi.AnthropicMessages, AllReasoningEfforts),
    ];

    public const int SingleTurnIterations = 12;
    public const int SingleTurnCallsPerIteration = 2;

    public const int MultiTurnIterations = 8;
    public const int MultiTurnCallsPerIteration = 1;

    public static (int Iterations, int CallsPerIteration) VolumeFor(BenchmarkScenario scenario) => scenario switch
    {
        BenchmarkScenario.SingleTurn => (SingleTurnIterations, SingleTurnCallsPerIteration),
        BenchmarkScenario.MultiTurn => (MultiTurnIterations, MultiTurnCallsPerIteration),
        _ => throw new InvalidOperationException($"Unknown scenario: {scenario}"),
    };

    public static int TurnsPerCall(BenchmarkScenario scenario) => scenario switch
    {
        BenchmarkScenario.SingleTurn => 1,
        BenchmarkScenario.MultiTurn => Prompts.MultiTurnUserMessages.Length,
        _ => throw new InvalidOperationException($"Unknown scenario: {scenario}"),
    };

    public static IEnumerable<BenchmarkCell> AllCells() =>
        from deployment in Deployments
        from reasoningEffort in deployment.ReasoningEfforts
        from scenario in Enum.GetValues<BenchmarkScenario>()
        select new BenchmarkCell(deployment.Name, deployment.Api, reasoningEffort, scenario);
}
