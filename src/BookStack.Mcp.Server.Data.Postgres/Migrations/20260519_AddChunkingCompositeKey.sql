-- FEAT-0060 / #108
-- Postgres schema migration for chunk-aware vector storage.
-- Converts page_vectors PK from (page_id) to (page_id, chunk_index)
-- and adds total_chunks metadata with backward-compatible defaults.

BEGIN;

ALTER TABLE IF EXISTS page_vectors
    ADD COLUMN IF NOT EXISTS chunk_index integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS page_vectors
    ADD COLUMN IF NOT EXISTS total_chunks integer NOT NULL DEFAULT 1;

DO $$
DECLARE
    pk_name text;
BEGIN
    SELECT c.conname
      INTO pk_name
      FROM pg_constraint c
      JOIN pg_class t ON t.oid = c.conrelid
     WHERE t.relname = 'page_vectors'
       AND c.contype = 'p'
     LIMIT 1;

    IF pk_name IS NOT NULL THEN
        EXECUTE format('ALTER TABLE page_vectors DROP CONSTRAINT %I', pk_name);
    END IF;
END $$;

ALTER TABLE page_vectors
    ADD CONSTRAINT pk_page_vectors PRIMARY KEY (page_id, chunk_index);

COMMIT;
