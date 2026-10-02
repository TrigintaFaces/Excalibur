-- ===========================================================================
-- 005 — Key-destruction ledger and staging table
-- ===========================================================================
-- Run this ONLY on a database already provisioned by an earlier version of
-- 001_CreateComplianceSchema.sql. A fresh database gets the final shape from
-- 001 and must not run this script.
--
-- WHY A SEPARATE SCRIPT IS NEEDED AT ALL: every statement in 001 is guarded by
-- IF NOT EXISTS, so re-running it on a provisioned database is a no-op and the
-- new column never arrives. The store's startup schema check then FAILS, by
-- design, naming the missing column — loud, and at the cheapest possible
-- moment. This script is what makes that failure fixable.
--
-- WHAT IT DOES TO EXISTING ROWS, stated plainly because it is destructive and
-- the consequence is wider than "in-flight erasures":
--
-- Rows written by the earlier schema record a destruction by (RequestId, KeyHandle)
-- and carry NO generation. A generation cannot be back-filled — the value only
-- ever existed inside the key backend, and the destruction annihilated it. A
-- row with no generation cannot answer the read predicate, so keeping it would
-- force the predicate back onto the handle, which is the defect this change
-- exists to remove. The table is therefore DROPPED AND RECREATED, and that
-- discards EVERY historical destruction record, not only in-flight ones.
--
-- SO, AFTER THIS SCRIPT RUNS: every past erasure request reports an EMPTY
-- destroyed-key-handle list. That list is read from this table, so a request
-- completed months ago will report zero destroyed handles where it previously
-- reported several.
--
-- WHAT SURVIVES, because this is what makes the trade acceptable rather than
-- alarming: this script touches ONLY this one table. The signed completion
-- certificate is untouched and remains the durable attestation for every
-- completed request — it carries the erasure summary, including the count of
-- keys destroyed, and it is stored as the exact signed bytes so its signature
-- still verifies. The request row is untouched too and keeps its own
-- keys-destroyed count. What is lost is the per-handle BREAKDOWN, which exists
-- to let an interrupted erasure attest what its earlier passes achieved, not to
-- serve as the attestation itself.
--
-- The acute case: an erasure that is MID-RETRY when you run this loses the
-- record of which handles its earlier passes destroyed, so it can no longer be
-- reported complete and will need re-filing. Let in-flight erasures finish
-- before applying this.
--
-- THE TRIGGER IS THE PRIMARY-KEY SHAPE, not the presence of a column, and that
-- matters for idempotency. The ledger has had two earlier shapes: one keyed on
-- (RequestId, KeyHandle) with no generation at all, and one keyed on the
-- GENERATION alone. Guarding on "does a generation column exist" would skip this
-- script entirely on the second of those, leaving the primary key un-migrated
-- while the script reported success. Guarding on the FINAL shape -- is the handle
-- part of the primary key -- cannot be skipped into a wrong state: it is false for
-- both earlier shapes and for a missing table, and true only once the migration
-- has actually happened.
--
-- SO THIS SCRIPT IS DESTRUCTIVE FOR BOTH EARLIER SHAPES, not only the oldest. A
-- database already carrying generation-keyed rows loses them too. Everything the
-- section above says about what is lost and what survives applies unchanged.
-- ===========================================================================

-- Idempotent: once the primary key is (KeyHandle, KeyGeneration) this block is
-- skipped, so re-running the script cannot drop a populated ledger a second time.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    JOIN sys.columns c        ON c.object_id  = ic.object_id AND c.column_id = ic.column_id
    JOIN sys.tables t         ON t.object_id  = i.object_id
    JOIN sys.schemas s        ON s.schema_id  = t.schema_id
    WHERE s.name = 'compliance'
      AND t.name = 'ErasureDestroyedKeys'
      AND i.is_primary_key = 1
      AND c.name = 'KeyHandle')
BEGIN
    DROP TABLE IF EXISTS [compliance].[ErasureDestroyedKeys];

    -- See 001 for the full rationale. In short: the primary key is the PAIR
    -- (KeyHandle, KeyGeneration). The generation, because it is minted once and
    -- never reused, so a row's existence IS the destruction statement. The
    -- handle, because the generation alone rests on a uniqueness-across-handles
    -- assumption nothing enforces, which a consumer-supplied provider deriving
    -- its generations would break -- one row for two tenants' distinct keys.
    -- An earlier comment here argued against keying on the handle because that
    -- "silently drops a second destruction at that handle": true of (KeyHandle)
    -- ALONE, and not of the pair, which admits many generations per handle.
    CREATE TABLE [compliance].[ErasureDestroyedKeys] (
        KeyGeneration NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
        -- NULLABLE: a destruction the CONSUMER asserted has no erasure request. An audit
        -- attribute, never a key component.
        RequestId     UNIQUEIDENTIFIER NULL,
        -- NOT NULL: part of the primary key, and the public write now requires it.
        KeyHandle     NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
        DestroyedAt   DATETIMEOFFSET   NOT NULL,
        -- WHO asserted it: 'framework-erasure' or 'caller-assertion'. Stated explicitly
        -- rather than inferred from the nulls above.
        RecordedBy    NVARCHAR(32)     NOT NULL,
        CONSTRAINT PK_ErasureDestroyedKeys PRIMARY KEY (KeyHandle, KeyGeneration)
    );

    CREATE INDEX IX_ErasureDestroyedKeys_Request
        ON [compliance].[ErasureDestroyedKeys] (RequestId, KeyHandle);
END
GO

-- The staging table, written BEFORE a destruction. A SEPARATE table from the
-- ledger, deliberately: between the stage and the destruction a staged row
-- names LIVE material, so nothing that resolves the destruction predicate may
-- be able to reach it. See 001 for the full rationale.
IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance' AND t.name = 'ErasureDestructionIntents')
BEGIN
    CREATE TABLE [compliance].[ErasureDestructionIntents] (
        RequestId     UNIQUEIDENTIFIER NOT NULL,
        KeyHandle     NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
        KeyGeneration NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
        StagedAt      DATETIMEOFFSET   NOT NULL,
        CONSTRAINT PK_ErasureDestructionIntents
            PRIMARY KEY (RequestId, KeyHandle, KeyGeneration)
    );
END
GO
