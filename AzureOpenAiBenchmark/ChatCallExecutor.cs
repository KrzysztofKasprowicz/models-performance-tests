using System.ClientModel;
using System.Diagnostics;
using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

/// <summary>
/// Streams one chat completion call and measures time-to-first-token / total time / response
/// length. Shared by both single-turn and multi-turn invokers — the only difference between
/// them is what message list they pass in.
/// </summary>
public static class ChatCallExecutor
{
    public static async Task<CallResult> ExecuteAsync(
        ChatClient chatClient,
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions options,
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
            await foreach (var update in chatClient
                               .CompleteChatStreamingAsync(messages, options, timeoutCts.Token)
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
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
        }
        catch (ClientResultException ex)
        {
            // A 429 (rate limit) or transient 5xx from Azure shouldn't take down the whole
            // benchmark run — record it as one failed sample instead of letting it propagate
            // through Task.WhenAll and lose every already-collected result.
            failedStatusCode = ex.Status;
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
