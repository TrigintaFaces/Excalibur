-- SQL Server Schema for Excalibur.Outbox.SqlServer
-- Version: 1.0
--
-- Creates the tables required by the SQL Server outbox store, its transport delivery
-- records, and its dead letter queue. The store never creates these tables at runtime:
-- run this script against the target database before the first drain.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     SqlServerOutboxOptions.Tables.SchemaName           = "dbo"
--     SqlServerOutboxOptions.Tables.OutboxTableName      = "OutboxMessages"
--     SqlServerOutboxOptions.Tables.TransportsTableName  = "OutboxMessageTransports"
--     SqlServerDeadLetterQueueOptions.SchemaName         = "dbo"
--     SqlServerDeadLetterQueueOptions.TableName          = "DeadLetterQueue"
--
-- If you override any of those, rename the corresponding object below to match.
--
-- The script is idempotent: every statement is guarded, so it is safe to re-run and
-- safe to apply to a database whose outbox table was created by an earlier version.

-- ---------------------------------------------------------------------------
-- Outbox messages
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessages]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[OutboxMessages] (
        Id                NVARCHAR(255)    NOT NULL PRIMARY KEY,
        MessageType       NVARCHAR(500)    NOT NULL,
        Payload           VARBINARY(MAX)   NOT NULL,
        Headers           NVARCHAR(MAX)    NULL,
        Destination       NVARCHAR(255)    NOT NULL,
        CreatedAt         DATETIMEOFFSET   NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        ScheduledAt       DATETIMEOFFSET   NULL,
        SentAt            DATETIMEOFFSET   NULL,
        Status            INT              NOT NULL DEFAULT 0,
        RetryCount        INT              NOT NULL DEFAULT 0,
        LastError         NVARCHAR(MAX)    NULL,
        LastAttemptAt     DATETIMEOFFSET   NULL,
        CorrelationId     NVARCHAR(255)    NULL,
        CausationId       NVARCHAR(255)    NULL,
        TenantId          NVARCHAR(255) COLLATE Latin1_General_BIN2    NOT NULL DEFAULT '__untenanted__',
        Priority          INT              NOT NULL DEFAULT 0,
        TargetTransports  NVARCHAR(MAX)    NULL,
        IsMultiTransport  BIT              NOT NULL DEFAULT 0,
        LeasedAt          DATETIMEOFFSET   NULL,
        LeasedBy          NVARCHAR(255)    NULL,
        PartitionKey      NVARCHAR(256)    NULL,
        GroupKey          NVARCHAR(256)    NULL,
        SequenceNumber    BIGINT           NOT NULL DEFAULT 0,
        NextAttemptAt     DATETIMEOFFSET   NULL,
        FencingToken      BIGINT           NULL
    );
END
GO

-- Additive upgrade for databases created before FencingToken existed. Without this
-- column, every drain fails with: Msg 207, Invalid column name 'FencingToken'.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessages]') AND type = N'U')
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessages]') AND name = N'FencingToken')
BEGIN
    ALTER TABLE [dbo].[OutboxMessages] ADD FencingToken BIGINT NULL;
END
GO

-- Additive upgrade for databases created while TenantId was nullable. A NULL tenant is an
-- un-tenanted row that no tenant-scoped predicate can match and no scoped read can see, so it
-- is unreachable data rather than shared data. The framework never emits NULL: an unscoped
-- write binds the reserved '__untenanted__' sentinel, which is a concrete term. Existing NULLs
-- are therefore backfilled to that same sentinel before the column is tightened, so the
-- upgrade is value-preserving and the tightening cannot fail on legacy rows.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessages]') AND type = N'U')
   AND EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessages]')
                 AND name = N'TenantId' AND is_nullable = 1)
BEGIN
    UPDATE [dbo].[OutboxMessages] SET TenantId = '__untenanted__' WHERE TenantId IS NULL;
    ALTER TABLE [dbo].[OutboxMessages] ALTER COLUMN TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessages]') AND name = N'IX_OutboxMessages_Status_CreatedAt')
BEGIN
    CREATE NONCLUSTERED INDEX IX_OutboxMessages_Status_CreatedAt
        ON [dbo].[OutboxMessages] (Status, CreatedAt);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessages]') AND name = N'IX_OutboxMessages_Claim')
BEGIN
    CREATE NONCLUSTERED INDEX IX_OutboxMessages_Claim
        ON [dbo].[OutboxMessages] (Status, NextAttemptAt, PartitionKey, SequenceNumber);
END
GO

-- ---------------------------------------------------------------------------
-- Durable leadership-fence control table
-- ---------------------------------------------------------------------------
-- Holds ONE row per outbox table (keyed by the qualified outbox table name) recording the highest
-- leadership fencing token ever accepted — the durable high-water mark. It is deliberately SEPARATE
-- from OutboxMessages so that routine cleanup (which DELETEs sent, token-bearing message rows) can
-- never lower the recorded high-water. A superseded leader's stale token is therefore still rejected
-- after cleanup has purged the rows that carried the tokens. Cleanup MUST NOT reference this table.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OutboxFence]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[OutboxFence] (
        OutboxTable     NVARCHAR(512)  NOT NULL PRIMARY KEY,
        HighWaterToken  BIGINT         NOT NULL
    );
END
GO

-- ---------------------------------------------------------------------------
-- Per-transport delivery records (multi-transport fan-out)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessageTransports]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[OutboxMessageTransports] (
        Id                 NVARCHAR(255)   NOT NULL PRIMARY KEY,
        MessageId          NVARCHAR(255)   NOT NULL,
        TransportName      NVARCHAR(255)   NOT NULL,
        Destination        NVARCHAR(255)   NULL,
        Status             INT             NOT NULL DEFAULT 0,
        CreatedAt          DATETIMEOFFSET  NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        AttemptedAt        DATETIMEOFFSET  NULL,
        SentAt             DATETIMEOFFSET  NULL,
        RetryCount         INT             NOT NULL DEFAULT 0,
        LastError          NVARCHAR(MAX)   NULL,
        TransportMetadata  NVARCHAR(MAX)   NULL,
        -- The transport row carries its parent's tenant explicitly rather than inheriting it through
        -- MessageId. Without this column no tenant predicate is expressible on this table at all: every
        -- operational sweep, retry query and delivery audit is cross-tenant by construction -- not because a
        -- WHERE clause was forgotten, but because there is nothing to put in one.
        TenantId           NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL DEFAULT '__untenanted__',
        CONSTRAINT FK_OutboxMessageTransports_OutboxMessages
            FOREIGN KEY (MessageId) REFERENCES [dbo].[OutboxMessages](Id)
    );
END
GO

-- ---------------------------------------------------------------------------
-- Dead letter queue (SqlServerDeadLetterQueue)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DeadLetterQueue]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[DeadLetterQueue] (
        -- UNIQUEIDENTIFIER, not NVARCHAR: the enqueue path mints the id as a GUID and every read binds it
        -- back as one, so a string column makes the store unable to read back what it writes. The column
        -- type is the contract with the reader -- keep them in lockstep.
        Id                     UNIQUEIDENTIFIER NOT NULL,
        -- Originating tenant, carried as provenance so a replay re-enters the SAME tenant. NOT NULL and a
        -- component of the primary key: an untenanted entry stores the reserved '__untenanted__' sentinel,
        -- never NULL, so the key stays intact and the untenanted partition never collides with a real
        -- tenant. No default -- the enqueue path always supplies the value.
        TenantId               NVARCHAR(255) COLLATE Latin1_General_BIN2   NOT NULL,
        MessageType            NVARCHAR(500)   NOT NULL,
        Payload                VARBINARY(MAX)  NOT NULL,
        Reason                 INT             NOT NULL,
        ExceptionMessage       NVARCHAR(MAX)   NULL,
        ExceptionStackTrace    NVARCHAR(MAX)   NULL,
        EnqueuedAt             DATETIMEOFFSET  NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        OriginalAttempts       INT             NOT NULL DEFAULT 0,
        Metadata               NVARCHAR(MAX)   NULL,
        CorrelationId          NVARCHAR(255)   NULL,
        CausationId            NVARCHAR(255)   NULL,
        SourceQueue            NVARCHAR(255)   NULL,
        IsReplayed             BIT             NOT NULL DEFAULT 0,
        ReplayedAt             DATETIMEOFFSET  NULL,
        CONSTRAINT PK_DeadLetterQueue PRIMARY KEY (Id, TenantId)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[DeadLetterQueue]') AND name = N'IX_DeadLetterQueue_EnqueuedAt')
BEGIN
    CREATE NONCLUSTERED INDEX IX_DeadLetterQueue_EnqueuedAt
        ON [dbo].[DeadLetterQueue] (EnqueuedAt);
END
GO

-- ---------------------------------------------------------------------------
-- Re-collate an already-migrated install
-- ---------------------------------------------------------------------------
-- The tightening block above is guarded on is_nullable = 1, so it does nothing for a consumer who
-- already ran it. That install has TenantId NOT NULL in the server's default collation -- typically
-- case-INSENSITIVE. Those are the installs that have already adopted multi-tenancy, so they are the
-- only ones holding more than one tenant's rows, and therefore the only ones that can leak:
-- 'Acme' matches 'acme' and the tenant predicate fails OPEN.
--
-- Guarded on the COLLATION, not on nullability, so it reaches exactly that population and is a
-- no-op afterwards (and on a fresh install, created pinned above).
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessages]')
             AND name = N'TenantId'
             AND collation_name <> N'Latin1_General_BIN2')
BEGIN
    -- TenantId participates in no index on this table, so it alters in place.
    ALTER TABLE [dbo].[OutboxMessages]
        ALTER COLUMN TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL;
END
GO

-- Same treatment for the dead-letter table, where TenantId IS part of the primary key and the
-- key must therefore be dropped and rebuilt around the alter. Run with the processor stopped.
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[dbo].[DeadLetterQueue]')
             AND name = N'TenantId'
             AND collation_name <> N'Latin1_General_BIN2')
BEGIN
    IF EXISTS (SELECT * FROM sys.key_constraints
               WHERE parent_object_id = OBJECT_ID(N'[dbo].[DeadLetterQueue]')
                 AND name = N'PK_DeadLetterQueue' AND type = N'PK')
    BEGIN
        ALTER TABLE [dbo].[DeadLetterQueue] DROP CONSTRAINT PK_DeadLetterQueue;
    END

    ALTER TABLE [dbo].[DeadLetterQueue]
        ALTER COLUMN TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL;

    ALTER TABLE [dbo].[DeadLetterQueue]
        ADD CONSTRAINT PK_DeadLetterQueue PRIMARY KEY (Id, TenantId);
END
GO

-- ---------------------------------------------------------------------------
-- Upgrade: give an existing transports table its own tenant term
-- ---------------------------------------------------------------------------
-- The CREATE above is guarded on the table being absent, so an existing install never receives this
-- column from it. Existing rows are anchored to the reserved sentinel: their tenant is recoverable from
-- the parent message, and the two-step cleanup deletes transports before messages, so the parent is
-- still present when this runs.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessageTransports]') AND type = N'U')
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessageTransports]') AND name = N'TenantId')
BEGIN
    ALTER TABLE [dbo].[OutboxMessageTransports]
        ADD TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT DF_OutboxMessageTransports_TenantId DEFAULT '__untenanted__';

    -- Recover each existing row's real tenant from its parent while the parent still exists.
    UPDATE t
       SET t.TenantId = m.TenantId
      FROM [dbo].[OutboxMessageTransports] t
      JOIN [dbo].[OutboxMessages] m ON m.Id = t.MessageId;
END
GO

-- Re-collate an already-added transports tenant column (see the notes on the sibling tables).
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessageTransports]')
             AND name = N'TenantId'
             AND collation_name <> N'Latin1_General_BIN2')
BEGIN
    ALTER TABLE [dbo].[OutboxMessageTransports]
        ALTER COLUMN TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL;
END
GO

-- ---------------------------------------------------------------------------
-- Repair transport rows stranded with the untenanted sentinel
-- ---------------------------------------------------------------------------
-- The parent-recovery UPDATE above lives inside the "column does not yet exist" guard, so it runs
-- exactly once — at the moment the column is added — and never again. Any transport row written
-- AFTER that point by a writer which does not supply the tenant takes the column DEFAULT and is
-- never revisited: the recovery that would have fixed it has already run.
--
-- That window is real and it has occurred: the column shipped before the insert path was updated to
-- write the term, so rows created in between claim the untenanted partition while their parent
-- message belongs to a real tenant.
--
-- This block is guarded on the DATA rather than on the schema, so it repairs that window whenever it
-- is run and is a no-op once clean. It is deliberately conservative: it only touches rows whose
-- parent carries a REAL tenant, so a genuinely untenanted row — parent and child both holding the
-- sentinel — is left alone. Untenanted is a legitimate partition, not a defect to be repaired away.
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessageTransports]') AND name = N'TenantId')
BEGIN
    UPDATE t
       SET t.TenantId = m.TenantId
      FROM [dbo].[OutboxMessageTransports] t
      JOIN [dbo].[OutboxMessages] m ON m.Id = t.MessageId
     WHERE t.TenantId = '__untenanted__'
       AND m.TenantId <> '__untenanted__';
END
GO
