using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

namespace AzureOpenAiBenchmark;

/// <summary>
/// Konfiguracja dostrojona do pomiaru opóźnień sieciowych (a nie mikrobenchmarków CPU):
/// strategia Monitoring wyłącza fazę pilotażu, więc wykonujemy DOKŁADNIE tyle wywołań,
/// ile zdefiniowano — 1 rozgrzewające + 10 mierzonych = 11 płatnych zapytań do API.
/// </summary>
public sealed class BenchmarkConfig : ManualConfig
{
    public const int WarmupCount = 1;
    public const int MeasuredIterations = 10;

    public BenchmarkConfig()
    {
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddColumn(StatisticColumn.Min, StatisticColumn.Median, StatisticColumn.Max);

        AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default);
        AddExporter(BenchmarkDotNet.Exporters.MarkdownExporter.GitHub);

        AddJob(Job.Default
            .WithStrategy(RunStrategy.Monitoring)
            .WithLaunchCount(1)
            .WithWarmupCount(WarmupCount)
            .WithIterationCount(MeasuredIterations)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));
    }
}
