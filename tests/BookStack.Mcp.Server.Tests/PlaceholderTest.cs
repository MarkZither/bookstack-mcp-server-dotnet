namespace BookStack.Mcp.Server.Tests;

public sealed class PlaceholderTest
{
    [Test]
    public async Task Placeholder_AlwaysPasses()
    {
        var value = true;
        await Assert.That(value).IsTrue();
    }
}
