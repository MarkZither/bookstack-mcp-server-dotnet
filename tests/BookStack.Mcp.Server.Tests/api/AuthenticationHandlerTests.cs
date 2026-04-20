using System.Net;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BookStack.Mcp.Server.Tests.Api;

public sealed class AuthenticationHandlerTests
{
    private static AuthenticationHandler CreateHandler(
        string tokenId,
        string tokenSecret,
        ILogger<AuthenticationHandler>? logger = null)
    {
        var options = Options.Create(new BookStackApiClientOptions
        {
            BaseUrl = "https://bookstack.example.com",
            TokenId = tokenId,
            TokenSecret = tokenSecret,
        });
        return new AuthenticationHandler(options, logger ?? NullLogger<AuthenticationHandler>.Instance);
    }

    [Test]
    public async Task SendAsync_AddsAuthorizationHeader()
    {
        var mockHandler = MockHttpMessageHandler.ReturningStatus(HttpStatusCode.OK);
        var handler = CreateHandler("myid", "mysecret");
        handler.InnerHandler = mockHandler;

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };
        await httpClient.GetAsync("resource").ConfigureAwait(false);

        var authHeader = mockHandler.LastRequest!.Headers.Authorization;
        var scheme = authHeader?.Scheme;
        await Assert.That(scheme).IsEqualTo("Token");

        var parameter = authHeader?.Parameter;
        await Assert.That(parameter).IsEqualTo("myid:mysecret");
    }

    [Test]
    public async Task SendAsync_TokenInHeader_MatchesOptions()
    {
        const string expectedId = "token-id-123";
        const string expectedSecret = "secret-abc";

        var mockHandler = MockHttpMessageHandler.ReturningStatus(HttpStatusCode.OK);
        var handler = CreateHandler(expectedId, expectedSecret);
        handler.InnerHandler = mockHandler;

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };
        await httpClient.GetAsync("resource").ConfigureAwait(false);

        var parameter = mockHandler.LastRequest!.Headers.Authorization?.Parameter;
        var expected = $"{expectedId}:{expectedSecret}";
        await Assert.That(parameter).IsEqualTo(expected);
    }

    [Test]
    public async Task SendAsync_DoesNotLogTokenSecret()
    {
        const string secret = "super-secret-value";
        var mockLogger = new Mock<ILogger<AuthenticationHandler>>();

        var mockHandler = MockHttpMessageHandler.ReturningStatus(HttpStatusCode.OK);
        var handler = CreateHandler("tid", secret, mockLogger.Object);
        handler.InnerHandler = mockHandler;

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };
        await httpClient.GetAsync("resource").ConfigureAwait(false);

        mockLogger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(secret)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "Token secret must never appear in log messages.");
    }

    [Test]
    public async Task SendAsync_DoesNotLogTokenId()
    {
        const string tokenId = "identifiable-token-id";
        var mockLogger = new Mock<ILogger<AuthenticationHandler>>();

        var mockHandler = MockHttpMessageHandler.ReturningStatus(HttpStatusCode.OK);
        var handler = CreateHandler(tokenId, "secret", mockLogger.Object);
        handler.InnerHandler = mockHandler;

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };
        await httpClient.GetAsync("resource").ConfigureAwait(false);

        mockLogger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(tokenId)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "Token ID must never appear in log messages.");
    }

    [Test]
    public async Task SendAsync_ForwardsRequestToInnerHandler()
    {
        var mockHandler = MockHttpMessageHandler.ReturningStatus(HttpStatusCode.OK);
        var handler = CreateHandler("id", "secret");
        handler.InnerHandler = mockHandler;

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };
        await httpClient.GetAsync("api/books").ConfigureAwait(false);

        var requestCount = mockHandler.Requests.Count;
        await Assert.That(requestCount).IsEqualTo(1);
    }
}
