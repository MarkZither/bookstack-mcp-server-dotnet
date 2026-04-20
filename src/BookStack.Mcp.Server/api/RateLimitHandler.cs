using Microsoft.Extensions.Logging;

namespace BookStack.Mcp.Server.Api;

public sealed class RateLimitHandler : DelegatingHandler
{
    private readonly ILogger<RateLimitHandler> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _resetDeadline = DateTimeOffset.MinValue;

    public RateLimitHandler(ILogger<RateLimitHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_resetDeadline > now)
            {
                var delay = _resetDeadline - now;
                _logger.LogInformation("Rate limit active; delaying {Delay}ms.", delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        ReadRateLimitHeaders(response);

        return response;
    }

    private void ReadRateLimitHeaders(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues)
            || !response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues))
        {
            _logger.LogWarning("Rate limit headers missing from response.");
            return;
        }

        var remainingStr = System.Linq.Enumerable.FirstOrDefault(remainingValues);
        var resetStr = System.Linq.Enumerable.FirstOrDefault(resetValues);

        if (!int.TryParse(remainingStr, out var remaining)
            || !long.TryParse(resetStr, out var resetEpoch))
        {
            _logger.LogWarning("Rate limit headers could not be parsed: Remaining={Remaining}, Reset={Reset}.",
                remainingStr, resetStr);
            return;
        }

        if (remaining == 0)
        {
            _resetDeadline = DateTimeOffset.FromUnixTimeSeconds(resetEpoch);
            _logger.LogInformation("Rate limit exhausted; next request gated until {ResetDeadline}.", _resetDeadline);
        }
        else
        {
            _resetDeadline = DateTimeOffset.MinValue;
        }
    }
}
