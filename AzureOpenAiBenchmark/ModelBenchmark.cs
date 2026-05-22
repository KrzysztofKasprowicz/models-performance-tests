namespace AzureOpenAiBenchmark;

public sealed class ModelBenchmark
{
    private readonly IModelInvoker _invoker;
    private readonly List<CallResult> _results = new();
    private readonly object _resultsLock = new();

    public string Deployment => _invoker.Deployment;
    public IReadOnlyList<CallResult> Results => _results;

    public ModelBenchmark(IModelInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task WarmUpAsync(int calls, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < calls; i++)
        {
            _ = await _invoker.MeasureSingleCallAsync(cancellationToken);
        }
    }

    public async Task RunAsync(
        int iterations,
        int callsPerIteration,
        Action<string, CallResult> onCallCompleted,
        CancellationToken cancellationToken = default)
    {
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var tasks = Enumerable.Range(0, callsPerIteration)
                .Select(async _ =>
                {
                    var result = await _invoker.MeasureSingleCallAsync(cancellationToken);
                    lock (_resultsLock)
                    {
                        _results.Add(result);
                    }
                    onCallCompleted(Deployment, result);
                    return result;
                })
                .ToArray();

            await Task.WhenAll(tasks);
        }
    }
}
