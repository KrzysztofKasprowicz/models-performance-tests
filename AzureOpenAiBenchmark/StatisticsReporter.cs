namespace AzureOpenAiBenchmark;

public static class StatisticsReporter
{
    public static void PrintConsole(IEnumerable<ModelStatistics> stats)
    {
        foreach (var group in stats.GroupBy(s => s.Scenario).OrderBy(g => g.Key))
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 110));
            Console.WriteLine($"Results — {group.Key} (times in ms)");
            Console.WriteLine(new string('=', 110));
            Console.WriteLine(
                $"{"Model",-18} {"Effort",-7} {"Metric",-8} {"Min",10} {"Avg",10} {"P50",10} {"P75",10} {"P95",10} {"Max",10}");
            Console.WriteLine(new string('-', 110));

            foreach (var s in group.OrderBy(s => s.Deployment).ThenBy(s => s.ReasoningEffort))
            {
                PrintRow(s.Deployment, s.ReasoningEffort, "TTFT", s.TtftMs, "F1");
                PrintRow(string.Empty, string.Empty, "Total", s.TotalMs, "F1");
                PrintRow(string.Empty, string.Empty, "Length", s.LengthChars, "F0", "   (chars)");
                var timeoutNote = s.TimedOutCount > 0 ? $", timeouts: {s.TimedOutCount} (>{BenchmarkConfig.CallTimeout.TotalSeconds:F0}s)" : string.Empty;
                Console.WriteLine($"{string.Empty,-18} {string.Empty,-7} samples: {s.SampleCount}{timeoutNote}");
                Console.WriteLine(new string('-', 110));
            }
        }
    }

    private static void PrintRow(string model, string effort, string metric, MetricStats m, string format, string suffix = "")
    {
        Console.WriteLine(
            $"{model,-18} {effort,-7} {metric,-8} {m.Min.ToString(format),10} {m.Avg.ToString(format),10} " +
            $"{m.P50.ToString(format),10} {m.P75.ToString(format),10} " +
            $"{m.P95.ToString(format),10} {m.Max.ToString(format),10}{suffix}");
    }
}
