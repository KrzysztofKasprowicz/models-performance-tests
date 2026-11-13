using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using AzureOpenAiBenchmark;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(BenchmarkConfig).Assembly)
    .Build();

var resourceName = configuration["AzureFoundry:ResourceName"]
    ?? throw new InvalidOperationException("Missing user secret 'AzureFoundry:ResourceName'.");

var foundryEndpoint = new Uri($"https://{resourceName}.services.ai.azure.com/");

TokenCredential credential = new AzureCliCredential();

// Force one sequential token acquisition before any concurrent work starts. Without this,
// the first batch of concurrent calls each spawn their own "az account get-access-token"
// subprocess and race on the Azure CLI's on-disk token cache lock file.
await credential.GetTokenAsync(new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), default);

var openAiClient = new AzureOpenAIClient(foundryEndpoint, credential);

var benchmarks = BenchmarkConfig.AllCells()
    .Select(cell => new ModelBenchmark(CreateInvoker(cell)))
    .ToArray();

Console.WriteLine($"Warm-up: {BenchmarkConfig.WarmupCallsPerModel} call(s) per cell...");
await Task.WhenAll(benchmarks.Select(b => b.WarmUpAsync(BenchmarkConfig.WarmupCallsPerModel)));

var totalCalls = benchmarks.Sum(b =>
{
    var (iterations, callsPerIteration) = BenchmarkConfig.VolumeFor(b.Scenario);
    return iterations * callsPerIteration * BenchmarkConfig.TurnsPerCall(b.Scenario);
});

Console.WriteLine(
    $"Starting: {benchmarks.Length} test cells across {BenchmarkConfig.Deployments.Length} deployments. " +
    $"Total {totalCalls} billed requests.");

var completed = 0;
var progressLock = new object();

void OnCallCompleted(ModelBenchmark benchmark, CallResult result)
{
    int current;
    lock (progressLock)
    {
        completed++;
        current = completed;
    }

    var label = $"{benchmark.Deployment,-14} {benchmark.ReasoningEffort,-6} {benchmark.Scenario}";
    var status = result.FailedStatusCode.HasValue
        ? $"FAILED (HTTP {result.FailedStatusCode}) after {result.TotalResponseTime.TotalMilliseconds,8:F1} ms"
        : result.TimedOut
            ? $"TIMEOUT after {result.TotalResponseTime.TotalMilliseconds,8:F1} ms  Length={result.ResponseLength,5} chars (partial)"
            : $"TTFT={result.TimeToFirstToken.TotalMilliseconds,7:F1} ms  " +
              $"Total={result.TotalResponseTime.TotalMilliseconds,8:F1} ms  " +
              $"Length={result.ResponseLength,5} chars";

    Console.WriteLine($"[{current,4}/{totalCalls}] {label,-38} {status}");
}

var stopwatch = System.Diagnostics.Stopwatch.StartNew();

var deploymentStartIndex = new Dictionary<string, int>();

await Task.WhenAll(benchmarks.Select(b =>
{
    var index = deploymentStartIndex.TryGetValue(b.Deployment, out var n) ? n : 0;
    deploymentStartIndex[b.Deployment] = index + 1;
    var stagger = BenchmarkConfig.DeploymentStartStagger * index;

    var (iterations, callsPerIteration) = BenchmarkConfig.VolumeFor(b.Scenario);
    return RunStaggeredAsync(b, stagger, iterations, callsPerIteration);
}));

async Task RunStaggeredAsync(ModelBenchmark benchmark, TimeSpan delay, int iterations, int callsPerIteration)
{
    if (delay > TimeSpan.Zero)
    {
        await Task.Delay(delay);
    }

    await benchmark.RunAsync(iterations, callsPerIteration, OnCallCompleted);
}

stopwatch.Stop();

Console.WriteLine();
Console.WriteLine($"Total wall-clock time: {stopwatch.Elapsed.TotalSeconds:F1} s");

var stats = benchmarks.Select(ModelStatistics.From).ToArray();
StatisticsReporter.PrintConsole(stats);

var outputDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "output");
var pdfPath = PdfReportGenerator.Save(stats, stopwatch.Elapsed, DateTimeOffset.Now, resourceName, foundryEndpoint, outputDirectory);
Console.WriteLine();
Console.WriteLine($"PDF report saved to: {Path.GetFullPath(pdfPath)}");

IModelInvoker CreateInvoker(BenchmarkCell cell) => cell.Scenario switch
{
    BenchmarkScenario.SingleTurn => new SingleTurnInvoker(cell, openAiClient),
    BenchmarkScenario.MultiTurn => new MultiTurnInvoker(cell, openAiClient),
    _ => throw new InvalidOperationException($"Unknown scenario: {cell.Scenario}"),
};
