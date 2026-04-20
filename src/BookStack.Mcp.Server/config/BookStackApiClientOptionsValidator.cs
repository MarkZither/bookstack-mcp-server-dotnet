using Microsoft.Extensions.Options;

namespace BookStack.Mcp.Server.Config;

public sealed class BookStackApiClientOptionsValidator : IValidateOptions<BookStackApiClientOptions>
{
    public ValidateOptionsResult Validate(string? name, BookStackApiClientOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            failures.Add($"{nameof(options.BaseUrl)} must not be empty.");
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add($"{nameof(options.BaseUrl)} must be a well-formed HTTP or HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(options.TokenId))
        {
            failures.Add($"{nameof(options.TokenId)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.TokenSecret))
        {
            failures.Add($"{nameof(options.TokenSecret)} must not be empty.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
