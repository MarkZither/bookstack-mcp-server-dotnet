using BookStack.Mcp.Server.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookStack.Mcp.Server.Api;

public sealed class AuthenticationHandler : DelegatingHandler
{
    private readonly BookStackApiClientOptions _options;
    private readonly ILogger<AuthenticationHandler> _logger;

    public AuthenticationHandler(
        IOptions<BookStackApiClientOptions> options,
        ILogger<AuthenticationHandler> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Token",
            $"{_options.TokenId}:{_options.TokenSecret}");

        _logger.LogDebug("Authorization header injected.");

        return base.SendAsync(request, cancellationToken);
    }
}
