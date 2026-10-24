-- ddl-migration: provisioning-only ErasureDestroyedKeys - Earlier alpha ledger identities require a new schema; incompatible tables are rejected without deleting destruction evidence.
-- Provision absent ledger tables; validate existing tables without altering records.
-- Earlier alpha schemas are unsupported: provision a new compliance schema instead.
-- All checks precede creation; a transaction-owned lock serializes this script.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @lockResult INT;
    EXEC @lockResult = sys.sp_getapplock
        @Resource = N'Excalibur.Compliance.KeyDestructionLedger.Provision',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000;
    IF @lockResult < 0 THROW 51000, 'Cannot acquire ledger provisioning lock.', 1;
    IF OBJECT_ID(N'[compliance].[ErasureDestroyedKeys]') IS NOT NULL
    BEGIN
        IF (SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), c.name), N',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'[compliance].[ErasureDestroyedKeys]') AND i.is_primary_key=1 AND ic.key_ordinal>0) IS NULL
            THROW 51001, 'Incompatible ErasureDestroyedKeys primary key; provision a new compliance schema.', 1;
        IF (SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), c.name), N',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'[compliance].[ErasureDestroyedKeys]') AND i.is_primary_key=1 AND ic.key_ordinal>0) COLLATE Latin1_General_BIN2 <> N'KeyHandle,KeyGeneration'
            THROW 51001, 'Incompatible ErasureDestroyedKeys primary key; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureDestroyedKeys]') AND c.name=N'KeyGeneration' AND TYPE_NAME(c.system_type_id)='nvarchar' AND c.is_nullable=0 AND c.max_length=512 AND c.collation_name='Latin1_General_BIN2')
            THROW 51002, 'Incompatible ErasureDestroyedKeys.KeyGeneration; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureDestroyedKeys]') AND c.name=N'RequestId' AND TYPE_NAME(c.system_type_id)='uniqueidentifier' AND c.is_nullable=1)
            THROW 51002, 'Incompatible ErasureDestroyedKeys.RequestId; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureDestroyedKeys]') AND c.name=N'KeyHandle' AND TYPE_NAME(c.system_type_id)='nvarchar' AND c.is_nullable=0 AND c.max_length=512 AND c.collation_name='Latin1_General_BIN2')
            THROW 51002, 'Incompatible ErasureDestroyedKeys.KeyHandle; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureDestroyedKeys]') AND c.name=N'DestroyedAt' AND TYPE_NAME(c.system_type_id)='datetimeoffset' AND c.is_nullable=0)
            THROW 51002, 'Incompatible ErasureDestroyedKeys.DestroyedAt; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureDestroyedKeys]') AND c.name=N'RecordedBy' AND TYPE_NAME(c.system_type_id)='nvarchar' AND c.is_nullable=0 AND c.max_length=64)
            THROW 51002, 'Incompatible ErasureDestroyedKeys.RecordedBy; provision a new compliance schema.', 1;
    END;
    IF OBJECT_ID(N'[compliance].[ErasureDestructionIntents]') IS NOT NULL
    BEGIN
        IF (SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), c.name), N',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'[compliance].[ErasureDestructionIntents]') AND i.is_primary_key=1 AND ic.key_ordinal>0) IS NULL
            THROW 51001, 'Incompatible ErasureDestructionIntents primary key; provision a new compliance schema.', 1;
        IF (SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), c.name), N',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'[compliance].[ErasureDestructionIntents]') AND i.is_primary_key=1 AND ic.key_ordinal>0) COLLATE Latin1_General_BIN2 <> N'RequestId,KeyHandle,KeyGeneration'
            THROW 51001, 'Incompatible ErasureDestructionIntents primary key; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureDestructionIntents]') AND c.name=N'RequestId' AND TYPE_NAME(c.system_type_id)='uniqueidentifier' AND c.is_nullable=0)
            THROW 51002, 'Incompatible ErasureDestructionIntents.RequestId; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureDestructionIntents]') AND c.name=N'KeyHandle' AND TYPE_NAME(c.system_type_id)='nvarchar' AND c.is_nullable=0 AND c.max_length=512 AND c.collation_name='Latin1_General_BIN2')
            THROW 51002, 'Incompatible ErasureDestructionIntents.KeyHandle; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureDestructionIntents]') AND c.name=N'KeyGeneration' AND TYPE_NAME(c.system_type_id)='nvarchar' AND c.is_nullable=0 AND c.max_length=512 AND c.collation_name='Latin1_General_BIN2')
            THROW 51002, 'Incompatible ErasureDestructionIntents.KeyGeneration; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureDestructionIntents]') AND c.name=N'StagedAt' AND TYPE_NAME(c.system_type_id)='datetimeoffset' AND c.is_nullable=0)
            THROW 51002, 'Incompatible ErasureDestructionIntents.StagedAt; provision a new compliance schema.', 1;
    END;
    IF OBJECT_ID(N'[compliance].[ErasureDestroyedKeys]') IS NULL
    BEGIN
        CREATE TABLE [compliance].[ErasureDestroyedKeys] (
            KeyGeneration NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
            RequestId UNIQUEIDENTIFIER NULL,
            KeyHandle NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
            DestroyedAt DATETIMEOFFSET NOT NULL,
            RecordedBy NVARCHAR(32) NOT NULL,
            CONSTRAINT PK_ErasureDestroyedKeys PRIMARY KEY (KeyHandle, KeyGeneration)
        );
        CREATE INDEX IX_ErasureDestroyedKeys_Request ON [compliance].[ErasureDestroyedKeys] (RequestId, KeyHandle);
    END;
    IF OBJECT_ID(N'[compliance].[ErasureDestructionIntents]') IS NULL
    BEGIN
        CREATE TABLE [compliance].[ErasureDestructionIntents] (
            RequestId UNIQUEIDENTIFIER NOT NULL,
            KeyHandle NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
            KeyGeneration NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
            StagedAt DATETIMEOFFSET NOT NULL,
            CONSTRAINT PK_ErasureDestructionIntents PRIMARY KEY (RequestId, KeyHandle, KeyGeneration)
        );
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
