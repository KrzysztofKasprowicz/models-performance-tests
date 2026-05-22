namespace AzureOpenAiBenchmark;

public interface IModelInvoker
{
    string Deployment { get; }

    Task<CallResult> MeasureSingleCallAsync(CancellationToken cancellationToken);
}
