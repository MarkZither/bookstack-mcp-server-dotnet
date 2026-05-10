using System.Text;
using System.Text.Json;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient : IBookStackApiClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<BookStackApiClient> _logger;

    public BookStackApiClient(
        HttpClient httpClient,
        IOptions<BookStackApiClientOptions> options,
        ILogger<BookStackApiClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/api/");
        _httpClient.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        _logger = logger;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Sending {Method} {Uri}", request.Method, request.RequestUri);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);

        return result!;
    }

    private async Task SendNoContentAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Sending {Method} {Uri}", request.Method, request.RequestUri);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Sending {Method} {Uri}", request.Method, request.RequestUri);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? errorMessage = null;
        string? errorCode = null;
        Dictionary<string, string[]>? validationErrors = null;

        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorElement))
            {
                errorMessage = errorElement.TryGetProperty("message", out var msg) ? msg.GetString() : null;
                errorCode = errorElement.TryGetProperty("code", out var code) ? code.GetRawText() : null;

                if (errorElement.TryGetProperty("validation", out var validation)
                    && validation.ValueKind == JsonValueKind.Object)
                {
                    validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (var field in validation.EnumerateObject())
                    {
                        var messages = field.Value.ValueKind == JsonValueKind.Array
                            ? field.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray()
                            : [field.Value.GetString() ?? string.Empty];
                        validationErrors[field.Name] = messages;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body; leave message/code null
        }

        throw new BookStackApiException((int)response.StatusCode, errorMessage, errorCode, validationErrors);
    }

    private static string GetExportUrlSegment(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Html => "html",
            ExportFormat.Pdf => "pdf",
            ExportFormat.Plaintext => "plaintext",
            ExportFormat.Markdown => "markdown",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    private static string GetContentTypePath(ContentType contentType)
    {
        return contentType switch
        {
            ContentType.Book => "books",
            ContentType.Chapter => "chapters",
            ContentType.Page => "pages",
            ContentType.Bookshelf => "shelves",
            _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, null),
        };
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, _jsonOptions),
                Encoding.UTF8,
                "application/json");
        }

        return request;
    }

    private static string BuildQueryString(ListQueryParams? query)
    {
        if (query is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (query.Count.HasValue)
        {
            parts.Add($"count={query.Count.Value}");
        }

        if (query.Offset.HasValue)
        {
            parts.Add($"offset={query.Offset.Value}");
        }

        if (!string.IsNullOrEmpty(query.Sort))
        {
            parts.Add($"sort={Uri.EscapeDataString(query.Sort)}");
        }

        return parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
    }
}
