using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;

namespace AzureOpenAiBenchmark;

public sealed class ClaudeFoundryInvoker : IModelInvoker
{
    private static readonly string[] AzureAiScopes = ["https://ai.azure.com/.default"];

    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;
    private readonly Uri _endpoint;
    private readonly byte[] _requestBody;

    public string Deployment { get; }

    public ClaudeFoundryInvoker(
        ClaudeFoundryDeployment deployment,
        Uri foundryBaseEndpoint,
        TokenCredential credential,
        string prompt,
        HttpClient? httpClient = null)
    {
        Deployment = deployment.Name;
        _endpoint = new Uri(foundryBaseEndpoint, "anthropic/v1/messages");
        _credential = credential;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        var payload = new
        {
            model = deployment.Name,
            max_tokens = 2048,
            stream = true,
            thinking = new { type = "disabled" },
            messages = new[]
            {
                new { role = "user", content = prompt },
            },
        };
        _requestBody = JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    public async Task<CallResult> MeasureSingleCallAsync(CancellationToken cancellationToken)
    {
        var token = await _credential
            .GetTokenAsync(new TokenRequestContext(AzureAiScopes), cancellationToken)
            .ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Content = new ByteArrayContent(_requestBody);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var stopwatch = Stopwatch.StartNew();
        TimeSpan? timeToFirstToken = null;
        var responseLength = 0;

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Claude Foundry call failed for {Deployment}: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {error}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }
            if (line.Length == 0)
            {
                continue;
            }

            const string dataPrefix = "data:";
            if (!line.StartsWith(dataPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line[dataPrefix.Length..].TrimStart();
            if (payload.Length == 0 || payload == "[DONE]")
            {
                continue;
            }

            var text = ExtractTextDelta(payload);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            timeToFirstToken ??= stopwatch.Elapsed;
            responseLength += text.Length;
        }

        stopwatch.Stop();
        return new CallResult(
            timeToFirstToken ?? stopwatch.Elapsed,
            stopwatch.Elapsed,
            responseLength);
    }

    private static string? ExtractTextDelta(string sseDataPayload)
    {
        using var doc = JsonDocument.Parse(sseDataPayload);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp))
        {
            return null;
        }

        if (typeProp.GetString() != "content_block_delta")
        {
            return null;
        }

        if (!root.TryGetProperty("delta", out var delta))
        {
            return null;
        }

        if (delta.TryGetProperty("type", out var deltaType) &&
            deltaType.GetString() != "text_delta")
        {
            return null;
        }

        return delta.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
    }
}
