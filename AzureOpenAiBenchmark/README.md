# AzureOpenAiBenchmark

Pomiar **TTFT** (time-to-first-token) deploymentów Azure OpenAI z `reasoning_effort = none`
za pomocą [BenchmarkDotNet](https://benchmarkdotnet.org/).

- Porównywane modele (nazwy deploymentów zahardkodowane w `LatencyBenchmark.cs` → `[Params]`):
  `gpt-5.4`, `gpt-5.4-mini`, `gpt-5.4-nano`. Każdy dostaje osobny wiersz Min/Median/Max.
- Na model: **10 sekwencyjnych** zapytań mierzonych + **1 rozgrzewające**
  (`RunStrategy.Monitoring` — bez fazy pilotażu). Łącznie **3 × 11 = 33** płatnych wywołań.
- Mierzony jest czas do **pierwszego tokenu** (streaming, przerwanie po pierwszym tokenie).
- Zapytanie: *"Wyjaśnij mi czym jest człowiek."*

## 1. Konfiguracja (user secrets)

`Endpoint` to **bazowy adres zasobu**, np. `https://twoj-zasob.openai.azure.com/`
(bez ścieżki `/openai/deployments/...`). Nazwa deploymentu podawana jest osobno.

```bash
cd AzureOpenAiBenchmark

dotnet user-secrets set "AzureOpenAI:Endpoint" "https://TWOJ-ZASOB.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey"   "WKLEJ-TUTAJ-KLUCZ"
```

> Nazwy deploymentów są zahardkodowane (`[Params]` w `LatencyBenchmark.cs`) — nie podaje się ich w sekretach.

Podgląd / weryfikacja zapisanych sekretów:

```bash
dotnet user-secrets list
```

## 2. Uruchomienie

> BenchmarkDotNet wymaga buildu **Release**.

```bash
dotnet run -c Release
```

Wyniki wypiszą się na konsolę i trafią do `BenchmarkDotNet.Artifacts/results/`
(plik `*-report-github.md`).

## Strojenie

W `BenchmarkConfig.cs`:

- `WarmupCount` — liczba rozgrzewających wywołań (domyślnie `1`; max sensownie `3`).
- `MeasuredIterations` — liczba mierzonych zapytań (domyślnie `10`).
