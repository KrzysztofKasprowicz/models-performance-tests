namespace AzureOpenAiBenchmark;

public static class StatisticsReporter
{
    public static void PrintConsole(IEnumerable<ModelStatistics> stats)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 110));
        Console.WriteLine("Results (times in ms)");
        Console.WriteLine(new string('=', 110));
        Console.WriteLine(
            $"{"Model",-15} {"Metric",-8} {"Min",10} {"Avg",10} {"P50",10} {"P75",10} {"P95",10} {"Max",10}");
        Console.WriteLine(new string('-', 110));

        foreach (var s in stats)
        {
            PrintRow(s.Deployment, "TTFT", s.TtftMs, "F1");
            PrintRow(string.Empty, "Total", s.TotalMs, "F1");
            PrintRow(string.Empty, "Length", s.LengthChars, "F0", "   (chars)");
            Console.WriteLine($"{string.Empty,-15} samples: {s.SampleCount}");
            Console.WriteLine(new string('-', 110));
        }
    }

    private static void PrintRow(string model, string metric, MetricStats m, string format, string suffix = "")
    {
        Console.WriteLine(
            $"{model,-15} {metric,-8} {m.Min.ToString(format),10} {m.Avg.ToString(format),10} " +
            $"{m.P50.ToString(format),10} {m.P75.ToString(format),10} " +
            $"{m.P95.ToString(format),10} {m.Max.ToString(format),10}{suffix}");
    }
}
