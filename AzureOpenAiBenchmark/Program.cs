using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using AzureOpenAiBenchmark;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(ModelBenchmark).Assembly)
    .Build();

var resourceName = configuration["AzureFoundry:ResourceName"]
    ?? throw new InvalidOperationException("Missing user secret 'AzureFoundry:ResourceName'.");

var azureOpenAiEndpoint = new Uri($"https://{resourceName}.openai.azure.com/");
var foundryEndpoint = new Uri($"https://{resourceName}.services.ai.azure.com/");
var foundryInferenceEndpoint = new Uri(foundryEndpoint, "models");

TokenCredential credential = new AzureCliCredential();
var azureOpenAiClient = new AzureOpenAIClient(azureOpenAiEndpoint, credential);
var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

var benchmarks = BenchmarkConfig.Deployments
    .Select(d => new ModelBenchmark(CreateInvoker(d)))
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
        $"[{current,3}/{totalCalls}] {deployment,-22} " +
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

IModelInvoker CreateInvoker(DeploymentConfig deployment) => deployment switch
{
    AzureOpenAiDeployment aoai => new AzureOpenAiInvoker(aoai, azureOpenAiClient, BenchmarkConfig.Prompt),
    FoundryInferenceDeployment fi => new FoundryInferenceInvoker(fi, foundryInferenceEndpoint, credential, BenchmarkConfig.Prompt),
    ClaudeFoundryDeployment claude => new ClaudeFoundryInvoker(claude, foundryEndpoint, credential, BenchmarkConfig.Prompt, httpClient),
    _ => throw new InvalidOperationException($"Unknown deployment type: {deployment.GetType().Name}"),
};
