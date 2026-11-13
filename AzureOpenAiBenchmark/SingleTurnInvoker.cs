namespace AzureOpenAiBenchmark;

public sealed class SingleTurnInvoker : IModelInvoker
{
    private static readonly ConversationMessage[] Conversation =
    [
        new(ConversationRole.User, Prompts.SingleTurnUserPrompt),
    ];

    private readonly IChatModel _chatModel;

    public string Deployment { get; }
    public string ReasoningEffort { get; }
    public BenchmarkScenario Scenario => BenchmarkScenario.SingleTurn;
    public bool IsPriority { get; }

    public SingleTurnInvoker(BenchmarkCell cell, IChatModel chatModel)
    {
        Deployment = cell.DeploymentName;
        ReasoningEffort = cell.ReasoningEffort;
        IsPriority = cell.IsPriority;
        _chatModel = chatModel;
    }

    public async Task<IReadOnlyList<CallResult>> MeasureAsync(CancellationToken cancellationToken)
    {
        var result = await ChatCallExecutor.ExecuteAsync(_chatModel, Prompts.SystemPrompt, Conversation, cancellationToken)
            .ConfigureAwait(false);
        return [result];
    }
}
