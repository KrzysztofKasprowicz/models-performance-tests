namespace AzureOpenAiBenchmark;

public static class BenchmarkConfig
{
    public const int WarmupCallsPerModel = 1;
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(25);

    public static readonly string[] DeploymentNames =
    [
        "gpt-5.4",
        "gpt-5.5",
        "gpt-5.4-prio",
        "gpt-5.5-prio",
    ];

    public static readonly string[] ReasoningEfforts = ["none", "low", "medium"];

    public const int SingleTurnIterations = 8;
    public const int SingleTurnCallsPerIteration = 2;

    public const int MultiTurnIterations = 5;
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
        from deploymentName in DeploymentNames
        from reasoningEffort in ReasoningEfforts
        from scenario in Enum.GetValues<BenchmarkScenario>()
        select new BenchmarkCell(deploymentName, reasoningEffort, scenario);
}
