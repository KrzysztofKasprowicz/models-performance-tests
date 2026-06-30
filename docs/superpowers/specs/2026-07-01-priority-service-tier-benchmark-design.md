# Priority service tier benchmark — design

Date: 2026-07-01
Branch: `feature/priority-service-tier-benchmark`

## Goal

Replace the existing multi-vendor benchmark (Claude/Kimi/DeepSeek/gpt-oss) with a focused
comparison of Azure OpenAI's `priority` service tier against the standard tier, across
reasoning effort levels and conversation shapes, for the GPT-5.4 / GPT-5.5 family.

## Test matrix

Full cross product, 24 cells:

- **Deployments (4)**: `gpt-5.4`, `gpt-5.5`, `gpt-5.4-prio`, `gpt-5.5-prio`
- **Reasoning effort (3)**: `none`, `low`, `medium`
- **Scenario (2)**: single-turn, multi-turn (3 user messages / 3 responses)

## Priority service tier

Azure OpenAI / Microsoft Foundry chat completions accept a request-level `service_tier`
attribute (`auto` | `default` | `priority`) that overrides the deployment-level setting
(source: `learn.microsoft.com/azure/foundry/openai/concepts/priority-processing`).

Rule: any deployment whose name ends in `-prio` automatically gets
`ChatCompletionOptions.ServiceTier = "priority"` on every request. Deployments without that
suffix never set `ServiceTier` (request defaults to `auto`, i.e. whatever the deployment
itself is configured as in Azure).

Confirmed compatible with the installed `OpenAI` 2.9.1 / `Azure.AI.OpenAI` 2.9.0-beta.1
SDKs: `ChatServiceTier` is an extensible string-backed enum (public `ChatServiceTier(string)`
constructor + implicit `string` conversion), so `"priority"` works even though the SDK has no
named `ChatServiceTier.Priority` constant yet.

## Prompts

A single system prompt is shared by both scenarios. It bounds response length so that
"Total response time" differences are driven by TTFT/reasoning/service-tier, not by one
model being more verbose than another:

```
You are a precise, efficient assistant. Answer the user's latest message directly, in
plain English prose only — no Markdown, no lists, no headings. Stay between 60 and 100
words regardless of how complex the question is.
```

### Single-turn scenario

One user message, designed to require actual multi-step reasoning (so `none`/`low`/`medium`
have something to differentiate) while keeping the answer short and close to deterministic:

```
A warehouse network has three sites. Site A starts with 420 units and gains 35 units per
day. Site B starts with 290 units and gains 52 units per day. Site C starts with 610 units
but loses 18 units per day to spoilage. After how many full days does Site B first hold
more units than Site C, and which site holds the most units at that point? Give the day
count, the site, and a one-sentence justification.
```

### Multi-turn scenario (fixed script)

Three user messages, scripted in advance. The "assistant" turns injected into the
conversation history between user turns are **also fixed, canned text** — not the model's
real output — so every one of the 24 cells sees byte-identical conversation context. The
real, measured call still streams a genuine response from the model at every turn; the
canned text is only what gets fed back as history for the next turn.

1. User: plan a 3-day Kraków trip for two adults (Old Town, Wieliczka Salt Mine, one
   evening of local food).
   Fixed assistant placeholder: a short canned 3-day itinerary.
2. User: budget is tighter than expected (~150 EUR/person excluding flights/hotel); cut
   costs without dropping the salt mine.
   Fixed assistant placeholder: canned cost-cutting adjustments.
3. User: one traveler is vegetarian and avoids gluten; update food recommendations across
   all three days and give a short final summary.

Each turn adds genuine reasoning load (constraint satisfaction over accumulating context),
and the growing — but fully reproducible — history exercises TTFT under more realistic
conditions than a single isolated question.

Per-call samples (TTFT, total time, length) from all 3 turns feed into the same
`MetricStats` pool for that cell, so "average time" falls out of the existing `Avg` field —
no new aggregation logic needed.

## Architecture changes

- **Remove**: `ClaudeFoundryInvoker.cs`, the `DeploymentConfig`/`ClaudeFoundryDeployment`
  polymorphism, the invoker factory switch in `Program.cs`.
- **New `ChatCallExecutor`**: static helper holding the streaming-call measurement logic
  currently embedded in `AzureOpenAiInvoker` (TTFT capture, timeout handling), parameterized
  over an arbitrary message list + `ChatCompletionOptions` so both scenarios can reuse it.
- **New `SingleTurnInvoker`** and **`MultiTurnInvoker`** (both `IModelInvoker`). Multi-turn
  runs 3 sequential calls per invocation (can't parallelize within one conversation; history
  depends on prior turn) and returns all 3 results.
- **`IModelInvoker`**: `MeasureSingleCallAsync` becomes
  `Task<IReadOnlyList<CallResult>> MeasureAsync(CancellationToken)` (1 result for single-turn,
  3 for multi-turn). `ModelBenchmark` flattens these into its existing sample list —
  `MetricStats`/percentile math is unchanged.
- **`ModelStatistics`**: add `ReasoningEffort`, `Scenario`, `IsPriority` fields for reporting.
- **`StatisticsReporter`** / **`PdfReportGenerator`**: group output into two sections
  (single-turn, multi-turn), rows per deployment × reasoning effort within each section.

Known characteristic, not a defect: all 24 cells run concurrently (same as the current
benchmark's "all models in parallel" approach), so the 3 reasoning-effort variants of a
non-`-prio` deployment do share that deployment's real-world capacity while running. This
mirrors production-like concurrent load and is intentional; it does not affect the
`-prio` vs. standard comparison since those are distinct Azure deployments.

## Volume

- **Single-turn**: 8 iterations × 2 parallel calls = 16 calls/cell → 192 calls total.
- **Multi-turn**: 5 iterations × 1 conversation at a time = 5 conversations/cell × 3 turns
  = 15 calls/cell → 180 calls total.
- **Total**: ~372 billed calls across all 24 cells (plus 1 warm-up call per cell = 24).

This keeps multi-turn's effective call volume close to single-turn's despite multi-turn
doing 3x the work per iteration, to avoid an unplanned 3x cost increase (relevant given
`-prio` deployments bill at a premium).

## Reporting

Console output and PDF report (QuestPDF, reusing existing `PdfReportGenerator`), both
restructured into two sections (single-turn / multi-turn). Each section's table has one row
per deployment × reasoning effort, with the existing TTFT/Total/Length percentile columns
(Min/Avg/P50/P75/P95/Max).

## Out of scope

- No CLI arguments for selecting a subset of the matrix — full 24-cell sweep every run
  (matches existing project convention of hardcoded `BenchmarkConfig` constants).
- No change to authentication (`AzureCliCredential` against the Foundry resource, unchanged).
