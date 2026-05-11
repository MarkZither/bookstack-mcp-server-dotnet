namespace BookStack.Mcp.Server.Config;

/// <summary>
/// Holds the resolved absolute path to the SQLite vector database file.
/// Registered as a singleton in DI only when the SQLite vector store provider is active.
/// </summary>
internal sealed record SqliteVectorDbPath(string Value);
