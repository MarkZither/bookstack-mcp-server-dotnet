using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookStack.Mcp.Server.Evaluation;

// Refs: FEAT-0060 Phase 3 — Req 4
// Minimal MCP Streamable HTTP client for the evaluation harness.
// Handles both application/json and text/event-stream (SSE) responses.
public sealed class McpHttpClient : IDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private int _nextId = 1;

    public McpHttpClient(string mcpBaseUrl, string? authToken = null)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(mcpBaseUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(60),
        };
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/event-stream"));

        if (!string.IsNullOrWhiteSpace(authToken))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", authToken);
        }
    }

    /// <summary>
    /// Calls <c>bookstack_semantic_search</c> and returns the ranked page slugs with scores.
    /// Slug is extracted from the page URL (last path segment).
    /// </summary>
    public async Task<IReadOnlyList<RankedPage>> CallSemanticSearchAsync(
        string query,
        int topN,
        CancellationToken ct)
    {
        var arguments = new { query, topN };
        var resultText = await CallToolRawAsync("bookstack_semantic_search", arguments, ct)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(resultText) || resultText == "[]")
        {
            return [];
        }

        var items = JsonSerializer.Deserialize<List<SemanticSearchItem>>(resultText, _jsonOptions)
            ?? [];

        var ranked = new List<RankedPage>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var slug = ExtractSlugFromUrl(items[i].Url);
            ranked.Add(new RankedPage(slug, items[i].Score, i + 1));
        }

        return ranked.AsReadOnly();
    }

    private async Task<string?> CallToolRawAsync(
        string toolName,
        object arguments,
        CancellationToken ct)
    {
        var id = _nextId++;
        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            @params = new { name = toolName, arguments },
            id,
        };

        using var content = JsonContent.Create(request);
        using var response = await _http.PostAsync("/mcp", content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

        var jsonBody = mediaType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase)
            ? ExtractJsonFromSse(body)
            : body;

        if (string.IsNullOrWhiteSpace(jsonBody))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(jsonBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out _))
        {
            return null;
        }

        if (!root.TryGetProperty("result", out var result))
        {
            return null;
        }

        if (!result.TryGetProperty("content", out var content2))
        {
            return null;
        }

        foreach (var item in content2.EnumerateArray())
        {
            if (item.TryGetProperty("type", out var type) &&
                type.GetString() == "text" &&
                item.TryGetProperty("text", out var text))
            {
                return text.GetString();
            }
        }

        return null;
    }

    private static string? ExtractJsonFromSse(string sseBody)
    {
        foreach (var line in sseBody.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("data:", StringComparison.Ordinal))
            {
                return trimmed["data:".Length..].Trim();
            }
        }

        return null;
    }

    private static string ExtractSlugFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var lastSlash = url.LastIndexOf('/');
        return lastSlash >= 0 ? url[(lastSlash + 1)..] : url;
    }

    public void Dispose() => _http.Dispose();

    private sealed record SemanticSearchItem(
        int PageId,
        string Title,
        string Url,
        string Excerpt,
        float Score);
}
