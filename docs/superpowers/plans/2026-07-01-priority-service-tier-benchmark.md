# Priority Service Tier Benchmark Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the multi-vendor benchmark with a 24-cell comparison of Azure OpenAI's `priority` vs. standard service tier across `gpt-5.4`/`gpt-5.5` (+ `-prio` variants), `reasoning_effort` (none/low/medium), and conversation shape (single-turn / fixed 3-turn).

**Architecture:** Introduce a `BenchmarkCell` (deployment × reasoning effort × scenario) as the unit of work. Two thin `IModelInvoker` implementations (`SingleTurnInvoker`, `MultiTurnInvoker`) share one streaming-measurement helper (`ChatCallExecutor`) and one options builder (`ChatCompletionOptionsFactory`). `ModelBenchmark`/`MetricStats`/reporting are extended to carry and group by reasoning effort and scenario instead of just a model name.

**Tech Stack:** .NET 10, `Azure.AI.OpenAI` 2.9.0-beta.1 (`OpenAI` 2.9.1 transitively), `Azure.Identity` (`AzureCliCredential`), QuestPDF 2026.5.0.

No automated tests are added in this plan (per project convention: don't add tests unless asked) — verification is "does it build" plus a manual smoke run against the real Foundry resource, covered in the final task.

Reference spec: `docs/superpowers/specs/2026-07-01-priority-service-tier-benchmark-design.md`

---

### Task 1: Core matrix types (scenario, cell, prompts)

**Files:**
- Create: `AzureOpenAiBenchmark/BenchmarkScenario.cs`
- Create: `AzureOpenAiBenchmark/BenchmarkCell.cs`
- Create: `AzureOpenAiBenchmark/Prompts.cs`

These are pure additions — nothing in the project references them yet, so the build stays green after this task.

- [ ] **Step 1: Create `AzureOpenAiBenchmark/BenchmarkScenario.cs`**

```csharp
namespace AzureOpenAiBenchmark;

public enum BenchmarkScenario
{
    SingleTurn,
    MultiTurn,
}
```

- [ ] **Step 2: Create `AzureOpenAiBenchmark/BenchmarkCell.cs`**

```csharp
namespace AzureOpenAiBenchmark;

/// <summary>
/// One cell of the test matrix: a deployment, the reasoning effort to request, and the
/// conversation shape to run. <see cref="IsPriority"/> is derived from the deployment name —
/// any deployment ending in "-prio" automatically requests the priority service tier.
/// </summary>
public sealed record BenchmarkCell(string DeploymentName, string ReasoningEffort, BenchmarkScenario Scenario)
{
    public bool IsPriority => DeploymentName.EndsWith("-prio", StringComparison.Ordinal);
}
```

- [ ] **Step 3: Create `AzureOpenAiBenchmark/Prompts.cs`**

```csharp
namespace AzureOpenAiBenchmark;

public static class Prompts
{
    public const string SystemPrompt =
        "You are a precise, efficient assistant. Answer the user's latest message directly, in " +
        "plain English prose only — no Markdown, no lists, no headings. Stay between 60 and 100 " +
        "words regardless of how complex the question is.";

    public const string SingleTurnUserPrompt =
        "A warehouse network has three sites. Site A starts with 420 units and gains 35 units per " +
        "day. Site B starts with 290 units and gains 52 units per day. Site C starts with 610 units " +
        "but loses 18 units per day to spoilage. After how many full days does Site B first hold " +
        "more units than Site C, and which site holds the most units at that point? Give the day " +
        "count, the site, and a one-sentence justification.";

    /// <summary>Fixed user turns for the multi-turn scenario, sent in order.</summary>
    public static readonly string[] MultiTurnUserMessages =
    [
        "I'm planning a 3-day trip to Kraków in October for two adults. Suggest a rough day-by-day " +
        "plan covering the Old Town, Wieliczka Salt Mine, and one evening with local food.",

        "Our budget is tighter than I expected — about 150 EUR per person for the whole trip " +
        "excluding flights and hotel. Adjust the plan to cut costs without dropping the salt mine.",

        "One of us is vegetarian and avoids gluten. Update the food recommendations across all " +
        "three days to fit that, and give a short final summary of the full itinerary.",
    ];

    /// <summary>
    /// Fixed, canned "assistant" replies injected into history between user turns so every test
    /// cell sees byte-identical conversation context. These are NOT the model's real output —
    /// the real streamed response is what gets measured, this text only shapes what the next
    /// turn sees as history. One fewer entry than <see cref="MultiTurnUserMessages"/>: there is
    /// no placeholder needed after the last turn.
    /// </summary>
    public static readonly string[] MultiTurnFixedAssistantReplies =
    [
        "Day 1: Old Town walking tour covering the Main Square, Cloth Hall, and St. Mary's " +
        "Basilica, followed by dinner at a milk bar. Day 2: Half-day guided tour of the Wieliczka " +
        "Salt Mine, then a free afternoon in Kazimierz. Day 3: Schindler's Factory museum in the " +
        "morning, then a farewell dinner of traditional pierogi and żurek near the Vistula.",

        "Swap the guided Wieliczka tour for the cheaper self-guided entry ticket, replace the " +
        "milk-bar dinner with a grocery-store picnic in Planty Park, and use the Kraków city card " +
        "for free public transport and discounted museum entry on day 3.",
    ];
}
```

- [ ] **Step 4: Build to confirm nothing broke**

Run: `dotnet build` (from `AzureOpenAiBenchmark/` or pass `--project AzureOpenAiBenchmark`)
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add AzureOpenAiBenchmark/BenchmarkScenario.cs AzureOpenAiBenchmark/BenchmarkCell.cs AzureOpenAiBenchmark/Prompts.cs
git commit -m "Add benchmark matrix types and scripted prompts"
```

---

### Task 2: Shared call infrastructure

**Files:**
- Create: `AzureOpenAiBenchmark/ChatCompletionOptionsFactory.cs`
- Create: `AzureOpenAiBenchmark/ChatCallExecutor.cs`

Still pure additions — `AzureOpenAiInvoker.cs` keeps its own copy of this logic until Task 3 deletes it, so the build stays green.

- [ ] **Step 1: Create `AzureOpenAiBenchmark/ChatCompletionOptionsFactory.cs`**

```csharp
using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

/// <summary>
/// Builds <see cref="ChatCompletionOptions"/> for a benchmark cell: reasoning effort is always
/// set (every cell in the matrix specifies one), and the priority service tier is requested
/// only for "-prio" deployments — see
/// https://learn.microsoft.com/azure/foundry/openai/concepts/priority-processing.
/// </summary>
public static class ChatCompletionOptionsFactory
{
    public static ChatCompletionOptions Create(string reasoningEffort, bool isPriority)
    {
        var options = new ChatCompletionOptions
        {
            ReasoningEffortLevel = ToReasoningEffortLevel(reasoningEffort),
        };

        if (isPriority)
        {
            options.ServiceTier = "priority";
        }

        return options;
    }

    private static ChatReasoningEffortLevel ToReasoningEffortLevel(string value) => value switch
    {
        "none" => ChatReasoningEffortLevel.None,
        "low" => ChatReasoningEffortLevel.Low,
        "medium" => ChatReasoningEffortLevel.Medium,
        _ => throw new InvalidOperationException($"Unsupported reasoning effort: '{value}'."),
    };
}
```

- [ ] **Step 2: Create `AzureOpenAiBenchmark/ChatCallExecutor.cs`**

```csharp
using System.Diagnostics;
using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

/// <summary>
/// Streams one chat completion call and measures time-to-first-token / total time / response
/// length. Shared by both single-turn and multi-turn invokers — the only difference between
/// them is what message list they pass in.
/// </summary>
public static class ChatCallExecutor
{
    public static async Task<CallResult> ExecuteAsync(
        ChatClient chatClient,
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions options,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(BenchmarkConfig.CallTimeout);

        var stopwatch = Stopwatch.StartNew();
        TimeSpan? timeToFirstToken = null;
        var responseLength = 0;
        var timedOut = false;

        try
        {
            await foreach (var update in chatClient
                               .CompleteChatStreamingAsync(messages, options, timeoutCts.Token)
                               .ConfigureAwait(false))
            {
                foreach (var part in update.ContentUpdate)
                {
                    if (string.IsNullOrEmpty(part.Text))
                    {
                        continue;
                    }

                    timeToFirstToken ??= stopwatch.Elapsed;
                    responseLength += part.Text.Length;
                }
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
        }

        stopwatch.Stop();
        return new CallResult(
            timeToFirstToken ?? stopwatch.Elapsed,
            stopwatch.Elapsed,
            responseLength,
            timedOut);
    }
}
```

- [ ] **Step 3: Build to confirm nothing broke**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add AzureOpenAiBenchmark/ChatCompletionOptionsFactory.cs AzureOpenAiBenchmark/ChatCallExecutor.cs
git commit -m "Add shared chat-options factory and streaming call executor"
```

---

### Task 3: Replace invokers

**Files:**
- Modify: `AzureOpenAiBenchmark/IModelInvoker.cs`
- Delete: `AzureOpenAiBenchmark/AzureOpenAiInvoker.cs`
- Delete: `AzureOpenAiBenchmark/ClaudeFoundryInvoker.cs`
- Create: `AzureOpenAiBenchmark/SingleTurnInvoker.cs`
- Create: `AzureOpenAiBenchmark/MultiTurnInvoker.cs`

This task intentionally leaves the build **red**: `BenchmarkConfig.cs` and `Program.cs` still reference the deleted types (`AzureOpenAiDeployment`, `ClaudeFoundryDeployment`, `AzureOpenAiInvoker`, `ClaudeFoundryInvoker`). That's expected — they get rewired in Task 6. Don't try to build after this task.

- [ ] **Step 1: Rewrite `AzureOpenAiBenchmark/IModelInvoker.cs`**

```csharp
namespace AzureOpenAiBenchmark;

public interface IModelInvoker
{
    string Deployment { get; }
    string ReasoningEffort { get; }
    BenchmarkScenario Scenario { get; }
    bool IsPriority { get; }

    Task<IReadOnlyList<CallResult>> MeasureAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Delete the old invokers**

```bash
git rm AzureOpenAiBenchmark/AzureOpenAiInvoker.cs AzureOpenAiBenchmark/ClaudeFoundryInvoker.cs
```

- [ ] **Step 3: Create `AzureOpenAiBenchmark/SingleTurnInvoker.cs`**

```csharp
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

public sealed class SingleTurnInvoker : IModelInvoker
{
    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _options;
    private readonly ChatMessage[] _messages;

    public string Deployment { get; }
    public string ReasoningEffort { get; }
    public BenchmarkScenario Scenario => BenchmarkScenario.SingleTurn;
    public bool IsPriority { get; }

    public SingleTurnInvoker(BenchmarkCell cell, AzureOpenAIClient client)
    {
        Deployment = cell.DeploymentName;
        ReasoningEffort = cell.ReasoningEffort;
        IsPriority = cell.IsPriority;
        _chatClient = client.GetChatClient(cell.DeploymentName);
        _options = ChatCompletionOptionsFactory.Create(cell.ReasoningEffort, cell.IsPriority);
        _messages =
        [
            new SystemChatMessage(Prompts.SystemPrompt),
            new UserChatMessage(Prompts.SingleTurnUserPrompt),
        ];
    }

    public async Task<IReadOnlyList<CallResult>> MeasureAsync(CancellationToken cancellationToken)
    {
        var result = await ChatCallExecutor.ExecuteAsync(_chatClient, _messages, _options, cancellationToken)
            .ConfigureAwait(false);
        return [result];
    }
}
```

- [ ] **Step 4: Create `AzureOpenAiBenchmark/MultiTurnInvoker.cs`**

```csharp
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

/// <summary>
/// Runs the fixed 3-turn script. Each turn's real streamed response is measured, but the
/// conversation history fed to the next turn uses <see cref="Prompts.MultiTurnFixedAssistantReplies"/>
/// instead of what the model actually said, so every test cell sees identical context.
/// </summary>
public sealed class MultiTurnInvoker : IModelInvoker
{
    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _options;

    public string Deployment { get; }
    public string ReasoningEffort { get; }
    public BenchmarkScenario Scenario => BenchmarkScenario.MultiTurn;
    public bool IsPriority { get; }

    public MultiTurnInvoker(BenchmarkCell cell, AzureOpenAIClient client)
    {
        Deployment = cell.DeploymentName;
        ReasoningEffort = cell.ReasoningEffort;
        IsPriority = cell.IsPriority;
        _chatClient = client.GetChatClient(cell.DeploymentName);
        _options = ChatCompletionOptionsFactory.Create(cell.ReasoningEffort, cell.IsPriority);
    }

    public async Task<IReadOnlyList<CallResult>> MeasureAsync(CancellationToken cancellationToken)
    {
        var history = new List<ChatMessage> { new SystemChatMessage(Prompts.SystemPrompt) };
        var results = new List<CallResult>(Prompts.MultiTurnUserMessages.Length);

        for (var turn = 0; turn < Prompts.MultiTurnUserMessages.Length; turn++)
        {
            history.Add(new UserChatMessage(Prompts.MultiTurnUserMessages[turn]));

            var result = await ChatCallExecutor.ExecuteAsync(_chatClient, history, _options, cancellationToken)
                .ConfigureAwait(false);
            results.Add(result);

            if (turn < Prompts.MultiTurnFixedAssistantReplies.Length)
            {
                history.Add(new AssistantChatMessage(Prompts.MultiTurnFixedAssistantReplies[turn]));
            }
        }

        return results;
    }
}
```

- [ ] **Step 5: Commit (build is expected red here — that's fine)**

```bash
git add AzureOpenAiBenchmark/IModelInvoker.cs AzureOpenAiBenchmark/SingleTurnInvoker.cs AzureOpenAiBenchmark/MultiTurnInvoker.cs
git commit -m "Replace vendor invokers with single/multi-turn scenario invokers"
```

---

### Task 4: `ModelBenchmark` and statistics

**Files:**
- Modify: `AzureOpenAiBenchmark/ModelBenchmark.cs`
- Modify: `AzureOpenAiBenchmark/MetricStats.cs`

Still red after this task (Program.cs/BenchmarkConfig.cs untouched) — that's expected, keep going.

- [ ] **Step 1: Rewrite `AzureOpenAiBenchmark/ModelBenchmark.cs`**

```csharp
namespace AzureOpenAiBenchmark;

public sealed class ModelBenchmark
{
    private readonly IModelInvoker _invoker;
    private readonly List<CallResult> _results = new();
    private readonly object _resultsLock = new();

    public string Deployment => _invoker.Deployment;
    public string ReasoningEffort => _invoker.ReasoningEffort;
    public BenchmarkScenario Scenario => _invoker.Scenario;
    public bool IsPriority => _invoker.IsPriority;
    public IReadOnlyList<CallResult> Results => _results;

    public ModelBenchmark(IModelInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task WarmUpAsync(int calls, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < calls; i++)
        {
            _ = await _invoker.MeasureAsync(cancellationToken);
        }
    }

    public async Task RunAsync(
        int iterations,
        int callsPerIteration,
        Action<ModelBenchmark, CallResult> onCallCompleted,
        CancellationToken cancellationToken = default)
    {
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var tasks = Enumerable.Range(0, callsPerIteration)
                .Select(async _ =>
                {
                    var callResults = await _invoker.MeasureAsync(cancellationToken).ConfigureAwait(false);
                    foreach (var result in callResults)
                    {
                        lock (_resultsLock)
                        {
                            _results.Add(result);
                        }
                        onCallCompleted(this, result);
                    }
                })
                .ToArray();

            await Task.WhenAll(tasks);
        }
    }
}
```

- [ ] **Step 2: Rewrite `AzureOpenAiBenchmark/MetricStats.cs`**

```csharp
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
    MetricStats TtftMs,
    MetricStats TotalMs,
    MetricStats LengthChars)
{
    public static ModelStatistics From(ModelBenchmark benchmark) => new(
        benchmark.Deployment,
        benchmark.ReasoningEffort,
        benchmark.Scenario,
        benchmark.IsPriority,
        benchmark.Results.Count,
        benchmark.Results.Count(r => r.TimedOut),
        MetricStats.From(benchmark.Results.Select(r => r.TimeToFirstToken.TotalMilliseconds)),
        MetricStats.From(benchmark.Results.Select(r => r.TotalResponseTime.TotalMilliseconds)),
        MetricStats.From(benchmark.Results.Select(r => (double)r.ResponseLength)));
}
```

- [ ] **Step 3: Commit (still red, expected)**

```bash
git add AzureOpenAiBenchmark/ModelBenchmark.cs AzureOpenAiBenchmark/MetricStats.cs
git commit -m "Carry reasoning effort, scenario, and priority flag through benchmark stats"
```

---

### Task 5: Reporting (console + PDF), sectioned by scenario

**Files:**
- Modify: `AzureOpenAiBenchmark/StatisticsReporter.cs`
- Modify: `AzureOpenAiBenchmark/PdfReportGenerator.cs`

Still red after this task — `BenchmarkConfig.cs` (referenced here for `SingleTurnIterations` etc.) and `Program.cs` aren't rewired yet. Expected, keep going.

- [ ] **Step 1: Rewrite `AzureOpenAiBenchmark/StatisticsReporter.cs`**

```csharp
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
```

- [ ] **Step 2: Rewrite `AzureOpenAiBenchmark/PdfReportGenerator.cs`**

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AzureOpenAiBenchmark;

public static class PdfReportGenerator
{
    private static readonly string PrimaryColor = Colors.Blue.Darken3;
    private static readonly string AccentColor = Colors.Blue.Lighten4;
    private static readonly string HeaderRowColor = Colors.Grey.Lighten3;
    private static readonly string BorderColor = Colors.Grey.Medium;

    public static string Save(
        IReadOnlyList<ModelStatistics> stats,
        TimeSpan wallClock,
        DateTimeOffset generatedAt,
        string resourceName,
        Uri endpoint,
        string outputDirectory)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        Directory.CreateDirectory(outputDirectory);
        var fileName = $"benchmark-{generatedAt.LocalDateTime:yyyyMMdd-HHmmss}.pdf";
        var path = Path.Combine(outputDirectory, fileName);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Helvetica"));

                page.Header().Column(col =>
                {
                    col.Item().Text("Azure OpenAI Priority Service Tier Benchmark")
                        .FontSize(20).Bold().FontColor(PrimaryColor);
                    col.Item().Text(
                            $"Generated {generatedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss} " +
                            $"(UTC offset {generatedAt.Offset:hh\\:mm})")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(6).LineHorizontal(2).LineColor(PrimaryColor);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(20);
                    BuildSummary(col.Item(), stats, wallClock, resourceName, endpoint);

                    foreach (var scenario in Enum.GetValues<BenchmarkScenario>())
                    {
                        var scenarioStats = stats.Where(s => s.Scenario == scenario)
                            .OrderBy(s => s.Deployment).ThenBy(s => s.ReasoningEffort)
                            .ToArray();

                        if (scenarioStats.Length == 0)
                        {
                            continue;
                        }

                        BuildRankingSection(col.Item(), scenario, scenarioStats);
                        BuildDetailsSection(col.Item(), scenario, scenarioStats);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private static void BuildSummary(
        IContainer container,
        IReadOnlyList<ModelStatistics> stats,
        TimeSpan wallClock,
        string resourceName,
        Uri endpoint)
    {
        container.Background(AccentColor).Padding(12).Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Configuration").FontSize(13).Bold().FontColor(PrimaryColor);
            col.Item().Text($"Foundry resource: {resourceName}");
            col.Item().Text($"Endpoint: {endpoint}");
            col.Item().Text($"Test cells: {stats.Count} (deployments x reasoning effort x scenario)");
            col.Item().Text(
                $"Single-turn volume: {BenchmarkConfig.SingleTurnIterations} iterations x " +
                $"{BenchmarkConfig.SingleTurnCallsPerIteration} parallel calls");
            col.Item().Text(
                $"Multi-turn volume: {BenchmarkConfig.MultiTurnIterations} iterations x " +
                $"{BenchmarkConfig.MultiTurnCallsPerIteration} parallel conversation(s) x " +
                $"{Prompts.MultiTurnUserMessages.Length} turns");
            col.Item().Text($"Warm-up calls per cell: {BenchmarkConfig.WarmupCallsPerModel}");
            col.Item().Text($"Per-call timeout: {BenchmarkConfig.CallTimeout.TotalSeconds:F0} s");
            col.Item().Text($"Total billed calls: {stats.Sum(s => s.SampleCount)}");
            col.Item().Text($"Timed-out calls: {stats.Sum(s => s.TimedOutCount)}");
            col.Item().Text($"Wall-clock duration: {wallClock.TotalSeconds:F1} s");
        });
    }

    private static void BuildRankingSection(IContainer container, BenchmarkScenario scenario, IReadOnlyList<ModelStatistics> stats)
    {
        container.Column(col =>
        {
            col.Spacing(8);
            col.Item().Text($"{scenario} — Ranking by P50").FontSize(13).Bold().FontColor(PrimaryColor);

            col.Item().Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem().Element(c => RenderRanking(c, "TTFT P50 (ms, lower better)",
                    stats.OrderBy(s => s.TtftMs.P50).Select(s => (Label(s), s.TtftMs.P50)).ToArray(), "F1"));
                row.RelativeItem().Element(c => RenderRanking(c, "Total P50 (ms, lower better)",
                    stats.OrderBy(s => s.TotalMs.P50).Select(s => (Label(s), s.TotalMs.P50)).ToArray(), "F1"));
                row.RelativeItem().Element(c => RenderRanking(c, "Avg response (chars)",
                    stats.OrderByDescending(s => s.LengthChars.Avg).Select(s => (Label(s), s.LengthChars.Avg)).ToArray(), "F0"));
            });
        });
    }

    private static void RenderRanking(IContainer container, string title, (string Name, double Value)[] rows, string format)
    {
        container.Border(1).BorderColor(BorderColor).Padding(8).Column(col =>
        {
            col.Spacing(3);
            col.Item().Text(title).Bold().FontSize(10);
            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(18);
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                });
                for (var i = 0; i < rows.Length; i++)
                {
                    var (name, value) = rows[i];
                    table.Cell().Text($"{i + 1}.").FontSize(9);
                    table.Cell().Text(name).FontSize(9);
                    table.Cell().AlignRight().Text(value.ToString(format)).FontSize(9);
                }
            });
        });
    }

    private static void BuildDetailsSection(IContainer container, BenchmarkScenario scenario, IReadOnlyList<ModelStatistics> stats)
    {
        container.Column(col =>
        {
            col.Spacing(8);
            col.Item().Text($"{scenario} — Detailed statistics").FontSize(13).Bold().FontColor(PrimaryColor);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                });

                table.Header(header =>
                {
                    foreach (var label in new[] { "Model / effort", "Metric" })
                    {
                        header.Cell().Background(HeaderRowColor).Padding(5)
                            .Text(label).Bold().FontSize(9);
                    }
                    foreach (var label in new[] { "Min", "Avg", "P50", "P75", "P95", "Max" })
                    {
                        header.Cell().Background(HeaderRowColor).Padding(5).AlignRight()
                            .Text(label).Bold().FontSize(9);
                    }
                });

                foreach (var s in stats)
                {
                    var modelLabel = s.TimedOutCount > 0
                        ? $"{Label(s)}\n({s.TimedOutCount} timeout{(s.TimedOutCount == 1 ? string.Empty : "s")})"
                        : Label(s);
                    AppendMetricRow(table, modelLabel, "TTFT (ms)", s.TtftMs, "F1", topBorder: true);
                    AppendMetricRow(table, string.Empty, "Total (ms)", s.TotalMs, "F1");
                    AppendMetricRow(table, string.Empty, "Length (chars)", s.LengthChars, "F0");
                }
            });
        });
    }

    private static string Label(ModelStatistics s) => $"{s.Deployment} / {s.ReasoningEffort}";

    private static void AppendMetricRow(
        TableDescriptor table,
        string model,
        string metric,
        MetricStats m,
        string format,
        bool topBorder = false)
    {
        var border = topBorder ? 1f : 0f;

        table.Cell().BorderTop(border).BorderColor(BorderColor).Padding(4)
            .Text(model).FontSize(9).Bold();
        table.Cell().BorderTop(border).BorderColor(BorderColor).Padding(4)
            .Text(metric).FontSize(9);

        foreach (var value in new[] { m.Min, m.Avg, m.P50, m.P75, m.P95, m.Max })
        {
            table.Cell().BorderTop(border).BorderColor(BorderColor).Padding(4).AlignRight()
                .Text(value.ToString(format)).FontSize(9);
        }
    }
}
```

- [ ] **Step 3: Commit (still red, expected)**

```bash
git add AzureOpenAiBenchmark/StatisticsReporter.cs AzureOpenAiBenchmark/PdfReportGenerator.cs
git commit -m "Section console and PDF reports by scenario"
```

---

### Task 6: Rewire `BenchmarkConfig.cs` and `Program.cs`

**Files:**
- Modify: `AzureOpenAiBenchmark/BenchmarkConfig.cs`
- Modify: `AzureOpenAiBenchmark/Program.cs`

This is the task where the build turns green again — everything written in Tasks 1-5 gets wired together here.

- [ ] **Step 1: Rewrite `AzureOpenAiBenchmark/BenchmarkConfig.cs`**

```csharp
namespace AzureOpenAiBenchmark;

public static class BenchmarkConfig
{
    public const int WarmupCallsPerModel = 1;
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(25);

    public static readonly string[] DeploymentNames =
    [
        "gpt-5.4",
        "gpt-5.5",
        "gpt-5.4-prio",
        "gpt-5.5-prio",
    ];

    public static readonly string[] ReasoningEfforts = ["none", "low", "medium"];

    public const int SingleTurnIterations = 8;
    public const int SingleTurnCallsPerIteration = 2;

    public const int MultiTurnIterations = 5;
    public const int MultiTurnCallsPerIteration = 1;

    public static (int Iterations, int CallsPerIteration) VolumeFor(BenchmarkScenario scenario) => scenario switch
    {
        BenchmarkScenario.SingleTurn => (SingleTurnIterations, SingleTurnCallsPerIteration),
        BenchmarkScenario.MultiTurn => (MultiTurnIterations, MultiTurnCallsPerIteration),
        _ => throw new InvalidOperationException($"Unknown scenario: {scenario}"),
    };

    public static int TurnsPerCall(BenchmarkScenario scenario) => scenario switch
    {
        BenchmarkScenario.SingleTurn => 1,
        BenchmarkScenario.MultiTurn => Prompts.MultiTurnUserMessages.Length,
        _ => throw new InvalidOperationException($"Unknown scenario: {scenario}"),
    };

    public static IEnumerable<BenchmarkCell> AllCells() =>
        from deploymentName in DeploymentNames
        from reasoningEffort in ReasoningEfforts
        from scenario in Enum.GetValues<BenchmarkScenario>()
        select new BenchmarkCell(deploymentName, reasoningEffort, scenario);
}
```

- [ ] **Step 2: Rewrite `AzureOpenAiBenchmark/Program.cs`**

```csharp
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
    $"Starting: {benchmarks.Length} test cells (4 deployments x 3 reasoning efforts x 2 scenarios). " +
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
    var status = result.TimedOut
        ? $"TIMEOUT after {result.TotalResponseTime.TotalMilliseconds,8:F1} ms  Length={result.ResponseLength,5} chars (partial)"
        : $"TTFT={result.TimeToFirstToken.TotalMilliseconds,7:F1} ms  " +
          $"Total={result.TotalResponseTime.TotalMilliseconds,8:F1} ms  " +
          $"Length={result.ResponseLength,5} chars";

    Console.WriteLine($"[{current,4}/{totalCalls}] {label,-38} {status}");
}

var stopwatch = System.Diagnostics.Stopwatch.StartNew();

await Task.WhenAll(benchmarks.Select(b =>
{
    var (iterations, callsPerIteration) = BenchmarkConfig.VolumeFor(b.Scenario);
    return b.RunAsync(iterations, callsPerIteration, OnCallCompleted);
}));

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
```

- [ ] **Step 3: Build and confirm it's green**

Run: `dotnet build`
Expected: `Build succeeded.` (this is the first build expected to succeed since Task 2 — if it fails, the error will point at a leftover reference to a deleted type; fix it before moving on)

- [ ] **Step 4: Commit**

```bash
git add AzureOpenAiBenchmark/BenchmarkConfig.cs AzureOpenAiBenchmark/Program.cs
git commit -m "Wire up the 24-cell priority service tier benchmark matrix"
```

---

### Task 7: Release build and run command

**Files:** none (verification only)

- [ ] **Step 1: Build in Release configuration**

Run: `dotnet build -c Release` (from the repo root, or add `--project AzureOpenAiBenchmark`)
Expected: `Build succeeded.` with zero warnings/errors beyond the existing `NU1900` vulnerability-data-lookup warning (harmless, caused by sandboxed/offline NuGet access).

- [ ] **Step 2: Confirm the run command and hand it to the user**

The benchmark needs Azure CLI login (`AzureCliCredential`) and the `AzureFoundry:ResourceName` user secret (already set to `dev-qia-kaspkmsc-swe-ai` per the prior session). Run command:

```bash
dotnet run --project AzureOpenAiBenchmark -c Release
```

This runs all 24 cells (~372 billed calls plus 24 warm-up calls) against the real Foundry resource — it costs real money and takes real wall-clock time, so don't run it speculatively.

No commit for this task — it's a build/verification step, not a source change.
