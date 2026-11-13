using System.ClientModel;
using System.Diagnostics;
using Anthropic.Exceptions;

namespace AzureOpenAiBenchmark;

/// <summary>
/// Streams one reply and measures time-to-first-token / total time / response length. Shared by
/// both single-turn and multi-turn invokers and by every provider API — the only differences are
/// which conversation is passed in and which <see cref="IChatModel"/> streams it.
/// </summary>
public static class ChatCallExecutor
{
    public static async Task<CallResult> ExecuteAsync(
        IChatModel chatModel,
        string systemPrompt,
        IReadOnlyList<ConversationMessage> conversation,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(BenchmarkConfig.CallTimeout);

        var stopwatch = Stopwatch.StartNew();
        TimeSpan? timeToFirstToken = null;
        var responseLength = 0;
        var timedOut = false;
        int? failedStatusCode = null;

        try
        {
            await foreach (var text in chatModel
                               .StreamReplyTextAsync(systemPrompt, conversation, timeoutCts.Token)
                               .ConfigureAwait(false))
            {
                timeToFirstToken ??= stopwatch.Elapsed;
                responseLength += text.Length;
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
        }
        // A 429 (rate limit) or transient 5xx from Azure shouldn't take down the whole benchmark
        // run — record it as one failed sample instead of letting it propagate through
        // Task.WhenAll and lose every already-collected result.
        catch (ClientResultException ex)
        {
            failedStatusCode = ex.Status;
        }
        catch (AnthropicApiException ex)
        {
            failedStatusCode = (int)ex.StatusCode;
        }

        stopwatch.Stop();
        return new CallResult(
            timeToFirstToken ?? stopwatch.Elapsed,
            stopwatch.Elapsed,
            responseLength,
            timedOut,
            failedStatusCode);
    }
}
