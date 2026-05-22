using Azure.AI.OpenAI;
using Azure.Identity;
using AzureOpenAiBenchmark;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(ModelBenchmark).Assembly)
    .Build();

var endpoint = configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("Brak sekretu 'AzureOpenAI:Endpoint'.");

var openAiClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential());

var benchmarks = BenchmarkConfig.Deployments
    .Select(deployment => new ModelBenchmark(deployment, openAiClient.GetChatClient(deployment)))
    .ToArray();

Console.WriteLine($"Rozgrzewka: {BenchmarkConfig.WarmupCallsPerModel} wywołań na model...");
await Task.WhenAll(benchmarks.Select(b => b.WarmUpAsync(BenchmarkConfig.WarmupCallsPerModel)));

var totalCalls = benchmarks.Length * BenchmarkConfig.Iterations * BenchmarkConfig.CallsPerIteration;
Console.WriteLine(
    $"Start: {benchmarks.Length} modele równolegle, " +
    $"{BenchmarkConfig.Iterations} iteracji x {BenchmarkConfig.CallsPerIteration} wywołań na iterację. " +
    $"Łącznie {totalCalls} płatnych zapytań.");

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
Console.WriteLine($"Łączny czas testu (wall-clock): {stopwatch.Elapsed.TotalSeconds:F1} s");

StatisticsReporter.Print(benchmarks);
