using Azure.AI.OpenAI;
using Azure.Identity;
using AzureOpenAiBenchmark;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(ModelBenchmark).Assembly)
    .Build();

var endpoint = configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("Missing user secret 'AzureOpenAI:Endpoint'.");

var openAiClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential());

var benchmarks = BenchmarkConfig.Deployments
    .Select(deployment => new ModelBenchmark(deployment, openAiClient.GetChatClient(deployment.Name)))
    .ToArray();

Console.WriteLine($"Warm-up: {BenchmarkConfig.WarmupCallsPerModel} call(s) per model...");
await Task.WhenAll(benchmarks.Select(b => b.WarmUpAsync(BenchmarkConfig.WarmupCallsPerModel)));

var totalCalls = benchmarks.Length * BenchmarkConfig.Iterations * BenchmarkConfig.CallsPerIteration;
Console.WriteLine(
    $"Starting: {benchmarks.Length} models in parallel, " +
    $"{BenchmarkConfig.Iterations} iterations x {BenchmarkConfig.CallsPerIteration} calls per iteration. " +
    $"Total {totalCalls} billed requests.");

var completed = 0;
var progressLock = new object();

void OnCallCompleted(string deployment, CallResult result)
{
    int current;
    lock (progressLock)
    {
        completed++;
        current = completed;
    }

    Console.WriteLine(
        $"[{current,3}/{totalCalls}] {deployment,-15} " +
        $"TTFT={result.TimeToFirstToken.TotalMilliseconds,7:F1} ms  " +
        $"Total={result.TotalResponseTime.TotalMilliseconds,8:F1} ms  " +
        $"Length={result.ResponseLength,5} chars");
}

var stopwatch = System.Diagnostics.Stopwatch.StartNew();

await Task.WhenAll(benchmarks.Select(b => b.RunAsync(
    BenchmarkConfig.Iterations,
    BenchmarkConfig.CallsPerIteration,
    OnCallCompleted)));

stopwatch.Stop();

Console.WriteLine();
Console.WriteLine($"Total wall-clock time: {stopwatch.Elapsed.TotalSeconds:F1} s");

var stats = benchmarks.Select(ModelStatistics.From).ToArray();
StatisticsReporter.PrintConsole(stats);

var outputDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "output");
var pdfPath = PdfReportGenerator.Save(stats, stopwatch.Elapsed, DateTimeOffset.Now, outputDirectory);
Console.WriteLine();
Console.WriteLine($"PDF report saved to: {Path.GetFullPath(pdfPath)}");
