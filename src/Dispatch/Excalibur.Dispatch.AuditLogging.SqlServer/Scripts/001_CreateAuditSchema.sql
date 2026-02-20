-- SQL Server Audit Store Migration Script
-- Creates the audit schema and tables for Excalibur.Dispatch.AuditLogging.SqlServer
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
        [TenantId] NVARCHAR(64) NULL,
        [CorrelationId] NVARCHAR(64) NULL,
        [SessionId] NVARCHAR(64) NULL,

        -- Source information
        [IpAddress] NVARCHAR(45) NULL, -- IPv6 max length
        [UserAgent] NVARCHAR(500) NULL,

        -- Additional context
        [Reason] NVARCHAR(1000) NULL,
        [Metadata] NVARCHAR(MAX) NULL, -- JSON

        -- Hash chain integrity
        [PreviousEventHash] NVARCHAR(64) NULL, -- SHA-256 hex
        [EventHash] NVARCHAR(64) NOT NULL, -- SHA-256 hex

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
    CREATE NONCLUSTERED INDEX [IX_AuditEvents_TenantId_Timestamp]
    ON [audit].[AuditEvents] ([TenantId], [Timestamp] DESC)
    WHERE [TenantId] IS NOT NULL;
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

PRINT 'Audit schema and tables created successfully.';
GO
