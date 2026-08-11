-- SQL Server requires SET QUOTED_IDENTIFIER ON to create a FILTERED index (one with a WHERE
-- clause), and sqlcmd defaults it OFF. Without these, every filtered index below fails with
-- Msg 1934 and is simply absent from the resulting database -- a script runner that does not
-- check exit status gets a schema silently missing its most selective indexes.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- SQL Server Audit Store Migration Script
-- Creates the audit schema and tables for Excalibur.AuditLogging.SqlServer
-- Provides tamper-evident hash-chain audit logging

-- Create schema if not exists
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'audit')
BEGIN
    EXEC('CREATE SCHEMA [audit]');
END
GO

-- Create main audit events table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[audit].[AuditEvents]') AND type in (N'U'))
BEGIN
    CREATE TABLE [audit].[AuditEvents] (
        -- Identity and ordering
        [SequenceNumber] BIGINT IDENTITY(1,1) NOT NULL,
        [EventId] NVARCHAR(64) NOT NULL,

        -- Event classification
        [EventType] INT NOT NULL,
        [Action] NVARCHAR(100) NOT NULL,
        [Outcome] INT NOT NULL,
        [Timestamp] DATETIMEOFFSET(7) NOT NULL,

        -- Actor information
        [ActorId] NVARCHAR(256) NOT NULL,
        [ActorType] NVARCHAR(50) NULL,

        -- Resource information
        [ResourceId] NVARCHAR(256) NULL,
        [ResourceType] NVARCHAR(100) NULL,
        [ResourceClassification] INT NULL,

        -- Context and correlation
        -- Untenanted rows carry the sentinel rather than NULL, so a scoped predicate is a plain
        -- equality that cannot fail open. NULL would make every scoped comparison UNKNOWN, which is
        -- why the readers currently compensate per-statement with COALESCE -- a convention nothing
        -- forces the next statement author to follow.
        --
        -- The binary collation is required, not cosmetic: tenant identity is compared with an ordinal,
        -- case-SENSITIVE comparer in code, while an unpinned NVARCHAR inherits the server default,
        -- which is case-INsensitive on a stock SQL Server. Without the pin, 'Acme' and 'acme' are two
        -- tenants to the framework and one tenant to the database, and a scoped read returns rows the
        -- caller does not own. Binary storage comparison matches the code's comparer by construction.
        [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL DEFAULT '__untenanted__',
        [ApplicationName] NVARCHAR(256) NULL,
        [CorrelationId] NVARCHAR(64) NULL,
        [SessionId] NVARCHAR(64) NULL,

        -- Source information
        [IpAddress] NVARCHAR(45) NULL, -- IPv6 max length
        [UserAgent] NVARCHAR(500) NULL,

        -- Additional context
        [Reason] NVARCHAR(1000) NULL,
        [Metadata] NVARCHAR(MAX) NULL, -- JSON

        -- Hash chain integrity
        [PreviousEventHash] NVARCHAR(512) NULL, -- keyed integrity tag: v1:{keyId}:{base64-hmac}
        [EventHash] NVARCHAR(512) NOT NULL, -- keyed integrity tag: v1:{keyId}:{base64-hmac}

        -- Constraints
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY CLUSTERED ([SequenceNumber] ASC),
        CONSTRAINT [UQ_AuditEvents_EventId] UNIQUE NONCLUSTERED ([EventId])
    );
END
GO

-- Create indexes for common query patterns

-- Index for time-based queries (most common pattern for compliance reports)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditEvents_Timestamp' AND object_id = OBJECT_ID('[audit].[AuditEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditEvents_Timestamp]
    ON [audit].[AuditEvents] ([Timestamp] DESC)
    INCLUDE ([EventId], [EventType], [ActorId], [Outcome]);
END
GO

-- Index for tenant-scoped queries (multi-tenant scenarios)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditEvents_TenantId_Timestamp' AND object_id = OBJECT_ID('[audit].[AuditEvents]'))
BEGIN
    -- Deliberately unfiltered. The previous WHERE [TenantId] IS NOT NULL was written when untenanted
    -- rows were NULL; now they carry the sentinel, so a filter would exclude the untenanted partition
    -- from the only index its scoped reads can use.
    CREATE NONCLUSTERED INDEX [IX_AuditEvents_TenantId_Timestamp]
    ON [audit].[AuditEvents] ([TenantId], [Timestamp] DESC);
END
GO

-- Additive upgrade for databases created while TenantId was nullable. A NULL tenant is an untenanted
-- row, not an unknown one, so it is backfilled to the sentinel before the column is tightened. Runs
-- only when the column is still nullable, so it is safe to re-run.
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[audit].[AuditEvents]')
             AND name = N'TenantId' AND is_nullable = 1)
BEGIN
    UPDATE [audit].[AuditEvents] SET [TenantId] = '__untenanted__' WHERE [TenantId] IS NULL;

    -- An index on the column blocks the ALTER, so it is dropped here and recreated unfiltered at the
    -- end of this block.
    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditEvents_TenantId_Timestamp'
               AND object_id = OBJECT_ID('[audit].[AuditEvents]'))
    BEGIN
        DROP INDEX [IX_AuditEvents_TenantId_Timestamp] ON [audit].[AuditEvents];
    END

    ALTER TABLE [audit].[AuditEvents]
        ALTER COLUMN [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

    ALTER TABLE [audit].[AuditEvents]
        ADD CONSTRAINT [DF_AuditEvents_TenantId] DEFAULT '__untenanted__' FOR [TenantId];

    CREATE NONCLUSTERED INDEX [IX_AuditEvents_TenantId_Timestamp]
    ON [audit].[AuditEvents] ([TenantId], [Timestamp] DESC);
END
GO

-- Index for application-scoped queries (multi-application shared backends)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditEvents_ApplicationName_Timestamp' AND object_id = OBJECT_ID('[audit].[AuditEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditEvents_ApplicationName_Timestamp]
    ON [audit].[AuditEvents] ([ApplicationName], [Timestamp] DESC)
    WHERE [ApplicationName] IS NOT NULL;
END
GO

-- Index for actor-based queries (user activity investigation)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditEvents_ActorId_Timestamp' AND object_id = OBJECT_ID('[audit].[AuditEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditEvents_ActorId_Timestamp]
    ON [audit].[AuditEvents] ([ActorId], [Timestamp] DESC)
    INCLUDE ([EventType], [Action], [ResourceId]);
END
GO

-- Index for resource-based queries (data access tracking)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditEvents_ResourceId_Timestamp' AND object_id = OBJECT_ID('[audit].[AuditEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditEvents_ResourceId_Timestamp]
    ON [audit].[AuditEvents] ([ResourceId], [Timestamp] DESC)
    WHERE [ResourceId] IS NOT NULL;
END
GO

-- Index for correlation-based queries (request tracing)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditEvents_CorrelationId' AND object_id = OBJECT_ID('[audit].[AuditEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditEvents_CorrelationId]
    ON [audit].[AuditEvents] ([CorrelationId])
    WHERE [CorrelationId] IS NOT NULL;
END
GO

-- Index for event type filtering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditEvents_EventType_Timestamp' AND object_id = OBJECT_ID('[audit].[AuditEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditEvents_EventType_Timestamp]
    ON [audit].[AuditEvents] ([EventType], [Timestamp] DESC);
END
GO

-- Index for hash chain verification (sequential access)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditEvents_SequenceNumber_EventHash' AND object_id = OBJECT_ID('[audit].[AuditEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditEvents_SequenceNumber_EventHash]
    ON [audit].[AuditEvents] ([SequenceNumber] ASC)
    INCLUDE ([EventHash], [PreviousEventHash]);
END
GO

-- Index for retention cleanup (efficient deletion of old events)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditEvents_Timestamp_Cleanup' AND object_id = OBJECT_ID('[audit].[AuditEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditEvents_Timestamp_Cleanup]
    ON [audit].[AuditEvents] ([Timestamp] ASC)
    INCLUDE ([SequenceNumber]);
END
GO

-- Index for classification-based queries (sensitive data access)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditEvents_Classification_Timestamp' AND object_id = OBJECT_ID('[audit].[AuditEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditEvents_Classification_Timestamp]
    ON [audit].[AuditEvents] ([ResourceClassification], [Timestamp] DESC)
    WHERE [ResourceClassification] IS NOT NULL;
END
GO

-- Create the annotation table.
--
-- Annotations never modify an audit event or its hash chain: they live in their own table and link back
-- by [EventId]. This table is provisioned here, alongside [AuditEvents], because the annotation store
-- defaults to [audit].[AuditAnnotations] and a host that ran only this script would otherwise fail with
-- "Invalid object name 'audit.AuditAnnotations'" on its first annotate call.
--
-- TENANCY IS DERIVED, DELIBERATELY. There is no [TenantId] column here. The annotation store scopes every
-- read and write by joining to [AuditEvents] and applying the tenant predicate there, so an annotation
-- inherits the tenancy of the event it annotates and the two can never disagree. That derivation is only
-- sound while every annotation row is guaranteed to have a parent event, which is what the foreign key
-- below enforces -- it is a correctness constraint for tenant scoping, not housekeeping.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[audit].[AuditAnnotations]') AND type in (N'U'))
BEGIN
    CREATE TABLE [audit].[AuditAnnotations] (
        [Id] NVARCHAR(32) NOT NULL,
        [EventId] NVARCHAR(64) NOT NULL,

        -- 0=Tag, 1=Bookmark, 2=Note
        [AnnotationType] INT NOT NULL,

        -- Free text: a tag term, a bookmark label, or a note body. NVARCHAR(MAX) because a note has no
        -- sensible length bound; the store compares it by equality when de-duplicating, never by range.
        [Content] NVARCHAR(MAX) NOT NULL,

        [ActorId] NVARCHAR(256) NOT NULL,
        [CreatedAt] DATETIMEOFFSET(7) NOT NULL,

        -- 0=Personal, 1=Shared
        [Visibility] INT NOT NULL,

        CONSTRAINT [PK_AuditAnnotations] PRIMARY KEY ([Id]),

        -- Referential integrity to the annotated event. CASCADE so a retention sweep that deletes an
        -- expired audit event does not strand its annotations as orphans pointing at nothing -- and so
        -- an orphan can never exist for the tenant join above to fail to resolve.
        CONSTRAINT [FK_AuditAnnotations_AuditEvents] FOREIGN KEY ([EventId])
            REFERENCES [audit].[AuditEvents] ([EventId])
            ON DELETE CASCADE
    );
END
GO

-- Existing installs: add the foreign key if the table predates it.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[audit].[AuditAnnotations]') AND type in (N'U'))
   AND NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AuditAnnotations_AuditEvents')
BEGIN
    ALTER TABLE [audit].[AuditAnnotations] WITH CHECK
        ADD CONSTRAINT [FK_AuditAnnotations_AuditEvents] FOREIGN KEY ([EventId])
            REFERENCES [audit].[AuditEvents] ([EventId])
            ON DELETE CASCADE;
END
GO

-- Indexes for the annotation store's actual query shapes.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditAnnotations_EventId_AnnotationType' AND object_id = OBJECT_ID('[audit].[AuditAnnotations]'))
BEGIN
    -- Every read and the de-duplication guard filter on both columns together.
    CREATE INDEX [IX_AuditAnnotations_EventId_AnnotationType]
    ON [audit].[AuditAnnotations] ([EventId], [AnnotationType]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditAnnotations_ActorId_AnnotationType' AND object_id = OBJECT_ID('[audit].[AuditAnnotations]'))
BEGIN
    -- Bookmark upsert and removal are keyed by actor within an annotation type.
    CREATE INDEX [IX_AuditAnnotations_ActorId_AnnotationType]
    ON [audit].[AuditAnnotations] ([ActorId], [AnnotationType]);
END
GO

PRINT 'Audit schema and tables created successfully.';
GO
