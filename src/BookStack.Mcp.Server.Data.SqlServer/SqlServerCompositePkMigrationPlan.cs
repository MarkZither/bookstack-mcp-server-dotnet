namespace BookStack.Mcp.Server.Data.SqlServer;

/// <summary>
/// Tracks FEAT-0060 Phase 2 schema-parity requirements for SQL Server.
/// This placeholder prevents SQL Server scope from being omitted while
/// Postgres/SQLite composite key migrations are implemented.
/// </summary>
public static class SqlServerCompositePkMigrationPlan
{
    public const string TrackingIssue = "#108";

    public const string TargetMilestoneVersion = "0.5.0";

    public const string MigrationScriptPath =
        "src/BookStack.Mcp.Server.Data.SqlServer/Migrations/20260519_AddChunkingCompositeKey.sql";

    public const string TableName = "page_vectors";

    public const string CompositePrimaryKey = "(page_id, chunk_index)";

    public const string RequiredColumns = "chunk_index, total_chunks";
}
