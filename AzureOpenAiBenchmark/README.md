# AzureOpenAiBenchmark

Pomiar czasu odpowiedzi deploymentu Azure OpenAI (`gpt-5.4-mini`) z `reasoning_effort = none`
za pomocą [BenchmarkDotNet](https://benchmarkdotnet.org/).

- **10 sekwencyjnych** zapytań mierzonych + **1 rozgrzewające** (`RunStrategy.Monitoring` — bez fazy
  pilotażu, więc liczba płatnych wywołań jest dokładnie znana: **11**).
- Raport zawiera **Min**, **Median** i **Max** (oraz domyślnie Mean/StdDev).
- Zapytanie: *"Wyjaśnij mi czym jest człowiek."*

## 1. Konfiguracja (user secrets)

`Endpoint` to **bazowy adres zasobu**, np. `https://twoj-zasob.openai.azure.com/`
(bez ścieżki `/openai/deployments/...`). Nazwa deploymentu podawana jest osobno.

```bash
cd AzureOpenAiBenchmark

dotnet user-secrets set "AzureOpenAI:Endpoint"   "https://TWOJ-ZASOB.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey"     "WKLEJ-TUTAJ-KLUCZ"
dotnet user-secrets set "AzureOpenAI:Deployment" "gpt-5.4-mini"
```

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
