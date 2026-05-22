# AzureOpenAiBenchmark

Parallel benchmark for Azure AI Foundry deployments that measures **TTFT**
(time-to-first-token) and **total response time**. A custom runner is used
instead of BenchmarkDotNet because BDN does not support intra-benchmark
parallelism nor capturing two timing metrics per call.

The benchmark targets three model families on the same Foundry resource,
each reached through a different API surface:

| Provider                | SDK / Transport               | Endpoint path                                      |
|-------------------------|-------------------------------|----------------------------------------------------|
| Azure OpenAI (`gpt-*`)  | `Azure.AI.OpenAI`             | `https://<resource>.openai.azure.com/`             |
| Foundry Inference       | `Azure.AI.Inference`          | `https://<resource>.services.ai.azure.com/models`  |
| Anthropic on Foundry    | raw `HttpClient` (SSE)        | `https://<resource>.services.ai.azure.com/anthropic/v1/messages` |

All three share a single `AzureCliCredential` (Microsoft Entra ID, keyless).

## Test plan

Currently enabled deployments (`BenchmarkConfig.Deployments`):

- Azure OpenAI: `gpt-5.4`, `gpt-5.4-mini`, `gpt-5.4-nano` (with `reasoning_effort = none`)
- Foundry Inference: `Kimi-K2.6`, `DeepSeek-V4-Flash` (`enable_thinking = false` sent best-effort)
- Anthropic on Foundry: `claude-sonnet-4-6`, `claude-haiku-4-5` (`thinking.type = disabled`)

Disabled (commented out, easy to re-enable): `gpt-5.1`, `gpt-4.1`, `gpt-4.1-mini`, `gpt-4.1-nano`.

Per-run shape:

- All models run **in parallel** (`Task.WhenAll` over models).
- Per model: **10 iterations × 5 parallel calls** = 50 requests per model.
- Plus **1 warm-up** call per model.
- The prompt is designed to produce responses of comparable length
  (four paragraphs of two sentences, 15–20 words per sentence).

## Per-call metrics

- **TTFT** — time from request start to the first non-empty streamed token.
- **Total** — time from request start to the end of the response stream.
- **Length** — total response length in characters.

## Per-model statistics

Min / Avg / P50 / P75 / P95 / Max for TTFT, Total, and Length.
The overall wall-clock duration is also printed.

## Output

The run prints results to the console and also writes a formatted PDF
report to `output/benchmark-YYYYMMDD-HHmmss.pdf` in the repository root.
The `output/` directory is git-ignored.

## 1. Authorization (Azure CLI)

```bash
az login
az account set --subscription "<SUBSCRIPTION-NAME-OR-ID>"
```

The identity must have:

- **Cognitive Services OpenAI User** — for Azure OpenAI deployments.
- **Azure AI Developer** (or higher) on the Foundry resource — for Foundry
  Inference and the Anthropic endpoint.

## 2. Configuration (user secrets)

Only the resource name is stored; endpoints are derived from it.

```bash
cd AzureOpenAiBenchmark

dotnet user-secrets set "AzureFoundry:ResourceName" "ailadevai"
```

Verify:

```bash
dotnet user-secrets list
```

## 3. Run

```bash
dotnet run -c Release
```

## Tuning

In `BenchmarkConfig.cs`:

- `Deployments` — list of deployment configs (Azure OpenAI / Foundry Inference / Claude).
- `Iterations` — sequential iterations per model (default `10`).
- `CallsPerIteration` — parallel calls within one iteration (default `5`).
- `WarmupCallsPerModel` — warm-up calls per model (default `1`).
- `Prompt` — request body (designed for stable response length).
