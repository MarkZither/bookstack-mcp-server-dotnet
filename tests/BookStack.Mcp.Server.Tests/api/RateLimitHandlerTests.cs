using System.Net;
using System.Net.Http.Headers;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookStack.Mcp.Server.Tests.Api;

public sealed class RateLimitHandlerTests
{
    private static HttpResponseMessage ResponseWithRateLimitHeaders(int remaining, long resetEpochSeconds)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-RateLimit-Remaining", remaining.ToString());
        response.Headers.Add("X-RateLimit-Reset", resetEpochSeconds.ToString());
        return response;
    }

    [Test]
    public async Task SendAsync_ForwardsRequest()
    {
        var resetEpoch = DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds();
        var mockHandler = new MockHttpMessageHandler(_ => ResponseWithRateLimitHeaders(10, resetEpoch));
        var rateLimitHandler = new RateLimitHandler(NullLogger<RateLimitHandler>.Instance)
        {
            InnerHandler = mockHandler,
        };

        using var httpClient = new HttpClient(rateLimitHandler) { BaseAddress = new Uri("http://test/") };
        await httpClient.GetAsync("api/books").ConfigureAwait(false);

        var requestCount = mockHandler.Requests.Count;
        await Assert.That(requestCount).IsEqualTo(1);
    }

    [Test]
    public async Task SendAsync_WhenHeadersMissing_DoesNotThrow()
    {
        var mockHandler = MockHttpMessageHandler.ReturningStatus(HttpStatusCode.OK);
        var rateLimitHandler = new RateLimitHandler(NullLogger<RateLimitHandler>.Instance)
        {
            InnerHandler = mockHandler,
        };

        using var httpClient = new HttpClient(rateLimitHandler) { BaseAddress = new Uri("http://test/") };
        await httpClient.GetAsync("api/books").ConfigureAwait(false);

        var requestCount = mockHandler.Requests.Count;
        await Assert.That(requestCount).IsEqualTo(1);
    }

    [Test]
    public async Task SendAsync_WhenRemainingIsPositive_AllowsImmediateSubsequentRequest()
    {
        var resetEpoch = DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds();
        var callCount = 0;
        var mockHandler = new MockHttpMessageHandler(_ =>
        {
            callCount++;
            return ResponseWithRateLimitHeaders(5, resetEpoch);
        });

        var rateLimitHandler = new RateLimitHandler(NullLogger<RateLimitHandler>.Instance)
        {
            InnerHandler = mockHandler,
        };

        using var httpClient = new HttpClient(rateLimitHandler) { BaseAddress = new Uri("http://test/") };
        await httpClient.GetAsync("api/books").ConfigureAwait(false);
        await httpClient.GetAsync("api/shelves").ConfigureAwait(false);

        var count = callCount;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task SendAsync_WhenRemainingIsZeroAndResetInPast_DoesNotDelay()
    {
        // Reset time in the past — no actual delay should occur
        var pastEpoch = DateTimeOffset.UtcNow.AddSeconds(-30).ToUnixTimeSeconds();
        var mockHandler = new MockHttpMessageHandler(_ => ResponseWithRateLimitHeaders(0, pastEpoch));

        var rateLimitHandler = new RateLimitHandler(NullLogger<RateLimitHandler>.Instance)
        {
            InnerHandler = mockHandler,
        };

        using var httpClient = new HttpClient(rateLimitHandler) { BaseAddress = new Uri("http://test/") };

        // First call sets resetDeadline (past)
        await httpClient.GetAsync("api/books").ConfigureAwait(false);

        // Second call should not delay since deadline is in the past
        var start = DateTimeOffset.UtcNow;
        await httpClient.GetAsync("api/shelves").ConfigureAwait(false);
        var elapsed = DateTimeOffset.UtcNow - start;

        var elapsedMs = (int)elapsed.TotalMilliseconds;
        await Assert.That(elapsedMs).IsLessThan(5000);
    }

    [Test]
    public async Task SendAsync_ReturnsResponseFromInnerHandler()
    {
        var mockHandler = new MockHttpMessageHandler(_ => ResponseWithRateLimitHeaders(10, 0));
        var rateLimitHandler = new RateLimitHandler(NullLogger<RateLimitHandler>.Instance)
        {
            InnerHandler = mockHandler,
        };

        using var httpClient = new HttpClient(rateLimitHandler) { BaseAddress = new Uri("http://test/") };
        var response = await httpClient.GetAsync("api/books").ConfigureAwait(false);

        var statusCode = (int)response.StatusCode;
        await Assert.That(statusCode).IsEqualTo(200);
    }
}
