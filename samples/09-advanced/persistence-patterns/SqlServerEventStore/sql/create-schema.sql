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
-- The combination of AggregateId + AggregateType + Version + TenantId
-- ensures optimistic concurrency control, scoped per tenant.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EventStoreEvents')
BEGIN
    CREATE TABLE [dbo].[EventStoreEvents] (
        [Position]       BIGINT IDENTITY(1,1)  NOT NULL,
        [EventId]        NVARCHAR(256)         NOT NULL,
        [AggregateId]    NVARCHAR(256)         NOT NULL,
        [AggregateType]  NVARCHAR(256)         NOT NULL,
        [EventType]      NVARCHAR(256)         NOT NULL,
        -- MUST be nullable. GDPR erasure tombstones an event by setting EventData to
        -- NULL while preserving its position in the stream. A NOT NULL column makes
        -- every erasure fail.
        [EventData]      VARBINARY(MAX)        NULL,
        [Metadata]       VARBINARY(MAX)        NULL,
        [Version]        BIGINT                NOT NULL,
        [Timestamp]      DATETIMEOFFSET        NOT NULL,
        -- Tenant discriminator: a component of the stream uniqueness key, so two tenants holding the same
        -- aggregate identifier occupy separate rows instead of colliding. NOT NULL and no default — the store
        -- writes a concrete term on every insert (the resolved tenant, or the '__untenanted__' sentinel when
        -- no tenant context is established), so an untenanted event is a named partition, never a NULL. The
        -- event store's read/erase paths bind this column unconditionally, so a range operation is never
        -- un-tenant-bound. Migrating an existing table: backfill absent/NULL rows to '__untenanted__' BEFORE
        -- the NOT NULL alter, or those rows become unreadable (tenant-bound reads match the sentinel, not NULL).
        [TenantId]       NVARCHAR(255) COLLATE Latin1_General_BIN2         NOT NULL,

        CONSTRAINT [PK_EventStoreEvents] PRIMARY KEY CLUSTERED ([Position]),
        CONSTRAINT [UQ_EventStoreEvents_Stream] UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId])
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
        [Metadata]       VARBINARY(MAX)        NULL,
        -- Part of the primary key so two tenants holding the same aggregate identifier occupy
        -- separate rows instead of overwriting one another. NOT NULL and no default:
        -- SQL Server forbids a nullable column in a PRIMARY KEY, and the reserved '__untenanted__'
        -- sentinel is the single-tenant value the store writes explicitly — never NULL, never an empty
        -- string — so omitting it must fail the INSERT, not silently land in that partition.
        [TenantId]       NVARCHAR(256) COLLATE Latin1_General_BIN2         NOT NULL,

        CONSTRAINT [PK_EventStoreSnapshots] PRIMARY KEY CLUSTERED ([AggregateId], [AggregateType], [TenantId])
    );

    PRINT 'Created dbo.EventStoreSnapshots table'
END
GO

-- Outbox table: dbo.OutboxMessages is NOT part of the event store schema, and it is
-- NOT created for you. AddOutbox(...) registers services only -- it runs no DDL. You
-- must create the table yourself before the first send, or dispatch fails with
-- "Invalid object name 'dbo.OutboxMessages'".
--
-- The outbox DDL is published at docs/patterns/outbox.md. (The reference
-- script under Excalibur.Outbox.SqlServer/Scripts is a repository artifact -- it is
-- not included in the NuGet package, so it is not on disk for package consumers.)

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
