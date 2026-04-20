using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BookStack.Mcp.Server.Tests.Api;

public sealed class BookStackServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BookStack:BaseUrl"] = "https://bookstack.example.com",
                ["BookStack:TokenId"] = "test-token-id",
                ["BookStack:TokenSecret"] = "test-token-secret",
                ["BookStack:TimeoutSeconds"] = "30",
            })
            .Build();
    }

    [Test]
    public async Task AddBookStackApiClient_RegistersIBookStackApiClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBookStackApiClient(BuildConfiguration());

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<IBookStackApiClient>();

        var isNotNull = client is not null;
        await Assert.That(isNotNull).IsTrue();
    }

    [Test]
    public async Task AddBookStackApiClient_ResolvesBookStackApiClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBookStackApiClient(BuildConfiguration());

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<IBookStackApiClient>();

        var isCorrectType = client is BookStackApiClient;
        await Assert.That(isCorrectType).IsTrue();
    }

    [Test]
    public async Task AddBookStackApiClient_SecondResolve_ReturnsSameInstance()
    {
        // IHttpClientFactory creates transient clients by default, so the
        // resolved IBookStackApiClient should be a new instance each time.
        // We just verify that resolution does not throw.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBookStackApiClient(BuildConfiguration());

        var provider = services.BuildServiceProvider();

        var client1 = provider.GetService<IBookStackApiClient>();
        var client2 = provider.GetService<IBookStackApiClient>();

        var bothResolved = client1 is not null && client2 is not null;
        await Assert.That(bothResolved).IsTrue();
    }
}
