using System.Net;
using System.Text.RegularExpressions;

namespace MarkZither.Rag.Chunking.Internal;

internal static partial class HtmlStripper
{
    internal const string DefaultDrawIoPattern = @"<div[^>]+drawio-diagram[^>]*>.*?</div>";

    private static readonly Regex _scriptRegex = new(
        @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex _styleRegex = new(
        @"<style\b[^<]*(?:(?!<\/style>)<[^<]*)*<\/style>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex _tagRegex = new(
        @"<[^>]+>",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex _whitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    public static string Strip(string html, string? drawIoPattern = null)
    {
        ArgumentNullException.ThrowIfNull(html);

        if (html.Length == 0)
        {
            return string.Empty;
        }

        var drawRegex = CreateDrawIoRegex(drawIoPattern);
        var text = drawRegex.Replace(html, " ");
        text = _scriptRegex.Replace(text, " ");
        text = _styleRegex.Replace(text, " ");
        text = _tagRegex.Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = _whitespaceRegex.Replace(text, " ");

        return text.Trim();
    }

    private static Regex CreateDrawIoRegex(string? drawIoPattern)
    {
        if (string.IsNullOrWhiteSpace(drawIoPattern))
        {
            return DefaultDrawIoRegex();
        }

        try
        {
            return new Regex(
                drawIoPattern,
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled,
                TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return DefaultDrawIoRegex();
        }
    }

    [GeneratedRegex(DefaultDrawIoPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DefaultDrawIoRegex();
}
