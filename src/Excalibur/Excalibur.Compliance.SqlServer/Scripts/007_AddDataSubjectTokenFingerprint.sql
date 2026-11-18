-- ddl-migration: provisioning-only [compliance].[ErasureTokenFingerprint] - A new table recording which data-subject token function produced this database's tokens; an incompatible existing table is rejected rather than altered, because rewriting this value would let a process with the wrong hashing key proceed.
-- Provision the absent token-fingerprint table; validate an existing table without altering records.
-- Earlier alpha schemas are unsupported: provision a new compliance schema instead.
-- All checks precede creation; a transaction-owned lock serializes this script.
--
-- WHY THIS TABLE EXISTS. Every durable compliance record here is keyed by a pseudonymisation token
-- derived from a data-subject identifier under a secret key. Change that key -- ordinary quarterly
-- secret-rotation hygiene is enough -- and every token changes at once: an erasure enumerates no key
-- handles and still signs a completion certificate, a legal-hold lookup returns nothing so the erasure
-- proceeds over data the law requires you to keep, and already-encrypted personal data stops being
-- decryptable. None of it raises and none of it is logged. This table turns that silence into a refusal.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @lockResult INT;
    EXEC @lockResult = sys.sp_getapplock
        @Resource = N'Excalibur.Compliance.DataSubjectTokenFingerprint.Provision',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000;
    IF @lockResult < 0 THROW 51000, 'Cannot acquire token-fingerprint provisioning lock.', 1;

    IF OBJECT_ID(N'[compliance].[ErasureTokenFingerprint]') IS NOT NULL
    BEGIN
        IF (SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), c.name), N',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'[compliance].[ErasureTokenFingerprint]') AND i.is_primary_key=1 AND ic.key_ordinal>0) COLLATE Latin1_General_BIN2 <> N'Id'
            THROW 51001, 'Incompatible ErasureTokenFingerprint primary key; it must be (Id) so the table holds at most one row by key rather than by convention. Provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureTokenFingerprint]') AND c.name=N'Id' AND TYPE_NAME(c.system_type_id)='nvarchar' AND c.is_nullable=0)
            THROW 51002, 'Incompatible ErasureTokenFingerprint.Id; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureTokenFingerprint]') AND c.name=N'Fingerprint' AND TYPE_NAME(c.system_type_id)='nvarchar' AND c.is_nullable=0)
            THROW 51002, 'Incompatible ErasureTokenFingerprint.Fingerprint; provision a new compliance schema.', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureTokenFingerprint]') AND c.name=N'RecordedAt' AND TYPE_NAME(c.system_type_id)='datetimeoffset' AND c.is_nullable=0)
            THROW 51002, 'Incompatible ErasureTokenFingerprint.RecordedAt; provision a new compliance schema.', 1;
        -- A TENANT COLUMN HERE IS A DEFECT, NOT A MISSING FEATURE, so an existing table carrying one is
        -- REFUSED rather than tolerated. The token function belongs to the process, not to a tenant: a
        -- per-tenant fingerprint would record a rotated key as correct for a tenant onboarded after the
        -- rotation while every earlier tenant's records were already orphaned -- and the check would then
        -- pass forever on exactly the deployment where the damage is worst. This is the one table in the
        -- schema whose tenant column must stay absent, so its absence is asserted rather than assumed.
        IF EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'[compliance].[ErasureTokenFingerprint]') AND c.name=N'TenantId')
            THROW 51003, 'ErasureTokenFingerprint must NOT have a TenantId column. The data-subject token function is a property of the process, not of a tenant; a per-tenant fingerprint would report a rotated hashing key as correct for newly onboarded tenants while every earlier tenant was already orphaned. Provision a new compliance schema.', 1;
    END
    ELSE
    BEGIN
        -- ONE row, for the whole database. There is deliberately no tenant column; see the refusal above.
        CREATE TABLE [compliance].[ErasureTokenFingerprint] (
            -- A framework constant, so the primary key bounds the table to a single row.
            [Id] NVARCHAR(128) COLLATE Latin1_General_BIN2 NOT NULL,

            -- A non-reversible fingerprint of the token function, derived by hashing one fixed canary
            -- identifier through the registered hasher and running the result through a key-derivation
            -- function. It is NOT a data-subject token and NOT a secret: the canary is a framework
            -- constant, so this value grants no capability its reader did not already have.
            [Fingerprint] NVARCHAR(128) COLLATE Latin1_General_BIN2 NOT NULL,

            -- Audit only. Never read by any predicate.
            [RecordedAt] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_ErasureTokenFingerprint_RecordedAt] DEFAULT SYSDATETIMEOFFSET(),

            CONSTRAINT [PK_ErasureTokenFingerprint] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [CK_ErasureTokenFingerprint_NotBlank] CHECK (LEN(LTRIM(RTRIM([Fingerprint]))) > 0)
        );
    END

    -- WRITE-ONCE, ENFORCED HERE RATHER THAN TRUSTED.
    --
    -- The claim this table backs is an insert-if-absent followed by a read of what is now recorded. That
    -- is sound only while a recorded fingerprint can never change. If any statement could update it, two
    -- processes can interleave so that the one with the CORRECT token function refuses while the one with
    -- the WRONG function proceeds:
    --
    --     A: INSERT -> wins, records fp1
    --     B: UPDATE Fingerprint := fp2
    --     A: SELECT -> reads fp2 -> refuses, against its own correct function
    --     B: SELECT -> reads fp2 -> matches -> PROCEEDS with the wrong one
    --
    -- The framework grants itself no update path. This trigger makes that true of the database as well,
    -- including for an administrator who believes they are repairing something.
    IF OBJECT_ID(N'[compliance].[TR_ErasureTokenFingerprint_WriteOnce]') IS NOT NULL
        DROP TRIGGER [compliance].[TR_ErasureTokenFingerprint_WriteOnce];

    EXEC sys.sp_executesql N'
CREATE TRIGGER [compliance].[TR_ErasureTokenFingerprint_WriteOnce]
ON [compliance].[ErasureTokenFingerprint]
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM deleted) RETURN;
    THROW 51004, ''compliance.ErasureTokenFingerprint is write-once: the recorded data-subject token fingerprint must never change or be removed. Changing it would let a process with the wrong hashing key proceed while a process with the right one is refused. If the hashing key genuinely changed, the records keyed under the old one are unreachable and must be migrated before any new fingerprint is recorded.'', 1;
END';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
