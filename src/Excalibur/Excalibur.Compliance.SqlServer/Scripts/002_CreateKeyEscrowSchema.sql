-- SQL Server Schema for Excalibur.Compliance.SqlServer -- Key Escrow
-- Version: 1.0
--
-- Creates the three tables required by the SQL Server key escrow service. Run this script
-- against the target database before the first key is escrowed.
--
-- This schema is SEPARATE from 001_CreateComplianceSchema.sql because key escrow is an opt-in
-- feature with its own registration. If you do not call AddSqlServerKeyEscrow, you do not need
-- these tables. If you do, you need all three: escrow, recovery tokens, and the envelope wrap.
--
-- The two settings below are REQUIRED, not stylistic. SQL Server refuses to create a filtered
-- index unless QUOTED_IDENTIFIER and ANSI_NULLS are ON, and sqlcmd runs with QUOTED_IDENTIFIER
-- OFF by default. Without them the unique index that permits only one live escrow per key is
-- silently never created, and the protection this schema describes below does not exist on the
-- database you just provisioned. The sibling audit schema sets both for the same reason.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- READ THIS BEFORE YOU RELY ON ESCROW FOR RECOVERY
--
-- Unlike the erasure and legal-hold stores, the key escrow service does NOT verify its schema
-- on startup and has NO AutoCreateSchema option. If these tables are absent, the failure appears
-- on the first escrow write as "Invalid object name", not as a startup error. Provision the
-- schema here, then escrow one key and recover it, before you depend on escrow to protect a key
-- you cannot afford to lose. An escrow you have never recovered from is a backup you have never
-- restored.
--
-- Schema and table names are configurable. This script uses the defaults:
--
--     Schema           = "compliance"
--     TableName        = "KeyEscrow"
--     TokensTableName  = "RecoveryTokens"
--     WrapTableName    = "KeyEscrowWrap"
--
-- If you override any of those, rename the corresponding object below to match.
--
-- Every statement is guarded, so the script is safe to re-run and safe to apply to a database
-- that already holds some of these tables.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'compliance')
BEGIN
    EXEC('CREATE SCHEMA [compliance]')
END
GO

-- ---------------------------------------------------------------------------
-- Key escrow
-- ---------------------------------------------------------------------------
-- One row per escrowed key. The key material is stored encrypted under the master key and is
-- ADDITIONALLY sealed once recovery tokens are issued: generating a token batch rewrites
-- EncryptedKey to NULL, after which the key can be recovered only by reassembling a custodian
-- quorum. That is the whole point of the design, and it is why EncryptedKey is nullable -- a
-- NULL there is a sealed escrow, not a missing one.
IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance' AND t.name = 'KeyEscrow')
BEGIN
    CREATE TABLE [compliance].[KeyEscrow] (
        EscrowId             NVARCHAR(64)   NOT NULL PRIMARY KEY,
        KeyId                NVARCHAR(256)  NOT NULL,
        -- NULL once the escrow is SEALED (a token batch was generated). See the note above.
        EncryptedKey         VARBINARY(MAX) NULL,
        -- SHA-256 of the PLAINTEXT key, hex encoded (64 characters), for post-recovery verification.
        KeyHash              NVARCHAR(128)  NOT NULL,
        Algorithm            INT            NOT NULL,
        Iv                   VARBINARY(64)  NOT NULL,
        -- Nullable because the authentication tag is produced only by AEAD algorithms. A
        -- non-AEAD encryption provider supplies none, and this column must be able to hold that
        -- rather than reject the write.
        AuthTag              VARBINARY(64)  NULL,
        MasterKeyId          NVARCHAR(256)  NOT NULL,
        MasterKeyVersion     INT            NOT NULL,
        State                INT            NOT NULL,
        EscrowedAt           DATETIMEOFFSET NOT NULL,
        ExpiresAt            DATETIMEOFFSET NULL,
        -- The tenant scope this key was escrowed under. TOTAL: "no tenant" is the reserved
        -- '__untenanted__' value, never NULL, so there is exactly one way to say it.
        --
        -- READ THIS BEFORE CHANGING EITHER THIS COLUMN OR THE WRITE PATH. This value is not a
        -- query predicate anywhere in the escrow path. It is AEAD ASSOCIATED DATA: the service
        -- hashes it into the AAD when it encrypts, and feeds it back into the decryption context
        -- at recovery from WHAT THIS COLUMN HOLDS. So the value stored here and the value hashed
        -- at encryption time must be the same bytes, or the key does not come back -- at the one
        -- moment escrow exists to survive. A null tenant and the sentinel produce DIFFERENT AAD
        -- (the provider length-prefixes the term, so absent is a zero-length field and the
        -- sentinel is a fourteen-byte one); they are not interchangeable after the fact.
        --
        -- That invariant is why this column was originally nullable and stored the caller's value
        -- verbatim. It is preserved rather than abandoned: the service now normalises the term
        -- ONCE, into a single local, and uses that same local both for the encryption context and
        -- for the value bound here -- so the two still cannot disagree, and the representation is
        -- no longer split. Binding a raw nullable argument here again would fail the NOT NULL on
        -- every untenanted escrow, which is the loud failure rather than the silent one.
        --
        -- Converging an EXISTING table would be a different and much worse operation: rewriting a
        -- stored tenant term invalidates the AAD of every row written under the old one, making
        -- those keys permanently unrecoverable. No migration script accompanies this change and
        -- none is possible. It is safe only because this schema has never been obtainable: the
        -- package shipped no script for these tables until the commit that added this file, and
        -- the service has no auto-create path, so no consumer can hold a row written under the
        -- previous shape.
        --
        -- The binary collation is restated deliberately. Under the server's usual
        -- case-insensitive default, any predicate later added over this column would treat 'Acme'
        -- and 'acme' as one tenant.
        TenantId             NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT DF_KeyEscrow_TenantId DEFAULT N'__untenanted__',
        Purpose              NVARCHAR(256)  NULL,
        Metadata             NVARCHAR(MAX)  NULL,
        -- Incremented in place on every recovery. NOT NULL with a zero default is load-bearing:
        -- the service updates it as RecoveryAttempts = RecoveryAttempts + 1, and NULL + 1 is
        -- NULL, so a nullable column would silently discard the count it exists to keep.
        RecoveryAttempts     INT            NOT NULL CONSTRAINT DF_KeyEscrow_RecoveryAttempts DEFAULT 0,
        LastRecoveryAttempt  DATETIMEOFFSET NULL,
        RevokedAt            DATETIMEOFFSET NULL,
        RevocationReason     NVARCHAR(1000) NULL,
        INDEX IX_KeyEscrow_KeyId_EscrowedAt (KeyId, EscrowedAt DESC)
    )
END
GO

-- At most ONE active escrow per key. This is not tidiness; it is the invariant the recovery path
-- already assumes. Recovery selects the active escrow for a key expecting exactly one row, so a
-- second active escrow for the same key does not shadow the first -- it makes BOTH unrecoverable,
-- and it does so silently until someone actually needs the key.
--
-- With this index the second escrow of a still-active key fails immediately, naming this
-- constraint, while an operator is present to revoke the old one first. Escrow history is
-- unaffected: the filter covers only State = 0 (Active), so revoked and recovered rows for the
-- same key accumulate normally.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE name = 'UX_KeyEscrow_ActiveKeyId'
      AND object_id = OBJECT_ID('[compliance].[KeyEscrow]'))
BEGIN
    CREATE UNIQUE INDEX UX_KeyEscrow_ActiveKeyId
        ON [compliance].[KeyEscrow] (KeyId)
        WHERE State = 0
END
GO

-- ---------------------------------------------------------------------------
-- Recovery tokens
-- ---------------------------------------------------------------------------
-- One row per custodian share issued in a batch. Recovering a key requires Threshold of the
-- TotalShares in a batch to be presented together.
--
-- NOTE WHAT IS NOT HERE: the share itself. ShareData is handed to the custodian and never
-- written to this table. A share column would put a quorum's worth of secret material in the
-- same database as the thing it protects, which would defeat split-knowledge recovery entirely.
-- If you are adding a column here, that is the one to not add.
IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance' AND t.name = 'RecoveryTokens')
BEGIN
    CREATE TABLE [compliance].[RecoveryTokens] (
        TokenId           NVARCHAR(64)   NOT NULL PRIMARY KEY,
        KeyId             NVARCHAR(256)  NOT NULL,
        EscrowId          NVARCHAR(64)   NOT NULL,
        -- The batch this share belongs to. Shares only reconstruct with other shares of the same
        -- batch, so this is NOT NULL: a share that cannot name its batch cannot be used.
        BatchId           NVARCHAR(64)   NOT NULL,
        ShareIndex        INT            NOT NULL,
        TotalShares       INT            NOT NULL,
        Threshold         INT            NOT NULL,
        CreatedAt         DATETIMEOFFSET NOT NULL,
        ExpiresAt         DATETIMEOFFSET NOT NULL,
        IsUsed            BIT            NOT NULL CONSTRAINT DF_RecoveryTokens_IsUsed DEFAULT 0,
        -- SHA-256 of the quorum secret (always 32 bytes), held SERVER-SIDE. Recovery verifies the
        -- reconstructed secret against this before any key material is touched, which is what
        -- makes a fabricated share set fail instead of succeed.
        --
        -- NOT NULL is the security property, not a formality: the commitment lookup ignores rows
        -- whose commitment is NULL, so a nullable column would let a row silently contribute
        -- nothing to the set being verified against -- weakening the check without failing it.
        SecretCommitment  VARBINARY(32)  NOT NULL,
        INDEX IX_RecoveryTokens_KeyId_Active (KeyId, IsUsed, ExpiresAt),
        INDEX IX_RecoveryTokens_EscrowId (EscrowId)
    )
END
GO

-- ---------------------------------------------------------------------------
-- Key escrow envelope wrap
-- ---------------------------------------------------------------------------
-- One row per token batch, holding the escrowed key bound under that batch's quorum: the key is
-- encrypted with a KEK derived from the reconstructed secret (inner layer), and that result is
-- encrypted again with the master key (outer layer).
--
-- Both layers are required. The master key alone strips the outer layer and reveals nothing --
-- it cannot derive the KEK, which exists only once a custodian quorum reassembles. This is the
-- row that makes "the operator of the database cannot unilaterally recover the key" true.
IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance' AND t.name = 'KeyEscrowWrap')
BEGIN
    CREATE TABLE [compliance].[KeyEscrowWrap] (
        EscrowId               NVARCHAR(64)   NOT NULL,
        BatchId                NVARCHAR(64)   NOT NULL,
        -- The commitment recovery matches on to select this row (32 bytes, as above).
        SecretCommitment       VARBINARY(32)  NOT NULL,
        -- Salt for the KEK derivation. 32 bytes today; sized with headroom so a future
        -- derivation change fails loudly on insert rather than truncating key-derivation input.
        KekSalt                VARBINARY(64)  NOT NULL,
        InnerIv                VARBINARY(64)  NOT NULL,
        InnerAuthTag           VARBINARY(64)  NOT NULL,
        WrappedInnerKey        VARBINARY(MAX) NOT NULL,
        OuterIv                VARBINARY(64)  NOT NULL,
        -- Nullable for the same reason as KeyEscrow.AuthTag: the outer layer is produced by the
        -- configured encryption provider, which need not be AEAD.
        OuterAuthTag           VARBINARY(64)  NULL,
        OuterAlgorithm         INT            NOT NULL,
        OuterMasterKeyId       NVARCHAR(256)  NOT NULL,
        OuterMasterKeyVersion  INT            NOT NULL,
        -- Envelope format version. Written on every wrap so that a future format change can be
        -- told apart from this one when reading rows written today.
        WrapVersion            INT            NOT NULL,
        CONSTRAINT PK_KeyEscrowWrap PRIMARY KEY (EscrowId, BatchId),
        -- Recovery selects this row by (EscrowId, SecretCommitment) expecting exactly one. That
        -- expectation is made true here rather than left to the improbability of a SHA-256
        -- collision: without this, a duplicate would throw during recovery, at the worst moment.
        CONSTRAINT UQ_KeyEscrowWrap_Commitment UNIQUE (EscrowId, SecretCommitment)
    )
END
GO
