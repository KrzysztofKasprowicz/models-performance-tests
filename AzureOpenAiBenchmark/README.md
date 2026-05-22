# AzureOpenAiBenchmark

Równoległy pomiar **TTFT** (time-to-first-token) i **całkowitego czasu odpowiedzi**
deploymentów Azure OpenAI z `reasoning_effort = none`. Własny runner (bez BenchmarkDotNet),
bo BDN nie wspiera dobrze równoległości w obrębie jednego benchmarku ani pomiaru dwóch
metryk per wywołanie.

## Plan testu

- Trzy modele: `gpt-5.4`, `gpt-5.4-mini`, `gpt-5.4-nano` (`BenchmarkConfig.Deployments`).
- Modele są testowane **równolegle względem siebie** (`Task.WhenAll` po modelach).
- Każdy model: **10 iteracji × 5 równoległych wywołań** = 50 zapytań na model.
- Łącznie **3 × 50 = 150** płatnych wywołań + **1 rozgrzewające** na model (3 dodatkowe).
- Prompt jest zaprojektowany tak, by wymusić odpowiedzi o zbliżonej długości
  (cztery akapity po dwa zdania, 15–20 słów na zdanie).

## Mierzone metryki (per wywołanie)

- **TTFT** — czas od wysłania żądania do pierwszego niepustego tokenu w streamie.
- **Total** — czas od wysłania żądania do zamknięcia streama.
- **Length** — długość pełnej odpowiedzi w znakach.

## Statystyki (per model)

Min / Avg / Median / P95 / Max dla TTFT, Total i Length. Dodatkowo wall-clock całego testu.

## 1. Autoryzacja (Azure CLI)

Autoryzacja odbywa się przez `AzureCliCredential` (Microsoft Entra ID, keyless).
Zaloguj się wcześniej do tej tenant/sub, która ma dostęp do zasobu OpenAI:

```bash
az login
az account set --subscription "<NAZWA-LUB-ID-SUBSKRYPCJI>"
```

Identity używana przez `az` musi mieć przypisaną rolę
**Cognitive Services OpenAI User** (lub równoważną) na zasobie Azure OpenAI.

## 2. Konfiguracja (user secrets)

`Endpoint` to **bazowy adres zasobu** (bez ścieżki `/openai/deployments/...`):

```bash
cd AzureOpenAiBenchmark

dotnet user-secrets set "AzureOpenAI:Endpoint" "https://TWOJ-ZASOB.openai.azure.com/"
```

Podgląd zapisanych sekretów:

```bash
dotnet user-secrets list
```

## 3. Uruchomienie

```bash
dotnet run -c Release
```

## Strojenie

W `BenchmarkConfig.cs`:

- `Deployments` — lista nazw deploymentów do porównania.
- `Iterations` — liczba sekwencyjnych iteracji na model (domyślnie `10`).
- `CallsPerIteration` — liczba równoległych wywołań w obrębie jednej iteracji (domyślnie `5`).
- `WarmupCallsPerModel` — liczba rozgrzewających wywołań na model (domyślnie `1`).
- `Prompt` — treść zapytania (zaprojektowana pod stabilną długość odpowiedzi).
