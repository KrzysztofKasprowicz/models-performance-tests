using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

public sealed class SingleTurnInvoker : IModelInvoker
{
    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _options;
    private readonly ChatMessage[] _messages;

    public string Deployment { get; }
    public string ReasoningEffort { get; }
    public BenchmarkScenario Scenario => BenchmarkScenario.SingleTurn;
    public bool IsPriority { get; }

    public SingleTurnInvoker(BenchmarkCell cell, AzureOpenAIClient client)
    {
        Deployment = cell.DeploymentName;
        ReasoningEffort = cell.ReasoningEffort;
        IsPriority = cell.IsPriority;
        _chatClient = client.GetChatClient(cell.DeploymentName);
        _options = ChatCompletionOptionsFactory.Create(cell.ReasoningEffort);
        _messages =
        [
            new SystemChatMessage(Prompts.SystemPrompt),
            new UserChatMessage(Prompts.SingleTurnUserPrompt),
        ];
    }

    public async Task<IReadOnlyList<CallResult>> MeasureAsync(CancellationToken cancellationToken)
    {
        var result = await ChatCallExecutor.ExecuteAsync(_chatClient, _messages, _options, cancellationToken)
            .ConfigureAwait(false);
        return [result];
    }
}
