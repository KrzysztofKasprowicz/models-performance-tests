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
    int SampleCount,
    MetricStats TtftMs,
    MetricStats TotalMs,
    MetricStats LengthChars)
{
    public static ModelStatistics From(ModelBenchmark benchmark) => new(
        benchmark.Deployment,
        benchmark.Results.Count,
        MetricStats.From(benchmark.Results.Select(r => r.TimeToFirstToken.TotalMilliseconds)),
        MetricStats.From(benchmark.Results.Select(r => r.TotalResponseTime.TotalMilliseconds)),
        MetricStats.From(benchmark.Results.Select(r => (double)r.ResponseLength)));
}
