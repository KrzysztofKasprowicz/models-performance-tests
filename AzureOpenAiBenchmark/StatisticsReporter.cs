namespace AzureOpenAiBenchmark;

public static class StatisticsReporter
{
    public static void Print(IEnumerable<ModelBenchmark> benchmarks)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 110));
        Console.WriteLine("Wyniki (czasy w ms)");
        Console.WriteLine(new string('=', 110));
        Console.WriteLine(
            $"{"Model",-15} {"Metric",-8} {"Min",10} {"Avg",10} {"P50",10} {"P75",10} {"P95",10} {"Max",10}");
        Console.WriteLine(new string('-', 110));

        foreach (var benchmark in benchmarks)
        {
            var ttft = benchmark.Results.Select(r => r.TimeToFirstToken.TotalMilliseconds).ToArray();
            var total = benchmark.Results.Select(r => r.TotalResponseTime.TotalMilliseconds).ToArray();
            var lengths = benchmark.Results.Select(r => (double)r.ResponseLength).ToArray();

            PrintRow(benchmark.Deployment, "TTFT", ttft);
            PrintRow(string.Empty, "Total", total);
            Console.WriteLine(
                $"{string.Empty,-15} {"Length",-8} {Min(lengths),10:F0} {Avg(lengths),10:F0} " +
                $"{Percentile(lengths, 0.50),10:F0} {Percentile(lengths, 0.75),10:F0} " +
                $"{Percentile(lengths, 0.95),10:F0} {Max(lengths),10:F0}   (chars)");
            Console.WriteLine($"{string.Empty,-15} samples: {benchmark.Results.Count}");
            Console.WriteLine(new string('-', 110));
        }
    }

    private static void PrintRow(string model, string metric, double[] values)
    {
        Console.WriteLine(
            $"{model,-15} {metric,-8} {Min(values),10:F1} {Avg(values),10:F1} " +
            $"{Percentile(values, 0.50),10:F1} {Percentile(values, 0.75),10:F1} " +
            $"{Percentile(values, 0.95),10:F1} {Max(values),10:F1}");
    }

    private static double Min(double[] values) => values.Min();
    private static double Max(double[] values) => values.Max();
    private static double Avg(double[] values) => values.Average();

    private static double Percentile(double[] values, double percentile)
    {
        if (values.Length == 0)
        {
            return 0;
        }

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
