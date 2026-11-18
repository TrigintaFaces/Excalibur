-- ddl-migration: provisioning-only ErasureMintedKeys - A mint ledger is the enumeration an erasure destroys from; an incompatible table is rejected rather than altered, because a row lost here is a key nothing looks for.
-- Provision the absent mint-ledger table; validate an existing table without altering records.
-- Earlier alpha schemas are unsupported: provision a new compliance schema instead.
-- All checks precede creation; a transaction-owned lock serializes this script.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @lockResult INT;
    EXEC @lockResult = sys.sp_getapplock
        @Resource = N'Excalibur.Compliance.KeyMintLedger.Provision',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000;
    IF @lockResult < 0 THROW 51000, 'Cannot acquire ledger provisioning lock.', 1;
    IF OBJECT_ID(N'[compliance].[ErasureMintedKeys]') IS NOT NULL
    BEGIN
        IF (SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), c.name), N',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'[compliance].[ErasureMintedKeys]') AND i.is_primary_key=1 AND ic.key_ordinal>0) IS NULL
            THROW 51001, 'Incompatible ErasureMintedKeys primary key; provision a new compliance schema.', 1;
        IF (SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), c.name), N',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'[compliance].[ErasureMintedKeys]') AND i.is_primary_key=1 AND ic.key_ordinal>0) COLLATE Latin1_General_BIN2 <> N'KeyHandle'
            THROW 51001, 'Incompatible ErasureMintedKeys primary key; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureMintedKeys]') AND c.name=N'KeyHandle' AND TYPE_NAME(c.system_type_id)='nvarchar' AND c.is_nullable=0 AND c.max_length=512 AND c.collation_name='Latin1_General_BIN2')
            THROW 51002, 'Incompatible ErasureMintedKeys.KeyHandle; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureMintedKeys]') AND c.name=N'TenantId' AND TYPE_NAME(c.system_type_id)='nvarchar' AND c.is_nullable=0 AND c.max_length=128 AND c.collation_name='Latin1_General_BIN2')
            THROW 51002, 'Incompatible ErasureMintedKeys.TenantId; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureMintedKeys]') AND c.name=N'DataSubjectIdHash' AND TYPE_NAME(c.system_type_id)='nvarchar' AND c.is_nullable=0 AND c.max_length=256 AND c.collation_name='Latin1_General_BIN2')
            THROW 51002, 'Incompatible ErasureMintedKeys.DataSubjectIdHash; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureMintedKeys]') AND c.name=N'IntentAt' AND TYPE_NAME(c.system_type_id)='datetimeoffset' AND c.is_nullable=0)
            THROW 51002, 'Incompatible ErasureMintedKeys.IntentAt; provision a new compliance schema.', 1;
        -- MintedAt MUST BE NULLABLE, and a NOT NULL one is refused rather than accepted. The null is the
        -- only thing that distinguishes a write in flight from a write that was abandoned: the row is
        -- written before the key is created, so a NOT NULL column forces every row to claim a key exists,
        -- and an erasure would read every pending row as covered and certify.
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureMintedKeys]') AND c.name=N'MintedAt' AND TYPE_NAME(c.system_type_id)='datetimeoffset' AND c.is_nullable=1)
            THROW 51002, 'Incompatible ErasureMintedKeys.MintedAt; it must be NULLABLE, because a null marks a mint whose key creation was never confirmed. Provision a new compliance schema.', 1;
        -- THE ENUMERATION INDEX IS VALIDATED, NOT ONLY CREATED. An existing table with the right primary key,
        -- columns and collations but NO index passes every check above while ListHandlesAsync scans
        -- sequentially forever, with nothing to notice. The comment on the create branch below calls this
        -- "the index the erasure cannot work without"; a validation that does not check it leaves that
        -- sentence unenforced on exactly the tables validation exists for.
        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes i
            WHERE i.object_id=OBJECT_ID(N'[compliance].[ErasureMintedKeys]')
              AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                          WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0 AND c.name=N'TenantId')
              AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                          WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0 AND c.name=N'DataSubjectIdHash'))
            THROW 51003, 'ErasureMintedKeys has no index covering (TenantId, DataSubjectIdHash); the erasure enumerates by that key and cannot work without it.', 1;
    END;
    IF OBJECT_ID(N'[compliance].[ErasureMintedKeys]') IS NULL
    BEGIN
        -- The handle alone is the key: existence is a per-HANDLE fact, where a destruction is a
        -- per-generation one. A generation column here would need updating after a re-mint, which makes
        -- the row mutable and rebuilds a create-or-rotate race one layer down. There is nothing to
        -- update: a handle minted, destroyed and minted again has one row throughout, and it stays true.
        --
        -- The binary collation is not stylistic. An erasure enumerates by (TenantId, DataSubjectIdHash)
        -- and the service compares handles ordinally; a database whose default collation folded case
        -- would let one owner's rows answer for another's, which is the cross-tenant direction this
        -- subsystem has already shipped once.
        CREATE TABLE [compliance].[ErasureMintedKeys] (
            KeyHandle NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
            TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,
            DataSubjectIdHash NVARCHAR(128) COLLATE Latin1_General_BIN2 NOT NULL,
            -- WHEN THE ROW WAS WRITTEN, which is BEFORE the key was created.
            IntentAt DATETIMEOFFSET NOT NULL,
            -- WHEN THE KEY WAS CONFIRMED TO EXIST, and NULL until it was. Between the row and the key a
            -- write is invisible to both observables an erasure has, so that state is indistinguishable
            -- from a writer that crashed, which is safely ignorable. This column separates them, and an
            -- erasure that finds a null reports a concurrent write rather than certifying. DO NOT make it
            -- NOT NULL.
            MintedAt DATETIMEOFFSET NULL,
            CONSTRAINT PK_ErasureMintedKeys PRIMARY KEY (KeyHandle)
        );
        -- The enumeration index, and the erasure cannot work without it: a handle is an opaque digest,
        -- so (TenantId, DataSubjectIdHash) is the only key a subject's handles can be found by.
        CREATE INDEX IX_ErasureMintedKeys_Subject ON [compliance].[ErasureMintedKeys] (TenantId, DataSubjectIdHash);
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
