using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

/// <summary>
/// Runs the fixed 3-turn script. Each turn's real streamed response is measured, but the
/// conversation history fed to the next turn uses <see cref="Prompts.MultiTurnFixedAssistantReplies"/>
/// instead of what the model actually said, so every test cell sees identical context.
/// </summary>
public sealed class MultiTurnInvoker : IModelInvoker
{
    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _options;

    public string Deployment { get; }
    public string ReasoningEffort { get; }
    public BenchmarkScenario Scenario => BenchmarkScenario.MultiTurn;
    public bool IsPriority { get; }

    public MultiTurnInvoker(BenchmarkCell cell, AzureOpenAIClient client)
    {
        Deployment = cell.DeploymentName;
        ReasoningEffort = cell.ReasoningEffort;
        IsPriority = cell.IsPriority;
        _chatClient = client.GetChatClient(cell.DeploymentName);
        _options = ChatCompletionOptionsFactory.Create(cell.ReasoningEffort, cell.IsPriority);
    }

    public async Task<IReadOnlyList<CallResult>> MeasureAsync(CancellationToken cancellationToken)
    {
        var history = new List<ChatMessage> { new SystemChatMessage(Prompts.SystemPrompt) };
        var results = new List<CallResult>(Prompts.MultiTurnUserMessages.Length);

        for (var turn = 0; turn < Prompts.MultiTurnUserMessages.Length; turn++)
        {
            history.Add(new UserChatMessage(Prompts.MultiTurnUserMessages[turn]));

            var result = await ChatCallExecutor.ExecuteAsync(_chatClient, history, _options, cancellationToken)
                .ConfigureAwait(false);
            results.Add(result);

            if (turn < Prompts.MultiTurnFixedAssistantReplies.Length)
            {
                history.Add(new AssistantChatMessage(Prompts.MultiTurnFixedAssistantReplies[turn]));
            }
        }

        return results;
    }
}
