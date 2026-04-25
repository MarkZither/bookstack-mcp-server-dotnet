namespace BookStack.Mcp.Server.Config;

internal static class ScopeFilter
{
    internal static bool MatchesScope(int id, string slug, IReadOnlyList<string> scope)
    {
        foreach (var entry in scope)
        {
            if (int.TryParse(entry, out var scopeId) && scopeId == id)
                return true;
            if (string.Equals(entry, slug, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
