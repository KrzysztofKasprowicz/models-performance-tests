namespace AzureOpenAiBenchmark;

public interface IModelInvoker
{
    string Deployment { get; }
    string ReasoningEffort { get; }
    BenchmarkScenario Scenario { get; }
    bool IsPriority { get; }

    Task<IReadOnlyList<CallResult>> MeasureAsync(CancellationToken cancellationToken);
}
