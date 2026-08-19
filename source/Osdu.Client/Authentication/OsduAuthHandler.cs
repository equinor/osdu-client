using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;

namespace Osdu.Client.Authentication;

public sealed class OsduAuthHandler : DelegatingHandler
{
    private readonly IConfidentialClientApplication? _app;
    private readonly TokenCredential? _credential;
    private readonly string[] _scopes;

    /// <summary>
    /// Uses DefaultAzureCredential (managed identity, VS, CLI, etc.).
    /// </summary>
    public OsduAuthHandler(string scope) : this(new DefaultAzureCredential(), scope)
    {
    }

    /// <summary>
    /// Uses any Azure.Identity TokenCredential (e.g., ManagedIdentityCredential, InteractiveBrowserCredential).
    /// </summary>
    public OsduAuthHandler(TokenCredential credential, string scope)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _scopes = [scope];
    }

    /// <summary>
    /// Uses MSAL confidential client with client secret (service principal).
    /// </summary>
    public OsduAuthHandler(string tenantId, string clientId, string clientSecret, string scope)
    {
        _scopes = [scope];
        _app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
            .Build();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string accessToken;

        if (_credential is not null)
        {
            var tokenRequestContext = new TokenRequestContext(_scopes);
            var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            accessToken = token.Token;
        }
        else
        {
            var result = await _app!.AcquireTokenForClient(_scopes)
                .ExecuteAsync(cancellationToken);
            accessToken = result.AccessToken;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
