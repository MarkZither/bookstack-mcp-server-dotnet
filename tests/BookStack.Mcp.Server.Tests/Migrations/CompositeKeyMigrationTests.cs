#pragma warning disable CA2007

using FluentAssertions;

using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

using Npgsql;

using BookStack.Mcp.Server.Tests.Migrations.Infrastructure;

namespace BookStack.Mcp.Server.Tests.Migrations;

public sealed class CompositeKeyMigrationTests
{
    [ClassDataSource<PostgresMigrationContainer>(Shared = SharedType.PerTestSession)]
    public required PostgresMigrationContainer Postgres { get; init; }

    [ClassDataSource<SqlServerMigrationContainer>(Shared = SharedType.PerTestSession)]
    public required SqlServerMigrationContainer SqlServer { get; init; }

    [Test]
    public async Task Postgres_MigrationScript_AppliesCompositePrimaryKeyAndColumns()
    {
        var scriptPath = GetRepoFilePath("src/BookStack.Mcp.Server.Data.Postgres/Migrations/20260519_AddChunkingCompositeKey.sql");
        var migrationSql = await File.ReadAllTextAsync(scriptPath).ConfigureAwait(false);

        await using var conn = new NpgsqlConnection(Postgres.ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        const string baseline = """
            CREATE TABLE page_vectors (
                page_id integer PRIMARY KEY,
                slug text NOT NULL,
                title text NOT NULL,
                url text NOT NULL,
                excerpt text NOT NULL,
                updated_at timestamp with time zone NOT NULL,
                content_hash text NOT NULL
            );

            INSERT INTO page_vectors(page_id, slug, title, url, excerpt, updated_at, content_hash)
            VALUES (1, 's', 't', 'u', 'e', NOW(), 'h');
            """;

        await using (var cmd = new NpgsqlCommand(baseline, conn))
        {
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var cmd = new NpgsqlCommand(migrationSql, conn))
        {
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var cmd = new NpgsqlCommand(
            "SELECT chunk_index, total_chunks FROM page_vectors WHERE page_id = 1", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            (await reader.ReadAsync().ConfigureAwait(false)).Should().BeTrue();
            reader.GetInt32(0).Should().Be(0);
            reader.GetInt32(1).Should().Be(1);
        }

        await using (var cmd = new NpgsqlCommand(
            """
            SELECT a.attname
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(i.indkey)
            WHERE t.relname = 'page_vectors' AND i.indisprimary
            ORDER BY array_position(i.indkey, a.attnum);
            """,
            conn))
        {
            var cols = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                cols.Add(reader.GetString(0));
            }

            cols.Should().Equal("page_id", "chunk_index");
        }
    }

    [Test]
    public async Task Sqlite_MigrationScript_AppliesChunkColumnsAndIndexes()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"bookstack-vectors-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = tempPath }.ToString();

        try
        {
            var scriptPath = GetRepoFilePath("src/BookStack.Mcp.Server.Data.Sqlite/Migrations/20260519_AddChunkingCompositeKey.sql");
            var migrationSql = await File.ReadAllTextAsync(scriptPath).ConfigureAwait(false);

            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            const string baseline = """
                CREATE TABLE page_vectors (
                    PageId INTEGER PRIMARY KEY,
                    Slug TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    Url TEXT NOT NULL,
                    Excerpt TEXT NOT NULL,
                    UpdatedAtTicks INTEGER NOT NULL,
                    ContentHash TEXT NOT NULL,
                    Embedding BLOB
                );

                INSERT INTO page_vectors(PageId, Slug, Title, Url, Excerpt, UpdatedAtTicks, ContentHash, Embedding)
                VALUES (1, 's', 't', 'u', 'e', 1, 'h', NULL);
                """;

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = baseline;
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = migrationSql;
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT StorageKey, ChunkIndex, TotalChunks FROM page_vectors WHERE PageId = 1";
                await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                (await reader.ReadAsync().ConfigureAwait(false)).Should().BeTrue();
                reader.GetString(0).Should().Be("1:0");
                reader.GetInt32(1).Should().Be(0);
                reader.GetInt32(2).Should().Be(1);
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(page_vectors)";
                var pkColumns = new List<string>();
                await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    if (reader.GetInt32(reader.GetOrdinal("pk")) > 0)
                    {
                        pkColumns.Add(reader.GetString(reader.GetOrdinal("name")));
                    }
                }

                pkColumns.Should().ContainSingle().Which.Should().Be("StorageKey");
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Test]
    public async Task SqlServer_MigrationScript_AppliesCompositePrimaryKeyAndColumns()
    {
        var scriptPath = GetRepoFilePath("src/BookStack.Mcp.Server.Data.SqlServer/Migrations/20260519_AddChunkingCompositeKey.sql");
        var migrationSql = await File.ReadAllTextAsync(scriptPath).ConfigureAwait(false);

        await using var conn = new SqlConnection(SqlServer.ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        const string baseline = """
            IF OBJECT_ID('dbo.page_vectors', 'U') IS NOT NULL
                DROP TABLE dbo.page_vectors;

            CREATE TABLE dbo.page_vectors (
                page_id int NOT NULL PRIMARY KEY,
                slug nvarchar(512) NOT NULL,
                title nvarchar(512) NOT NULL,
                url nvarchar(1024) NOT NULL,
                excerpt nvarchar(2048) NOT NULL,
                updated_at datetimeoffset NOT NULL,
                content_hash nvarchar(64) NOT NULL
            );

            INSERT INTO dbo.page_vectors(page_id, slug, title, url, excerpt, updated_at, content_hash)
            VALUES (1, 's', 't', 'u', 'e', SYSUTCDATETIME(), 'h');
            """;

        await using (var cmd = new SqlCommand(baseline, conn))
        {
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var cmd = new SqlCommand(migrationSql, conn))
        {
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var cmd = new SqlCommand(
            "SELECT chunk_index, total_chunks FROM dbo.page_vectors WHERE page_id = 1", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            (await reader.ReadAsync().ConfigureAwait(false)).Should().BeTrue();
            reader.GetInt32(0).Should().Be(0);
            reader.GetInt32(1).Should().Be(1);
        }

        await using (var cmd = new SqlCommand(
            """
            SELECT c.name
            FROM sys.key_constraints kc
            JOIN sys.index_columns ic ON kc.parent_object_id = ic.object_id AND kc.unique_index_id = ic.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            JOIN sys.tables t ON t.object_id = kc.parent_object_id
            WHERE kc.type = 'PK' AND t.name = 'page_vectors'
            ORDER BY ic.key_ordinal;
            """,
            conn))
        {
            var cols = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                cols.Add(reader.GetString(0));
            }

            cols.Should().Equal("page_id", "chunk_index");
        }
    }

    private static string GetRepoFilePath(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "BookStack.Mcp.Server.sln");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test execution directory.");
    }
}
