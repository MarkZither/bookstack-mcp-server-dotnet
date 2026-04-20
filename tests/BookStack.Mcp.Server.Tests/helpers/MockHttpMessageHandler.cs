using System.Net;

namespace BookStack.Mcp.Server.Tests.Helpers;

internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public HttpRequestMessage? LastRequest { get; private set; }

    public IList<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = req => Task.FromResult(handler(req));
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public static MockHttpMessageHandler ReturningJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(status)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
    }

    public static MockHttpMessageHandler ReturningStatus(HttpStatusCode status)
    {
        return new MockHttpMessageHandler(_ => new HttpResponseMessage(status));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        Requests.Add(request);
        return await _handler(request).ConfigureAwait(false);
    }
}
