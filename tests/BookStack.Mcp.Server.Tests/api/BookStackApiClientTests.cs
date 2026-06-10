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
                        "created_by": 1,
                        "updated_by": 1,
                        "owned_by": 1,
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
                "created_by": {"id": 1, "name": "Admin", "slug": "admin", "avatar_url": ""},
                "updated_by": {"id": 1, "name": "Admin", "slug": "admin", "avatar_url": ""},
                "owned_by": {"id": 1, "name": "Admin", "slug": "admin", "avatar_url": ""},
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
                "created_by": {"id": 1, "name": "Admin", "slug": "admin", "avatar_url": ""},
                "updated_by": {"id": 1, "name": "Admin", "slug": "admin", "avatar_url": ""},
                "owned_by": {"id": 1, "name": "Admin", "slug": "admin", "avatar_url": ""},
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

    // ── Bug reproduction: Null created_by and owned_by ─────────────────────
    // GitHub Issue: When the BookStack API returns null for created_by or owned_by
    // (which can happen when a user account is deleted or in certain edge cases),
    // the client fails to deserialize the response because these fields are non-nullable int.
    // Expected behavior: These fields should be nullable int? to gracefully handle API edge cases.

    [Test]
    public async Task ListBooksAsync_WithNullCreatedBy_DeserializesSuccessfully()
    {
        // Scenario: BookStack API returns a book with null created_by (e.g., user was deleted).
        // With the fix (int? fields), this should deserialize successfully with null values.
        var json = """
            {
                "data": [
                    {
                        "id": 8,
                        "name": "Problematic Book",
                        "slug": "problematic-book",
                        "description": "Book with null ownership.",
                        "description_html": "<p>Book with null ownership.</p>",
                        "created_at": "2024-01-01T00:00:00Z",
                        "updated_at": "2024-01-02T00:00:00Z",
                        "created_by": null,
                        "updated_by": 1,
                        "owned_by": null,
                        "tags": []
                    }
                ],
                "total": 1
            }
            """;

        var handler = MockHttpMessageHandler.ReturningJson(json);
        var client = CreateClient(handler);

        var result = await client.ListBooksAsync().ConfigureAwait(false);

        var book = result.Data[0];
        await Assert.That(book.Id).IsEqualTo(8);
        await Assert.That(book.CreatedBy).IsNull();
        await Assert.That(book.OwnedBy).IsNull();
        await Assert.That(book.UpdatedBy).IsEqualTo(1);
    }

    [Test]
    public async Task GetBookAsync_WithNullCreatedByAndOwnedBy_DeserializesSuccessfully()
    {
        // Scenario: BookStack API returns a book detail with null user references.
        // The BookWithContents model with nullable UserSummary? properties should handle this.
        var json = """
            {
                "id": 9,
                "name": "Another Problematic Book",
                "slug": "another-problematic-book",
                "created_at": "2024-01-01T00:00:00Z",
                "updated_at": "2024-01-01T00:00:00Z",
                "created_by": null,
                "updated_by": null,
                "owned_by": null,
                "tags": [],
                "contents": []
            }
            """;
        var handler = MockHttpMessageHandler.ReturningJson(json);
        var client = CreateClient(handler);

        var book = await client.GetBookAsync(9).ConfigureAwait(false);

        await Assert.That(book.Id).IsEqualTo(9);
        // BookWithContents uses nullable UserSummary? which can be null
        await Assert.That(book.CreatedBy).IsNull();
        await Assert.That(book.OwnedBy).IsNull();
    }

    [Test]
    public async Task ListBooksAsync_WithMixedNullAndValidCreatedBy_HandlesEdgeCases()
    {
        // Real-world scenario: The API returns a mixed list where some books have valid users
        // and others have null (users deleted). This tests handling of multiple items.
        var json = """
            {
                "data": [
                    {
                        "id": 1,
                        "name": "Normal Book",
                        "slug": "normal-book",
                        "created_at": "2024-01-01T00:00:00Z",
                        "updated_at": "2024-01-01T00:00:00Z",
                        "created_by": 42,
                        "updated_by": 42,
                        "owned_by": 42,
                        "tags": []
                    },
                    {
                        "id": 2,
                        "name": "Orphaned Book",
                        "slug": "orphaned-book",
                        "created_at": "2024-01-01T00:00:00Z",
                        "updated_at": "2024-01-01T00:00:00Z",
                        "created_by": null,
                        "updated_by": null,
                        "owned_by": null,
                        "tags": []
                    }
                ],
                "total": 2
            }
            """;

        var handler = MockHttpMessageHandler.ReturningJson(json);
        var client = CreateClient(handler);

        var result = await client.ListBooksAsync().ConfigureAwait(false);

        await Assert.That(result.Total).IsEqualTo(2);
        await Assert.That(result.Data[0].CreatedBy).IsEqualTo(42);
        await Assert.That(result.Data[1].CreatedBy).IsNull();
    }
}
