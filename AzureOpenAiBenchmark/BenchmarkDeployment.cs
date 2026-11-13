namespace AzureOpenAiBenchmark;

public sealed record BenchmarkDeployment(string Name, ModelApi Api, string[] ReasoningEfforts);
