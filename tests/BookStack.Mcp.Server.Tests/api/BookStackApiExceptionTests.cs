using BookStack.Mcp.Server.Api;

namespace BookStack.Mcp.Server.Tests.Api;

public sealed class BookStackApiExceptionTests
{
    [Test]
    public async Task Constructor_SetsStatusCode()
    {
        var ex = new BookStackApiException(404, "Not found", "not_found");
        var statusCode = ex.StatusCode;
        await Assert.That(statusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Constructor_SetsErrorMessage()
    {
        var ex = new BookStackApiException(422, "Validation failed", null);
        var message = ex.ErrorMessage;
        await Assert.That(message).IsEqualTo("Validation failed");
    }

    [Test]
    public async Task Constructor_SetsErrorCode()
    {
        var ex = new BookStackApiException(403, null, "forbidden");
        var code = ex.ErrorCode;
        await Assert.That(code).IsEqualTo("forbidden");
    }

    [Test]
    public async Task Constructor_AcceptsNullMessageAndCode()
    {
        var ex = new BookStackApiException(500, null, null);
        await Assert.That(ex.ErrorMessage).IsNull();
        await Assert.That(ex.ErrorCode).IsNull();
    }

    [Test]
    public async Task Message_ContainsStatusCode()
    {
        var ex = new BookStackApiException(401, "Unauthorized", null);
        var message = ex.Message;
        await Assert.That(message).Contains("401");
    }

    [Test]
    public async Task IsException_IsTrue()
    {
        var ex = new BookStackApiException(500, null, null);
        var isException = ex is Exception;
        await Assert.That(isException).IsTrue();
    }
}
