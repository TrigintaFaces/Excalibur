-- =====================================================
-- Excalibur Event Sourcing SQL Server Schema
-- =====================================================
-- This script creates the tables required for the
-- Excalibur.EventSourcing.SqlServer package.
--
-- Run this against your SQL Server database before
-- starting the application.
--
-- Default schema: dbo
-- Default table names: EventStoreEvents, EventStoreSnapshots
-- (configurable via SqlServerEventSourcingOptions)
-- =====================================================

-- =====================================================
-- Event Store Table
-- =====================================================
-- Stores all domain events for event-sourced aggregates.
-- The combination of AggregateId + AggregateType + Version
-- ensures optimistic concurrency control.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EventStoreEvents')
BEGIN
    CREATE TABLE [dbo].[EventStoreEvents] (
        [Position]       BIGINT IDENTITY(1,1)  NOT NULL,
        [EventId]        NVARCHAR(256)         NOT NULL,
        [AggregateId]    NVARCHAR(256)         NOT NULL,
        [AggregateType]  NVARCHAR(256)         NOT NULL,
        [EventType]      NVARCHAR(256)         NOT NULL,
        [EventData]      VARBINARY(MAX)        NOT NULL,
        [Metadata]       VARBINARY(MAX)        NULL,
        [Version]        BIGINT                NOT NULL,
        [Timestamp]      DATETIMEOFFSET        NOT NULL,

        CONSTRAINT [PK_EventStoreEvents] PRIMARY KEY CLUSTERED ([Position]),
        CONSTRAINT [UQ_EventStoreEvents_Stream] UNIQUE ([AggregateId], [AggregateType], [Version])
    );

    -- Index for loading events by aggregate stream
    CREATE INDEX [IX_EventStoreEvents_AggregateId] ON [dbo].[EventStoreEvents] ([AggregateId], [AggregateType]);

    -- Index for querying by event type
    CREATE INDEX [IX_EventStoreEvents_EventType] ON [dbo].[EventStoreEvents] ([EventType]);

    PRINT 'Created dbo.EventStoreEvents table'
END
GO

-- =====================================================
-- Snapshot Store Table
-- =====================================================
-- Stores the latest aggregate snapshot for fast rehydration.
-- One row per aggregate (upserted via MERGE).

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EventStoreSnapshots')
BEGIN
    CREATE TABLE [dbo].[EventStoreSnapshots] (
        [SnapshotId]     NVARCHAR(256)         NOT NULL,
        [AggregateId]    NVARCHAR(256)         NOT NULL,
        [AggregateType]  NVARCHAR(256)         NOT NULL,
        [Version]        BIGINT                NOT NULL,
        [Data]           VARBINARY(MAX)        NOT NULL,
        [CreatedAt]      DATETIMEOFFSET        NOT NULL,

        CONSTRAINT [PK_EventStoreSnapshots] PRIMARY KEY CLUSTERED ([AggregateId], [AggregateType])
    );

    PRINT 'Created dbo.EventStoreSnapshots table'
END
GO

-- Outbox table: The unified outbox (dbo.OutboxMessages) is managed by
-- Excalibur.Outbox.SqlServer and is created separately via AddExcaliburOutbox().
-- It is NOT part of the event store schema.

-- =====================================================
-- Sample Queries
-- =====================================================

-- Load all events for an aggregate (ordered by version)
-- SELECT * FROM dbo.EventStoreEvents
-- WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType
-- ORDER BY Version;

-- Load events from a specific version
-- SELECT * FROM dbo.EventStoreEvents
-- WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType AND Version > @FromVersion
-- ORDER BY Version;

-- Get the latest snapshot for an aggregate
-- SELECT * FROM dbo.EventStoreSnapshots
-- WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType;

PRINT 'Event sourcing schema setup complete!'
GO
