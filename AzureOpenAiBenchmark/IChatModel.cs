namespace AzureOpenAiBenchmark;

/// <summary>
/// One deployment behind one provider API, configured for one reasoning effort. Yields the
/// visible text of the model's reply as it streams; reasoning output is never yielded.
/// </summary>
public interface IChatModel
{
    IAsyncEnumerable<string> StreamReplyTextAsync(
        string systemPrompt,
        IReadOnlyList<ConversationMessage> conversation,
        CancellationToken cancellationToken);
}
