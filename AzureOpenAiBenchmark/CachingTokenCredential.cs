using System.Collections.Concurrent;
using Azure.Core;

namespace AzureOpenAiBenchmark;

/// <summary>
/// Reuses each scope's access token until shortly before it expires. AzureCliCredential spawns an
/// "az" subprocess per call, and AnthropicFoundryIdentityTokenCredentials requests a token on every
/// HTTP request — without this, that subprocess time would be measured as part of each Claude call.
/// Serializing refreshes also stops concurrent calls racing on the Azure CLI's token cache lock file.
/// </summary>
public sealed class CachingTokenCredential : TokenCredential
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly TokenCredential _innerCredential;
    private readonly ConcurrentDictionary<string, AccessToken> _tokensByScope = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public CachingTokenCredential(TokenCredential innerCredential)
    {
        _innerCredential = innerCredential;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var scopeKey = string.Join(' ', requestContext.Scopes);
        if (TryGetFreshToken(scopeKey, out var token))
        {
            return token;
        }

        _refreshLock.Wait(cancellationToken);
        try
        {
            if (!TryGetFreshToken(scopeKey, out token))
            {
                token = _innerCredential.GetToken(requestContext, cancellationToken);
                _tokensByScope[scopeKey] = token;
            }

            return token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var scopeKey = string.Join(' ', requestContext.Scopes);
        if (TryGetFreshToken(scopeKey, out var token))
        {
            return token;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!TryGetFreshToken(scopeKey, out token))
            {
                token = await _innerCredential.GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);
                _tokensByScope[scopeKey] = token;
            }

            return token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool TryGetFreshToken(string scopeKey, out AccessToken token) =>
        _tokensByScope.TryGetValue(scopeKey, out token) &&
        token.ExpiresOn - DateTimeOffset.UtcNow > RefreshMargin;
}
