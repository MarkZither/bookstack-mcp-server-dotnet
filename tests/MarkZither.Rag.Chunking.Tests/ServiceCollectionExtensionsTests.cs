using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace MarkZither.Rag.Chunking.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Test]
    public void AddChunking_RegistersEncoderAndChunkingService()
    {
        var services = new ServiceCollection();

        services.AddChunking();

        using var provider = services.BuildServiceProvider();
        provider.GetService<ITokenEncoder>().Should().BeOfType<TiktokenEncoder>();
        provider.GetService<IChunkingService>().Should().BeOfType<SlideWindowChunkingService>();
    }
}
