# AzureOpenAiBenchmark

Parallel benchmark for Azure OpenAI deployments that measures **TTFT**
(time-to-first-token) and **total response time**. A custom runner is used
instead of BenchmarkDotNet because BDN does not support intra-benchmark
parallelism nor capturing two timing metrics per call.

## Test plan

- Seven deployments (`BenchmarkConfig.Deployments`):
  - reasoning models (with `reasoning_effort = none`): `gpt-5.4`, `gpt-5.4-mini`, `gpt-5.4-nano`, `gpt-5.1`
  - non-reasoning models (`reasoning_effort` not sent): `gpt-4.1`, `gpt-4.1-mini`, `gpt-4.1-nano`
- All models run **in parallel** (`Task.WhenAll` over models).
- Per model: **10 iterations × 5 parallel calls** = 50 requests per model.
- Totals: **7 × 50 = 350** billed calls + **1 warm-up** call per model (7 extra).
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

Authorization uses `AzureCliCredential` (Microsoft Entra ID, keyless).
Sign in beforehand to the tenant/subscription that has access to the
Azure OpenAI resource:

```bash
az login
az account set --subscription "<SUBSCRIPTION-NAME-OR-ID>"
```

The identity used by `az` must have the
**Cognitive Services OpenAI User** role (or equivalent) on the
Azure OpenAI resource.

## 2. Configuration (user secrets)

`Endpoint` is the **resource base URL** (without the `/openai/deployments/...` path):

```bash
cd AzureOpenAiBenchmark

dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
```

Verify stored secrets:

```bash
dotnet user-secrets list
```

## 3. Run

```bash
dotnet run -c Release
```

## Tuning

In `BenchmarkConfig.cs`:

- `Deployments` — list of deployment names to compare.
- `Iterations` — number of sequential iterations per model (default `10`).
- `CallsPerIteration` — number of parallel calls within a single iteration (default `5`).
- `WarmupCallsPerModel` — number of warm-up calls per model (default `1`).
- `Prompt` — request body (designed for stable response length).
