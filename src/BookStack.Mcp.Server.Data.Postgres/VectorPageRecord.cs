using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace BookStack.Mcp.Server.Data.Postgres;

public sealed class VectorPageRecord
{
    public int PageId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public Vector? Embedding { get; set; }
}

public sealed class VectorPageRecordConfiguration : IEntityTypeConfiguration<VectorPageRecord>
{
    public void Configure(EntityTypeBuilder<VectorPageRecord> builder)
    {
        builder.ToTable("page_vectors");
        builder.HasKey(e => e.PageId);
        builder.Property(e => e.Slug).HasMaxLength(512);
        builder.Property(e => e.Title).HasMaxLength(512);
        builder.Property(e => e.Url).HasMaxLength(1024);
        builder.Property(e => e.Excerpt).HasMaxLength(2048);
        builder.Property(e => e.ContentHash).HasMaxLength(64);
        builder.Property(e => e.Embedding).HasColumnType("vector(768)");

        builder.HasIndex(e => e.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
