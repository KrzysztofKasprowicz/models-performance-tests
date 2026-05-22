namespace AzureOpenAiBenchmark;

public sealed record CallResult(
    TimeSpan TimeToFirstToken,
    TimeSpan TotalResponseTime,
    int ResponseLength,
    bool TimedOut = false);
