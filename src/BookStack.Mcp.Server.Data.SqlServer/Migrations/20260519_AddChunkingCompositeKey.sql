-- FEAT-0060 / #108
-- SQL Server schema parity migration script for chunk-aware vector storage.
-- Included to avoid provider drift while SQL Server vector store support evolves.

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.page_vectors', 'chunk_index') IS NULL
BEGIN
    ALTER TABLE dbo.page_vectors
        ADD chunk_index int NOT NULL CONSTRAINT DF_page_vectors_chunk_index DEFAULT (0);
END;

IF COL_LENGTH('dbo.page_vectors', 'total_chunks') IS NULL
BEGIN
    ALTER TABLE dbo.page_vectors
        ADD total_chunks int NOT NULL CONSTRAINT DF_page_vectors_total_chunks DEFAULT (1);
END;

DECLARE @pk_name sysname;
SELECT @pk_name = kc.name
FROM sys.key_constraints kc
JOIN sys.tables t ON t.object_id = kc.parent_object_id
WHERE kc.[type] = 'PK'
  AND t.name = 'page_vectors'
  AND SCHEMA_NAME(t.schema_id) = 'dbo';

IF @pk_name IS NOT NULL
BEGIN
    DECLARE @drop_pk nvarchar(max) = N'ALTER TABLE dbo.page_vectors DROP CONSTRAINT ' + QUOTENAME(@pk_name);
    EXEC(@drop_pk);
END;

ALTER TABLE dbo.page_vectors
    ADD CONSTRAINT PK_page_vectors PRIMARY KEY (page_id, chunk_index);

COMMIT TRANSACTION;
