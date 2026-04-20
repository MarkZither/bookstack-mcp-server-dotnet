using BookStack.Mcp.Server.Config;
using Microsoft.Extensions.Options;

namespace BookStack.Mcp.Server.Tests.Api;

public sealed class BookStackApiClientOptionsValidatorTests
{
    private static BookStackApiClientOptions ValidOptions() =>
        new()
        {
            BaseUrl = "https://bookstack.example.com",
            TokenId = "myTokenId",
            TokenSecret = "myTokenSecret",
            TimeoutSeconds = 30,
        };

    [Test]
    public async Task Validate_WithValidOptions_ReturnsSuccess()
    {
        var validator = new BookStackApiClientOptionsValidator();
        var result = validator.Validate(null, ValidOptions());
        var succeeded = result.Succeeded;
        await Assert.That(succeeded).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyBaseUrl_ReturnsFailed()
    {
        var options = ValidOptions();
        options.BaseUrl = string.Empty;
        var validator = new BookStackApiClientOptionsValidator();
        var result = validator.Validate(null, options);
        var failed = result.Failed;
        await Assert.That(failed).IsTrue();
    }

    [Test]
    public async Task Validate_WithNonHttpBaseUrl_ReturnsFailed()
    {
        var options = ValidOptions();
        options.BaseUrl = "ftp://example.com";
        var validator = new BookStackApiClientOptionsValidator();
        var result = validator.Validate(null, options);
        var failed = result.Failed;
        await Assert.That(failed).IsTrue();
    }

    [Test]
    public async Task Validate_WithHttpBaseUrl_ReturnsSuccess()
    {
        var options = ValidOptions();
        options.BaseUrl = "http://localhost:8080";
        var validator = new BookStackApiClientOptionsValidator();
        var result = validator.Validate(null, options);
        var succeeded = result.Succeeded;
        await Assert.That(succeeded).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyTokenId_ReturnsFailed()
    {
        var options = ValidOptions();
        options.TokenId = string.Empty;
        var validator = new BookStackApiClientOptionsValidator();
        var result = validator.Validate(null, options);
        var failed = result.Failed;
        await Assert.That(failed).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyTokenSecret_ReturnsFailed()
    {
        var options = ValidOptions();
        options.TokenSecret = string.Empty;
        var validator = new BookStackApiClientOptionsValidator();
        var result = validator.Validate(null, options);
        var failed = result.Failed;
        await Assert.That(failed).IsTrue();
    }

    [Test]
    public async Task Validate_WithMultipleFailures_ReturnsAllFailures()
    {
        var options = new BookStackApiClientOptions
        {
            BaseUrl = string.Empty,
            TokenId = string.Empty,
            TokenSecret = string.Empty,
        };
        var validator = new BookStackApiClientOptionsValidator();
        var result = validator.Validate(null, options);
        var failed = result.Failed;
        await Assert.That(failed).IsTrue();
        var failureCount = result.Failures?.Count() ?? 0;
        await Assert.That(failureCount).IsGreaterThanOrEqualTo(2);
    }
}
