using System.Net;
using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BookStack.Mcp.Server.Tests.Api;

public sealed class BookStackApiClientTests
{
    private static BookStackApiClient CreateClient(MockHttpMessageHandler handler)
    {
        var options = Options.Create(new BookStackApiClientOptions
        {
            BaseUrl = "http://bookstack.test",
            TokenId = "tid",
            TokenSecret = "tsecret",
            TimeoutSeconds = 30,
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://bookstack.test/api/"),
        };

        return new BookStackApiClient(httpClient, options, NullLogger<BookStackApiClient>.Instance);
    }

    // ── Error handling ─────────────────────────────────────────────────────

    [Test]
    public async Task SendAsync_On404_ThrowsBookStackApiException()
    {
        var json = """{"error":{"code":404,"message":"Entity not found."}}""";
        var handler = MockHttpMessageHandler.ReturningJson(json, HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<BookStackApiException>(
            async () => await client.GetBookAsync(999).ConfigureAwait(false));
    }

    [Test]
    public async Task SendAsync_On404_ExceptionHasCorrectStatusCode()
    {
        var json = """{"error":{"code":404,"message":"Entity not found."}}""";
        var handler = MockHttpMessageHandler.ReturningJson(json, HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<BookStackApiException>(
            async () => await client.GetBookAsync(999).ConfigureAwait(false));

        var statusCode = ex!.StatusCode;
        await Assert.That(statusCode).IsEqualTo(404);
    }

    [Test]
    public async Task SendAsync_On404_ExceptionHasErrorMessage()
    {
        var json = """{"error":{"code":404,"message":"Entity not found."}}""";
        var handler = MockHttpMessageHandler.ReturningJson(json, HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<BookStackApiException>(
            async () => await client.GetBookAsync(999).ConfigureAwait(false));

        var errorMessage = ex!.ErrorMessage;
        await Assert.That(errorMessage).IsEqualTo("Entity not found.");
    }

    [Test]
    public async Task SendAsync_OnNonJsonErrorBody_ThrowsBookStackApiException()
    {
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal Server Error"),
            });
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<BookStackApiException>(
            async () => await client.GetBookAsync(1).ConfigureAwait(false));

        var statusCode = ex!.StatusCode;
        await Assert.That(statusCode).IsEqualTo(500);
    }

    // ── JSON deserialization ────────────────────────────────────────────────

    [Test]
    public async Task ListBooksAsync_DeserializesSnakeCaseFields()
    {
        var json = """
            {
                "data": [
                    {
                        "id": 42,
                        "name": "Test Book",
                        "slug": "test-book",
                        "description": "A test.",
                        "description_html": "<p>A test.</p>",
                        "created_at": "2024-01-01T00:00:00Z",
                        "updated_at": "2024-01-02T00:00:00Z",
                        "created_by": {"id": 1, "name": "Admin", "slug": "admin"},
                        "updated_by": {"id": 1, "name": "Admin", "slug": "admin"},
                        "owned_by": {"id": 1, "name": "Admin", "slug": "admin"},
                        "tags": []
                    }
                ],
                "total": 1
            }
            """;

        var handler = MockHttpMessageHandler.ReturningJson(json);
        var client = CreateClient(handler);

        var result = await client.ListBooksAsync().ConfigureAwait(false);

        var total = result.Total;
        await Assert.That(total).IsEqualTo(1);

        var bookId = result.Data[0].Id;
        await Assert.That(bookId).IsEqualTo(42);

        var bookName = result.Data[0].Name;
        await Assert.That(bookName).IsEqualTo("Test Book");
    }

    [Test]
    public async Task GetBookAsync_DeserializesBookId()
    {
        var json = """
            {
                "id": 7,
                "name": "My Book",
                "slug": "my-book",
                "created_at": "2024-01-01T00:00:00Z",
                "updated_at": "2024-01-01T00:00:00Z",
                "created_by": {"id": 1, "name": "Admin", "slug": "admin"},
                "updated_by": {"id": 1, "name": "Admin", "slug": "admin"},
                "owned_by": {"id": 1, "name": "Admin", "slug": "admin"},
                "tags": [],
                "contents": []
            }
            """;

        var handler = MockHttpMessageHandler.ReturningJson(json);
        var client = CreateClient(handler);

        var book = await client.GetBookAsync(7).ConfigureAwait(false);

        var bookId = book.Id;
        await Assert.That(bookId).IsEqualTo(7);
    }

    // ── Request URL routing ────────────────────────────────────────────────

    [Test]
    public async Task ListBooksAsync_CallsCorrectEndpoint()
    {
        var json = """{"data":[],"total":0}""";
        var handler = MockHttpMessageHandler.ReturningJson(json);
        var client = CreateClient(handler);

        await client.ListBooksAsync().ConfigureAwait(false);

        var requestUri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        await Assert.That(requestUri).Contains("books");
    }

    [Test]
    public async Task GetBookAsync_CallsCorrectEndpointWithId()
    {
        var json = """
            {
                "id": 5,
                "name": "Book",
                "slug": "book",
                "created_at": "2024-01-01T00:00:00Z",
                "updated_at": "2024-01-01T00:00:00Z",
                "created_by": {"id": 1, "name": "Admin", "slug": "admin"},
                "updated_by": {"id": 1, "name": "Admin", "slug": "admin"},
                "owned_by": {"id": 1, "name": "Admin", "slug": "admin"},
                "tags": [],
                "contents": []
            }
            """;
        var handler = MockHttpMessageHandler.ReturningJson(json);
        var client = CreateClient(handler);

        await client.GetBookAsync(5).ConfigureAwait(false);

        var requestUri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        await Assert.That(requestUri).Contains("books/5");
    }

    [Test]
    public async Task ListBooksAsync_WithQueryParams_AppendsQueryString()
    {
        var json = """{"data":[],"total":0}""";
        var handler = MockHttpMessageHandler.ReturningJson(json);
        var client = CreateClient(handler);

        await client.ListBooksAsync(new ListQueryParams { Count = 10, Offset = 20 }).ConfigureAwait(false);

        var query = handler.LastRequest!.RequestUri!.Query;
        await Assert.That(query).Contains("count=10");
        await Assert.That(query).Contains("offset=20");
    }

    [Test]
    public async Task DeleteBookAsync_UsesDeleteMethod()
    {
        var handler = MockHttpMessageHandler.ReturningStatus(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        await client.DeleteBookAsync(3).ConfigureAwait(false);

        var method = handler.LastRequest!.Method.Method;
        await Assert.That(method).IsEqualTo("DELETE");
    }
}
