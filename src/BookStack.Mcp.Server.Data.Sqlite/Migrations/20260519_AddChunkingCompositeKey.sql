-- FEAT-0060 / #108
-- SQLite schema migration for chunk-aware vector storage.
-- SQLite cannot alter a primary key in place; recreate table.

BEGIN TRANSACTION;

CREATE TABLE IF NOT EXISTS page_vectors_new (
    StorageKey TEXT NOT NULL PRIMARY KEY,
    PageId INTEGER NOT NULL,
    ChunkIndex INTEGER NOT NULL DEFAULT 0,
    TotalChunks INTEGER NOT NULL DEFAULT 1,
    Slug TEXT NOT NULL,
    Title TEXT NOT NULL,
    Url TEXT NOT NULL,
    Excerpt TEXT NOT NULL,
    UpdatedAtTicks INTEGER NOT NULL,
    ContentHash TEXT NOT NULL,
    Embedding BLOB
);

INSERT INTO page_vectors_new (
    StorageKey,
    PageId,
    ChunkIndex,
    TotalChunks,
    Slug,
    Title,
    Url,
    Excerpt,
    UpdatedAtTicks,
    ContentHash,
    Embedding
)
SELECT
    CAST(PageId AS TEXT) || ':0' AS StorageKey,
    PageId,
    0 AS ChunkIndex,
    1 AS TotalChunks,
    Slug,
    Title,
    Url,
    Excerpt,
    UpdatedAtTicks,
    ContentHash,
    Embedding
FROM page_vectors;

DROP TABLE page_vectors;
ALTER TABLE page_vectors_new RENAME TO page_vectors;

CREATE INDEX IF NOT EXISTS ix_page_vectors_page_id ON page_vectors (PageId);
CREATE INDEX IF NOT EXISTS ix_page_vectors_page_id_chunk_index ON page_vectors (PageId, ChunkIndex);

COMMIT;
