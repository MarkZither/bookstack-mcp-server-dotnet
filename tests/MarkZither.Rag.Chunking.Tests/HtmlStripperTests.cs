using FluentAssertions;
using MarkZither.Rag.Chunking.Internal;

namespace MarkZither.Rag.Chunking.Tests;

public sealed class HtmlStripperTests
{
    [Test]
    public void Strip_RemovesScriptStyleAndTags()
    {
        var html = "<style>.a{color:red;}</style><p>Hello <strong>world</strong></p><script>alert(1)</script>";

        var stripped = HtmlStripper.Strip(html);

        stripped.Should().Be("Hello world");
    }

    [Test]
    public void Strip_RemovesDefaultDrawIoBlock()
    {
        var html = "<p>Before</p><div drawio-diagram=\"abc\"><img src=\"data:image/png;base64,AAAA\" /></div><p>After</p>";

        var stripped = HtmlStripper.Strip(html);

        stripped.Should().Be("Before After");
    }

    [Test]
    public void Strip_UsesCustomDrawIoPattern()
    {
        var html = "<p>One</p><figure data-diagram=\"true\">payload</figure><p>Two</p>";

        var stripped = HtmlStripper.Strip(html, @"<figure[^>]*data-diagram[^>]*>.*?</figure>");

        stripped.Should().Be("One Two");
    }
}
