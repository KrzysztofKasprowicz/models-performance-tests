using System.Diagnostics;
using Azure.AI.Inference;
using Azure.Core;

namespace AzureOpenAiBenchmark;

public sealed class FoundryInferenceInvoker : IModelInvoker
{
    private readonly ChatCompletionsClient _client;
    private readonly ChatCompletionsOptions _options;

    public string Deployment { get; }

    public FoundryInferenceInvoker(
        FoundryInferenceDeployment deployment,
        Uri endpoint,
        TokenCredential credential,
        string prompt)
    {
        Deployment = deployment.Name;
        _client = new ChatCompletionsClient(endpoint, credential);

        _options = new ChatCompletionsOptions
        {
            Model = deployment.Name,
            Messages = { new ChatRequestUserMessage(prompt) },
        };

        // Request no reasoning content when supported (Kimi K2.6, DeepSeek reasoning variants).
        // Unknown parameters are ignored by models that do not support them.
        _options.AdditionalProperties["enable_thinking"] = BinaryData.FromString("false");
        _options.AdditionalProperties["thinking"] = BinaryData.FromString("\"none\"");
    }

    public async Task<CallResult> MeasureSingleCallAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        TimeSpan? timeToFirstToken = null;
        var responseLength = 0;

        var stream = await _client
            .CompleteStreamingAsync(_options, cancellationToken)
            .ConfigureAwait(false);

        await foreach (var update in stream.EnumerateValues().WithCancellation(cancellationToken))
        {
            var content = update.ContentUpdate;
            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            timeToFirstToken ??= stopwatch.Elapsed;
            responseLength += content.Length;
        }

        stopwatch.Stop();
        return new CallResult(
            timeToFirstToken ?? stopwatch.Elapsed,
            stopwatch.Elapsed,
            responseLength);
    }
}
