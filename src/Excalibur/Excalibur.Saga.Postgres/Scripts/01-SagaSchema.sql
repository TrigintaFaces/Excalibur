-- PostgreSQL Schema for Excalibur.Saga.Postgres
-- Version: 1.0
-- This script creates the saga storage schema for the Excalibur framework.
--
-- Object names match the SQL emitted by PostgresSagaStore's request types
-- (schema "dispatch", table "sagas", snake_case columns). The defaults for
-- Schema/TableName live in PostgresSagaOptions; adjust the identifiers below if
-- you override them.

-- Create schema if not exists
CREATE SCHEMA IF NOT EXISTS dispatch;

-- Create sagas table
CREATE TABLE IF NOT EXISTS dispatch.sagas (
    -- Primary key
    saga_id       uuid          NOT NULL,

    -- Saga metadata
    saga_type     varchar(500)  NOT NULL,
    state_json    jsonb         NOT NULL,
    is_completed  boolean       NOT NULL DEFAULT false,

    -- Explicit completion instant (UTC; SagaState.CompletedAt). The retention purge
    -- (PurgeCompletedBeforeAsync) keys on this indexed column across every provider rather than a
    -- proxy, so it is a base-table column. NULL until the saga completes.
    completed_at  timestamptz   NULL,

    -- Defense-in-depth tenant binding: the owning tenant persisted on the saga row
    -- itself (in addition to tenant_id inside state_json) so tenant scope is
    -- queryable/enforceable at the row level.
    --
    -- NOT NULL with an explicit reserved sentinel for rows that are genuinely not tenant-scoped,
    -- never NULL. NULL cannot participate in an equality predicate, so a nullable discriminator
    -- forces the tenant term to be OMITTED from the upsert's conflict/update predicate on the
    -- untenanted path -- which is how an unscoped save came to overwrite a scoped tenant's row. It
    -- also makes "global" and "forgot to scope" indistinguishable. The sentinel makes the term
    -- unconditional.
    --
    -- Deliberately NO COLLATE clause, unlike the SQL Server schema: PostgreSQL equality on a
    -- deterministic collation is already byte-exact and case-sensitive, matching .NET's Ordinal
    -- comparison, so there is nothing to pin here. Pinning a non-deterministic collation would
    -- additionally risk defeating index usage on this column. The absence is deliberate, not a gap
    -- -- stated so a collation census does not read it as one.
    tenant_id     varchar(200)  NOT NULL DEFAULT '__untenanted__',

    -- Application-level optimistic concurrency version (matches SagaState.Version).
    -- The store performs a compare-and-swap on this column via INSERT ... ON CONFLICT
    -- DO NOTHING (new saga) and a version-gated UPDATE (existing saga).
    version       bigint        NOT NULL DEFAULT 0,

    -- Timestamps (UTC). The store writes NOW() explicitly on insert/update; the
    -- defaults are a fallback for out-of-band inserts.
    created_utc   timestamptz   NOT NULL DEFAULT now(),
    updated_utc   timestamptz   NOT NULL DEFAULT now(),

    -- The tenant term is PART OF THE KEY. Sagas are correlated by a BUSINESS key (OrderId,
    -- CorrelationId), not a per-tenant UUID, so tenant A's Order-123 saga and tenant B's Order-123
    -- saga carry the same saga_id. Keyed on saga_id alone they are the same row: the second write
    -- either violates the primary key or overwrites the first tenant's saga state AND its tenant
    -- stamp. tenant_id leads so a tenant's sagas are contiguous for scoped range scans.
    -- Deliberately NO separate unique index on saga_id alone -- that would re-impose the
    -- cross-tenant uniqueness this key exists to remove.
    CONSTRAINT pk_dispatch_sagas PRIMARY KEY (tenant_id, saga_id)
);

-- ---------------------------------------------------------------------------------------------
-- Upgrade path for a sagas table created by an earlier version of this script. The CREATE above
-- is IF NOT EXISTS, so an existing table is never touched by it and would otherwise keep a
-- nullable discriminator and a tenant-less key permanently. Each statement is guarded on the
-- condition it repairs, so re-running is safe and a half-converted table converges.
-- ---------------------------------------------------------------------------------------------

-- 1. Discriminator: NULL -> reserved sentinel, then NOT NULL with that default.
UPDATE dispatch.sagas SET tenant_id = '__untenanted__' WHERE tenant_id IS NULL;
ALTER TABLE dispatch.sagas ALTER COLUMN tenant_id SET DEFAULT '__untenanted__';
ALTER TABLE dispatch.sagas ALTER COLUMN tenant_id SET NOT NULL;

-- 2. Primary key: (saga_id) -> (tenant_id, saga_id). Runs only when the existing key does not
-- already carry tenant_id. On a large sagas table this rewrites the index; run it in a
-- maintenance window.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_index i
        JOIN pg_class t ON t.oid = i.indrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        WHERE n.nspname = 'dispatch' AND t.relname = 'sagas' AND i.indisprimary
    ) AND NOT EXISTS (
        SELECT 1
        FROM pg_index i
        JOIN pg_class t ON t.oid = i.indrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY (i.indkey)
        WHERE n.nspname = 'dispatch' AND t.relname = 'sagas' AND i.indisprimary
          AND a.attname = 'tenant_id'
    ) THEN
        ALTER TABLE dispatch.sagas DROP CONSTRAINT pk_dispatch_sagas;
        ALTER TABLE dispatch.sagas ADD CONSTRAINT pk_dispatch_sagas PRIMARY KEY (tenant_id, saga_id);
    END IF;
END $$;

-- Index for querying by saga type (covers the completed flag)
CREATE INDEX IF NOT EXISTS ix_dispatch_sagas_saga_type
    ON dispatch.sagas (saga_type)
    INCLUDE (is_completed);

-- Partial index for querying incomplete sagas
CREATE INDEX IF NOT EXISTS ix_dispatch_sagas_is_completed
    ON dispatch.sagas (is_completed)
    WHERE is_completed = false;

-- Index for the retention purge range scan (PurgeCompletedBeforeAsync): DELETE ... WHERE
-- completed_at IS NOT NULL AND completed_at < @Threshold. Filtered to non-null so it stays small.
CREATE INDEX IF NOT EXISTS ix_dispatch_sagas_completed_at
    ON dispatch.sagas (completed_at)
    WHERE completed_at IS NOT NULL;
