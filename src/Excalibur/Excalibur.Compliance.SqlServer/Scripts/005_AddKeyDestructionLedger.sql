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
-- ===========================================================================

-- Idempotent: once KeyGeneration exists this block is skipped, so re-running
-- the script cannot drop a populated ledger a second time.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.tables t  ON c.object_id = t.object_id
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance'
      AND t.name = 'ErasureDestroyedKeys'
      AND c.name = 'KeyGeneration')
BEGIN
    DROP TABLE IF EXISTS [compliance].[ErasureDestroyedKeys];

    -- See 001 for the full rationale. In short: the primary key is the
    -- GENERATION alone, because a generation is minted once and never reused,
    -- so a row's existence IS the destruction statement and two rows for one
    -- generation are a contradiction the database refuses. RequestId and
    -- KeyHandle are audit attributes, never key components — keying on the
    -- handle silently drops a second destruction at that handle.
    CREATE TABLE [compliance].[ErasureDestroyedKeys] (
        KeyGeneration NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
        -- NULLABLE: a destruction the CONSUMER asserted through the ledger's public
        -- write has no erasure request and no handle. Audit attributes either way.
        RequestId     UNIQUEIDENTIFIER NULL,
        KeyHandle     NVARCHAR(256) COLLATE Latin1_General_BIN2 NULL,
        DestroyedAt   DATETIMEOFFSET   NOT NULL,
        -- WHO asserted it: 'framework-erasure' or 'caller-assertion'. Stated explicitly
        -- rather than inferred from the nulls above.
        RecordedBy    NVARCHAR(32)     NOT NULL,
        CONSTRAINT PK_ErasureDestroyedKeys PRIMARY KEY (KeyGeneration)
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
