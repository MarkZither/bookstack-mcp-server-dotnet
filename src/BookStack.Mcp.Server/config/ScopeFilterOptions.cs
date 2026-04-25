namespace BookStack.Mcp.Server.Config;

public sealed class ScopeFilterOptions
{
    public IReadOnlyList<string> ScopedShelves { get; set; } = [];
    public IReadOnlyList<string> ScopedBooks { get; set; } = [];

    public bool HasBookScope => ScopedBooks.Count > 0;
    public bool HasShelfScope => ScopedShelves.Count > 0;
}
