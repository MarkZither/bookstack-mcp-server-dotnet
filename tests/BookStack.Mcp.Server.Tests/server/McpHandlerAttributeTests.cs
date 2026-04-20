using System.Reflection;
using BookStack.Mcp.Server.Tools.Books;
using ModelContextProtocol.Server;
using TUnit.Core;

namespace BookStack.Mcp.Server.Tests.Server;

public sealed class McpHandlerAttributeTests
{
    [Test]
    public async Task BookToolHandler_IsDecoratedWithMcpServerToolTypeAttribute()
    {
        var hasAttr = typeof(BookToolHandler)
            .GetCustomAttribute<McpServerToolTypeAttribute>() is not null;

        var result = hasAttr;
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ServerAssembly_ContainsAtLeastFourteenToolHandlerTypes()
    {
        var assembly = typeof(BookToolHandler).Assembly;
        var toolTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .ToList();

        var count = toolTypes.Count;
        await Assert.That(count).IsGreaterThanOrEqualTo(14);
    }

    [Test]
    public async Task ServerAssembly_ContainsAtLeastSixResourceHandlerTypes()
    {
        var assembly = typeof(BookToolHandler).Assembly;
        var resourceTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerResourceTypeAttribute>() is not null)
            .ToList();

        var count = resourceTypes.Count;
        await Assert.That(count).IsGreaterThanOrEqualTo(6);
    }

    [Test]
    public async Task AllToolMethods_HaveMcpServerToolAttributeWithNonEmptyName()
    {
        var assembly = typeof(BookToolHandler).Assembly;
        var toolHandlerTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null);

        foreach (var type in toolHandlerTypes)
        {
            var toolMethods = type.GetMethods(
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
                .ToList();

            var hasToolMethods = toolMethods.Count > 0;
            await Assert.That(hasToolMethods).IsTrue();

            foreach (var method in toolMethods)
            {
                var attr = method.GetCustomAttribute<McpServerToolAttribute>()!;
                var hasName = !string.IsNullOrWhiteSpace(attr.Name);
                await Assert.That(hasName).IsTrue();
            }
        }
    }
}
