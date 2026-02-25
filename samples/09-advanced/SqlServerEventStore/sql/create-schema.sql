-- =====================================================
-- Excalibur Event Sourcing SQL Server Schema
-- =====================================================
-- This script creates the tables required for the
-- Excalibur.EventSourcing.SqlServer package.
--
-- Run this against your SQL Server database before
-- starting the application.
-- =====================================================

-- Create schema if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'eventsourcing')
BEGIN
    EXEC('CREATE SCHEMA eventsourcing')
END
GO

-- =====================================================
-- Event Store Table
-- =====================================================
-- Stores all domain events for event-sourced aggregates.
-- The combination of StreamId + Version ensures optimistic
-- concurrency control.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Events' AND schema_id = SCHEMA_ID('eventsourcing'))
BEGIN
    CREATE TABLE eventsourcing.Events (
        Id              BIGINT IDENTITY(1,1) NOT NULL,
        StreamId        NVARCHAR(255)        NOT NULL,
        Version         BIGINT               NOT NULL,
        EventType       NVARCHAR(500)        NOT NULL,
        Payload         NVARCHAR(MAX)        NOT NULL,
        Metadata        NVARCHAR(MAX)        NULL,
        CreatedAt       DATETIME2(7)         NOT NULL DEFAULT SYSUTCDATETIME(),
        DispatchedAt    DATETIME2(7)         NULL,

        CONSTRAINT PK_Events PRIMARY KEY (Id),
        CONSTRAINT UQ_Events_Stream_Version UNIQUE (StreamId, Version)
    );

    -- Index for loading events by stream
    CREATE INDEX IX_Events_StreamId ON eventsourcing.Events (StreamId, Version);

    -- Index for finding undispatched events
    CREATE INDEX IX_Events_Undispatched ON eventsourcing.Events (DispatchedAt)
        WHERE DispatchedAt IS NULL;

    PRINT 'Created eventsourcing.Events table'
END
GO

-- =====================================================
-- Snapshot Store Table
-- =====================================================
-- Stores aggregate snapshots for faster rehydration.
-- Only the latest snapshot per stream is typically needed.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Snapshots' AND schema_id = SCHEMA_ID('eventsourcing'))
BEGIN
    CREATE TABLE eventsourcing.Snapshots (
        Id              BIGINT IDENTITY(1,1) NOT NULL,
        StreamId        NVARCHAR(255)        NOT NULL,
        Version         BIGINT               NOT NULL,
        SnapshotType    NVARCHAR(500)        NOT NULL,
        Payload         NVARCHAR(MAX)        NOT NULL,
        CreatedAt       DATETIME2(7)         NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_Snapshots PRIMARY KEY (Id)
    );

    -- Index for loading latest snapshot
    CREATE INDEX IX_Snapshots_StreamId ON eventsourcing.Snapshots (StreamId, Version DESC);

    PRINT 'Created eventsourcing.Snapshots table'
END
GO

-- =====================================================
-- Outbox Table
-- =====================================================
-- Stores messages for reliable delivery using the
-- transactional outbox pattern.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Outbox' AND schema_id = SCHEMA_ID('eventsourcing'))
BEGIN
    CREATE TABLE eventsourcing.Outbox (
        Id              BIGINT IDENTITY(1,1) NOT NULL,
        MessageId       UNIQUEIDENTIFIER     NOT NULL DEFAULT NEWID(),
        MessageType     NVARCHAR(500)        NOT NULL,
        Payload         NVARCHAR(MAX)        NOT NULL,
        Destination     NVARCHAR(255)        NULL,
        CreatedAt       DATETIME2(7)         NOT NULL DEFAULT SYSUTCDATETIME(),
        PublishedAt     DATETIME2(7)         NULL,
        RetryCount      INT                  NOT NULL DEFAULT 0,
        LastError       NVARCHAR(MAX)        NULL,

        CONSTRAINT PK_Outbox PRIMARY KEY (Id),
        CONSTRAINT UQ_Outbox_MessageId UNIQUE (MessageId)
    );

    -- Index for finding unpublished messages
    CREATE INDEX IX_Outbox_Unpublished ON eventsourcing.Outbox (CreatedAt)
        WHERE PublishedAt IS NULL;

    PRINT 'Created eventsourcing.Outbox table'
END
GO

-- =====================================================
-- Projections Checkpoint Table
-- =====================================================
-- Tracks projection progress for replay/resume.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProjectionCheckpoints' AND schema_id = SCHEMA_ID('eventsourcing'))
BEGIN
    CREATE TABLE eventsourcing.ProjectionCheckpoints (
        ProjectionName  NVARCHAR(255)        NOT NULL,
        LastEventId     BIGINT               NOT NULL DEFAULT 0,
        LastUpdatedAt   DATETIME2(7)         NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_ProjectionCheckpoints PRIMARY KEY (ProjectionName)
    );

    PRINT 'Created eventsourcing.ProjectionCheckpoints table'
END
GO

-- =====================================================
-- Sample Queries
-- =====================================================

-- Load all events for a stream (ordered by version)
-- SELECT * FROM eventsourcing.Events
-- WHERE StreamId = @StreamId
-- ORDER BY Version;

-- Load events from a specific version
-- SELECT * FROM eventsourcing.Events
-- WHERE StreamId = @StreamId AND Version > @FromVersion
-- ORDER BY Version;

-- Get the latest snapshot for a stream
-- SELECT TOP 1 * FROM eventsourcing.Snapshots
-- WHERE StreamId = @StreamId
-- ORDER BY Version DESC;

-- Get unpublished outbox messages
-- SELECT TOP 100 * FROM eventsourcing.Outbox
-- WHERE PublishedAt IS NULL
-- ORDER BY CreatedAt;

PRINT 'Event sourcing schema setup complete!'
GO
