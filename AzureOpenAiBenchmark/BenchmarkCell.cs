namespace AzureOpenAiBenchmark;

/// <summary>
/// One cell of the test matrix: a deployment, the reasoning effort to request, and the
/// conversation shape to run. <see cref="IsPriority"/> is derived from the deployment name —
/// any deployment ending in "-prio" automatically requests the priority service tier.
/// </summary>
public sealed record BenchmarkCell(string DeploymentName, ModelApi Api, string ReasoningEffort, BenchmarkScenario Scenario)
{
    public bool IsPriority => DeploymentName.EndsWith("-prio", StringComparison.Ordinal);
}
