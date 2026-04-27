using Microsoft.EntityFrameworkCore;

namespace BookStack.Mcp.Server.Data.Sqlite;

public sealed class SyncMetadataDbContext : DbContext
{
    public DbSet<SyncMetadataRecord> SyncMetadata => Set<SyncMetadataRecord>();

    public SyncMetadataDbContext(DbContextOptions<SyncMetadataDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncMetadataRecord>(b =>
        {
            b.ToTable("sync_metadata");
            b.HasKey(e => e.Key);
            b.Property(e => e.Key).HasMaxLength(64);
            b.Property(e => e.Value).HasMaxLength(256);
        });
    }
}

public sealed class SyncMetadataRecord
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
