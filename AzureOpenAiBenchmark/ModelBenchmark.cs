using System.Diagnostics;
using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

public sealed class ModelBenchmark
{
    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _options;
    private readonly ChatMessage[] _messages;

    public string Deployment { get; }
    public IReadOnlyList<CallResult> Results => _results;

    private readonly List<CallResult> _results = new();
    private readonly object _resultsLock = new();

    public ModelBenchmark(string deployment, ChatClient chatClient)
    {
        Deployment = deployment;
        _chatClient = chatClient;
        _options = new ChatCompletionOptions
        {
            ReasoningEffortLevel = ChatReasoningEffortLevel.None,
        };
        _messages = [new UserChatMessage(BenchmarkConfig.Prompt)];
    }

    public async Task WarmUpAsync(int calls, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < calls; i++)
        {
            _ = await MeasureSingleCallAsync(cancellationToken);
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
                    var result = await MeasureSingleCallAsync(cancellationToken);
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

    private async Task<CallResult> MeasureSingleCallAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        TimeSpan? timeToFirstToken = null;
        var responseLength = 0;

        await foreach (var update in _chatClient
                           .CompleteChatStreamingAsync(_messages, _options, cancellationToken)
                           .ConfigureAwait(false))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (string.IsNullOrEmpty(part.Text))
                {
                    continue;
                }

                timeToFirstToken ??= stopwatch.Elapsed;
                responseLength += part.Text.Length;
            }
        }

        stopwatch.Stop();

        return new CallResult(
            timeToFirstToken ?? stopwatch.Elapsed,
            stopwatch.Elapsed,
            responseLength);
    }
}
