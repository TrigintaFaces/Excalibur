-- SQL Server requires SET QUOTED_IDENTIFIER ON to create a FILTERED index (one with a WHERE
-- clause), and sqlcmd defaults it OFF. Without these, every filtered index below fails with
-- Msg 1934 and is simply absent from the resulting database -- a script runner that does not
-- check exit status gets a schema silently missing its most selective indexes.
-- No GO after these: GO is a client batch separator, not T-SQL, and this script is also
-- executed as a SINGLE command by callers that do not split batches. The setting applies
-- within its own batch and persists across later ones, so it is not needed.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- SQL Server Schema for Excalibur.Saga.SqlServer
-- Version: 1.0
-- This script creates the saga storage schema for the Excalibur framework.

-- Create schema if not exists
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dispatch')
BEGIN
    EXEC('CREATE SCHEMA dispatch');
END
GO

-- Create sagas table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dispatch.sagas') AND type = N'U')
BEGIN
    CREATE TABLE dispatch.sagas (
        -- Primary key
        SagaId UNIQUEIDENTIFIER NOT NULL,

        -- Saga metadata
        SagaType NVARCHAR(500) NOT NULL,
        StateJson NVARCHAR(MAX) NOT NULL,
        IsCompleted BIT NOT NULL DEFAULT 0,

        -- Explicit completion instant (UTC; SagaState.CompletedAt). The retention purge
        -- (PurgeCompletedBeforeAsync) keys on this indexed column across every provider rather than a
        -- proxy, so it is a base-table column (not the optional monitoring add-on) — a store running only
        -- this script must still support purge. NULL until the saga completes.
        -- DATETIMEOFFSET, not DATETIME2. CompletedAt is a consumer-supplied DateTimeOffset and the
        -- retention purge compares it against a UTC threshold. DATETIME2 has no offset: the client
        -- writes the LOCAL wall-clock of the instant and the offset is discarded, so a saga completed
        -- from a host at UTC+1 is purged an hour early. This is the only client-bound instant in this
        -- schema; CreatedUtc and UpdatedUtc are set server-side by SYSUTCDATETIME() and are correct.
        CompletedAt DATETIMEOFFSET(7) NULL,

        -- Defense-in-depth tenant binding: the owning tenant persisted on the saga row itself
        -- (in addition to TenantId inside StateJson) so tenant scope is queryable/enforceable at
        -- the row level.
        --
        -- NOT NULL with an explicit reserved sentinel for rows that are genuinely not
        -- tenant-scoped, never NULL. NULL cannot participate in an equality predicate, so a
        -- nullable discriminator forces every read path to special-case IS NULL — and forces the
        -- tenant term to be OMITTED from a MERGE match condition on the untenanted path, which is
        -- how an unscoped save came to overwrite a scoped tenant's row. It also makes "global" and
        -- "forgot to scope" indistinguishable. The sentinel makes the term unconditional.
        --
        -- COLLATE Latin1_General_BIN2 because the tenant term is compared with Ordinal
        -- (case-sensitive) semantics in .NET, while the SQL Server database default collation is
        -- case-INSENSITIVE: without this pin 'Acme' and 'acme' are two tenants in memory and one
        -- tenant in storage, so one tenant reads another's sagas. Matches the tenant discriminator
        -- shape shipped by the audit schema.
        TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL DEFAULT '__untenanted__',

        -- Application-level optimistic concurrency version (matches SagaState.Version).
        -- The store performs a compare-and-swap on this column; RowVersion below is
        -- a separate SQL Server rowversion retained for change-tracking, NOT used for the CAS.
        Version BIGINT NOT NULL DEFAULT 0,

        -- Timestamps
        CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        -- Concurrency control
        RowVersion ROWVERSION NOT NULL,

        -- Constraints
        -- The tenant term is PART OF THE KEY. Sagas are correlated by a BUSINESS key (OrderId,
        -- CorrelationId), not a per-tenant GUID, so tenant A's Order-123 saga and tenant B's
        -- Order-123 saga carry the same SagaId. Keyed on SagaId alone they are the same row: the
        -- second write either violates the primary key or overwrites the first tenant's saga state
        -- AND its tenant stamp. Two tenants' sagas must be able to coexist, so the discriminator
        -- is in the key rather than only in a read-side predicate.
        --
        -- TenantId leads: it makes a tenant's sagas physically contiguous, so every tenant-scoped
        -- read is a range seek on the clustered index. Deliberately NO separate unique index on
        -- SagaId alone — that would re-impose the cross-tenant uniqueness this key exists to remove.
        CONSTRAINT PK_dispatch_sagas PRIMARY KEY CLUSTERED (TenantId, SagaId)
    );

    -- Index for querying by saga type
    CREATE NONCLUSTERED INDEX IX_dispatch_sagas_SagaType
        ON dispatch.sagas (SagaType)
        INCLUDE (IsCompleted);

    -- Index for querying incomplete sagas
    CREATE NONCLUSTERED INDEX IX_dispatch_sagas_IsCompleted
        ON dispatch.sagas (IsCompleted)
        WHERE IsCompleted = 0;

    -- Index for the retention purge range scan (PurgeCompletedBeforeAsync): DELETE ... WHERE
    -- CompletedAt IS NOT NULL AND CompletedAt < @Threshold. Filtered to non-null so it stays small.
    CREATE NONCLUSTERED INDEX IX_dispatch_sagas_CompletedAt
        ON dispatch.sagas (CompletedAt)
        WHERE CompletedAt IS NOT NULL;
END
GO

-- ---------------------------------------------------------------------------------------------
-- Upgrade path for a sagas table created by an earlier version of this script.
--
-- The CREATE above is guarded by IF NOT EXISTS, so an existing table is never touched by it. That
-- means a consumer who already ran this script would otherwise keep a nullable, collation-unpinned
-- discriminator and a tenant-less primary key permanently, and receive none of the fix. These two
-- blocks bring an existing table to the shape declared above. Each is guarded on the condition it
-- repairs, so running the script repeatedly is safe and a table left half-converted converges.
-- ---------------------------------------------------------------------------------------------

-- 1. Discriminator: NULL -> reserved sentinel, then NOT NULL + case-sensitive collation.
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dispatch.sagas')
             AND name = N'TenantId' AND is_nullable = 1)
BEGIN
    -- Backfill before the ALTER: every pre-existing untenanted row carries NULL, and NOT NULL
    -- cannot be applied while any remain. These rows ARE the untenanted partition, so the sentinel
    -- is their correct value rather than a guess.
    UPDATE dispatch.sagas SET TenantId = '__untenanted__' WHERE TenantId IS NULL;

    ALTER TABLE dispatch.sagas
        ALTER COLUMN TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

    ALTER TABLE dispatch.sagas
        ADD CONSTRAINT DF_dispatch_sagas_TenantId DEFAULT '__untenanted__' FOR TenantId;
END
GO

-- 2. Primary key: (SagaId) -> (TenantId, SagaId).
-- Runs only when the existing key does not already lead with TenantId. This rebuilds the table,
-- because PK_dispatch_sagas is CLUSTERED — expect it to be proportional to row count, and run it
-- in a maintenance window on a large sagas table.
IF EXISTS (SELECT * FROM sys.indexes i
           WHERE i.object_id = OBJECT_ID(N'dispatch.sagas') AND i.is_primary_key = 1)
   AND NOT EXISTS (SELECT * FROM sys.indexes i
                   JOIN sys.index_columns ic
                     ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                   JOIN sys.columns c
                     ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                   WHERE i.object_id = OBJECT_ID(N'dispatch.sagas')
                     AND i.is_primary_key = 1
                     AND c.name = N'TenantId')
BEGIN
    DECLARE @pkName SYSNAME = (SELECT name FROM sys.indexes
                               WHERE object_id = OBJECT_ID(N'dispatch.sagas') AND is_primary_key = 1);
    EXEC('ALTER TABLE dispatch.sagas DROP CONSTRAINT ' + @pkName);

    ALTER TABLE dispatch.sagas
        ADD CONSTRAINT PK_dispatch_sagas PRIMARY KEY CLUSTERED (TenantId, SagaId);
END
GO
