using System.ClientModel;
using Azure.AI.OpenAI;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace AzureOpenAiBenchmark;

[Config(typeof(BenchmarkConfig))]
public class LatencyBenchmark
{
    private const string Prompt = "Wyjaśnij mi czym jest człowiek.";

    [Params("gpt-5.4", "gpt-5.4-mini", "gpt-5.4-nano")]
    public string Deployment { get; set; } = "gpt-5.4-mini";

    private ChatClient _chatClient = null!;
    private ChatCompletionOptions _options = null!;
    private ChatMessage[] _messages = null!;

    [GlobalSetup]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(LatencyBenchmark).Assembly)
            .Build();

        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Brak sekretu 'AzureOpenAI:Endpoint'.");
        var apiKey = configuration["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("Brak sekretu 'AzureOpenAI:ApiKey'.");

        var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
        _chatClient = client.GetChatClient(Deployment);

        _options = new ChatCompletionOptions
        {
            ReasoningEffortLevel = ChatReasoningEffortLevel.None,
        };

        _messages = [new UserChatMessage(Prompt)];
    }

    [Benchmark(Description = "TTFT")]
    public async Task<string> TimeToFirstToken()
    {
        await foreach (StreamingChatCompletionUpdate update in
            _chatClient.CompleteChatStreamingAsync(_messages, _options))
        {
            foreach (ChatMessageContentPart part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    return part.Text;
                }
            }
        }

        return string.Empty;
    }
}
