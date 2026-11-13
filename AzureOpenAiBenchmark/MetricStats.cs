namespace AzureOpenAiBenchmark;

public sealed record MetricStats(
    double Min,
    double Avg,
    double P50,
    double P75,
    double P95,
    double Max)
{
    public static MetricStats From(IEnumerable<double> source)
    {
        var values = source as double[] ?? source.ToArray();
        if (values.Length == 0)
        {
            return new MetricStats(0, 0, 0, 0, 0, 0);
        }

        return new MetricStats(
            values.Min(),
            values.Average(),
            Percentile(values, 0.50),
            Percentile(values, 0.75),
            Percentile(values, 0.95),
            values.Max());
    }

    private static double Percentile(double[] values, double percentile)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var position = percentile * (sorted.Length - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        if (lower == upper)
        {
            return sorted[lower];
        }

        var weight = position - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }
}

public sealed record ModelStatistics(
    string Deployment,
    string ReasoningEffort,
    BenchmarkScenario Scenario,
    bool IsPriority,
    int SampleCount,
    int TimedOutCount,
    int FailedCount,
    MetricStats TtftMs,
    MetricStats TotalMs,
    MetricStats LengthChars)
{
    public bool HasSuccessfulSamples => FailedCount < SampleCount;

    public static ModelStatistics From(ModelBenchmark benchmark)
    {
        var answered = benchmark.Results.Where(r => !r.FailedStatusCode.HasValue).ToArray();

        return new(
            benchmark.Deployment,
            benchmark.ReasoningEffort,
            benchmark.Scenario,
            benchmark.IsPriority,
            benchmark.Results.Count,
            benchmark.Results.Count(r => r.TimedOut),
            benchmark.Results.Count - answered.Length,
            MetricStats.From(answered.Select(r => r.TimeToFirstToken.TotalMilliseconds)),
            MetricStats.From(answered.Select(r => r.TotalResponseTime.TotalMilliseconds)),
            MetricStats.From(answered.Select(r => (double)r.ResponseLength)));
    }
}
