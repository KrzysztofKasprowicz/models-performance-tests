namespace AzureOpenAiBenchmark;

public static class BenchmarkConfig
{
    public const int WarmupCallsPerModel = 1;
    public const int Iterations = 10;
    public const int CallsPerIteration = 5;

    public static readonly string[] Deployments =
    [
        "gpt-5.4",
        "gpt-5.4-mini",
        "gpt-5.4-nano",
    ];

    public const string Prompt =
        "Odpowiedz dokładnie czterema akapitami po dwa zdania każdy. " +
        "Wyjaśnij krótko: (1) czym jest fotosynteza, (2) jakie są jej główne etapy, " +
        "(3) jakie ma znaczenie dla życia na Ziemi, (4) jak wykorzystywana jest w rolnictwie. " +
        "Każde zdanie powinno mieć od 15 do 20 słów. " +
        "Nie używaj list, nagłówków ani znaków formatowania Markdown.";
}
