-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: Apache-2.0

-- ============================================================================
-- CDC + Event Store + Elasticsearch Sample - Database Setup
-- ============================================================================
--
-- This script sets up the required databases for the sample:
--
--   1. SQL Server #1 (port 1433): LegacyDb with CDC enabled
--   2. SQL Server #2 (port 1434): EventStore and its tables
--
-- setup-databases.sh applies this file for you: it splits the file at the SECTION 2 banner
-- and sends each half to the server that section targets. Run it after docker-compose up -d
-- and the containers are healthy. To apply the file by hand instead, run each section
-- separately against its own instance.
--
-- ============================================================================
-- IMPORTANT: Run each section on the appropriate SQL Server instance!
-- ============================================================================

-- ============================================================================
-- SECTION 1: Run on SQL Server #1 (port 1433) - CDC Source
-- ============================================================================

-- Create the legacy database
USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'LegacyDb')
BEGIN
    CREATE DATABASE LegacyDb;
    PRINT 'Created database: LegacyDb';
END
GO

USE LegacyDb;
GO

-- Enable CDC on the database
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'LegacyDb' AND is_cdc_enabled = 1)
BEGIN
    EXEC sys.sp_cdc_enable_db;
    PRINT 'Enabled CDC on LegacyDb';
END
GO

-- Create the legacy customers table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LegacyCustomers')
BEGIN
    CREATE TABLE dbo.LegacyCustomers
    (
        -- V1 column names (some legacy systems have different naming)
        CustId          NVARCHAR(50)    NOT NULL PRIMARY KEY,
        CustomerName    NVARCHAR(200)   NOT NULL,
        Email           NVARCHAR(200)   NULL,
        Phone           NVARCHAR(50)    NULL,
        Address         NVARCHAR(500)   NULL,
        City            NVARCHAR(100)   NULL,
        Country         NVARCHAR(100)   NULL,
        CreatedDate     DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDate    DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        IsActive        BIT             NOT NULL DEFAULT 1,

        -- V2 column names (added in migration)
        ExternalId      AS CustId PERSISTED,    -- Computed column for ACL compatibility
        Name            AS CustomerName PERSISTED
    );

    PRINT 'Created table: dbo.LegacyCustomers';
END
GO

-- Enable CDC on the LegacyCustomers table
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id
    WHERE t.name = 'LegacyCustomers'
)
BEGIN
    EXEC sys.sp_cdc_enable_table
        @source_schema = N'dbo',
        @source_name = N'LegacyCustomers',
        @role_name = NULL,
        @capture_instance = N'dbo_LegacyCustomers',
        @supports_net_changes = 1;

    PRINT 'Enabled CDC on dbo.LegacyCustomers';
END
GO

-- Create an index for efficient lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LegacyCustomers_Email')
BEGIN
    CREATE INDEX IX_LegacyCustomers_Email ON dbo.LegacyCustomers(Email);
    PRINT 'Created index: IX_LegacyCustomers_Email';
END
GO

-- Verify CDC is enabled
SELECT
    'CDC Enabled Tables' AS Info,
    s.name AS SchemaName,
    t.name AS TableName,
    ct.capture_instance AS CaptureInstance
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id;
GO

PRINT '';
PRINT '============================================================================';
PRINT 'SQL Server #1 (CDC Source) setup complete!';
PRINT '';
PRINT 'The LegacyCustomers table is now CDC-enabled.';
PRINT 'Capture instance: dbo_LegacyCustomers';
PRINT '============================================================================';
GO

-- ============================================================================
-- CDC Processing State Table
-- ============================================================================
-- The Excalibur CDC processor tracks its position per table using this table.
-- Without it, the processor cannot resume after restarts.
--
-- Default schema: [Cdc]    (configurable via SqlServerCdcStateStoreOptions.SchemaName)
-- Default table:  [CdcProcessingState] (configurable via SqlServerCdcStateStoreOptions.TableName)
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Cdc')
BEGIN
    EXEC('CREATE SCHEMA [Cdc]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[Cdc].[CdcProcessingState]') AND type = N'U')
BEGIN
    CREATE TABLE [Cdc].[CdcProcessingState]
    (
        [MessageId]                     BIGINT IDENTITY(1,1)    NOT NULL,
        [DatabaseConnectionIdentifier]  NVARCHAR(256)           NOT NULL,
        [DatabaseName]                  NVARCHAR(256)           NOT NULL,
        [TableName]                     NVARCHAR(256)           NOT NULL,
        [LastProcessedLsn]              BINARY(10)              NOT NULL,
        [LastProcessedSequenceValue]    BINARY(10)              NULL,
        [LastCommitTime]                DATETIME2               NULL,
        [FencingToken]                  BIGINT                  NULL,
        [ProcessedAt]                   DATETIMEOFFSET          NOT NULL DEFAULT SYSDATETIMEOFFSET(),

        CONSTRAINT [PK_CdcProcessingState] PRIMARY KEY CLUSTERED ([MessageId]),
        CONSTRAINT [UQ_CdcProcessingState_Key] UNIQUE ([DatabaseConnectionIdentifier], [DatabaseName], [TableName])
    );

    PRINT 'Created [Cdc].[CdcProcessingState] table';
END
GO


-- ============================================================================
-- SECTION 2: Run on SQL Server #2 (port 1434) - Event Store
-- ============================================================================
-- NOTE: Connect to SQL Server #2 (port 1434) before running this section!
--
-- The Excalibur.EventSourcing.SqlServer package does NOT auto-create tables.
-- You must create the database and tables before running the application.
-- ============================================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'EventStore')
BEGIN
    CREATE DATABASE EventStore;
    PRINT 'Created database: EventStore';
END
GO

USE EventStore;
GO

-- Events table (stores domain events per aggregate stream)
-- Default table name: dbo.EventStoreEvents (configurable via SqlServerEventSourcingOptions)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EventStoreEvents')
BEGIN
    CREATE TABLE [dbo].[EventStoreEvents]
    (
        [Position]       BIGINT IDENTITY(1,1)  NOT NULL,
        [EventId]        NVARCHAR(255)         NOT NULL,
        [AggregateId]    NVARCHAR(255)         NOT NULL,
        [AggregateType]  NVARCHAR(255)         NOT NULL,
        [EventType]      NVARCHAR(255)         NOT NULL,
        -- MUST be nullable. GDPR erasure tombstones an event by setting EventData to
        -- NULL while preserving its position in the stream. A NOT NULL column makes
        -- every erasure fail.
        [EventData]      VARBINARY(MAX)        NULL,
        [Metadata]       VARBINARY(MAX)        NULL,
        [Version]        BIGINT                NOT NULL,
        [Timestamp]      DATETIMEOFFSET        NOT NULL,
        [TenantId]       NVARCHAR(64) COLLATE Latin1_General_BIN2         NOT NULL
            CONSTRAINT [DF_EventStoreEvents_TenantId] DEFAULT '__untenanted__',

        CONSTRAINT [PK_EventStoreEvents] PRIMARY KEY CLUSTERED ([Position]),
        CONSTRAINT [UQ_EventStoreEvents_Stream] UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId])
    );

    CREATE INDEX [IX_EventStoreEvents_AggregateId] ON [dbo].[EventStoreEvents]([AggregateId], [AggregateType]);
    CREATE INDEX [IX_EventStoreEvents_EventType] ON [dbo].[EventStoreEvents]([EventType]);

    PRINT 'Created table: dbo.EventStoreEvents';
END
GO

-- Snapshots table (stores latest aggregate snapshot for fast rehydration)
-- Default table name: dbo.EventStoreSnapshots (configurable via SqlServerEventSourcingOptions)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EventStoreSnapshots')
BEGIN
    CREATE TABLE [dbo].[EventStoreSnapshots]
    (
        [SnapshotId]     NVARCHAR(255)         NOT NULL,
        [AggregateId]    NVARCHAR(255)         NOT NULL,
        [AggregateType]  NVARCHAR(255)         NOT NULL,
        [Version]        BIGINT                NOT NULL,
        [Data]           VARBINARY(MAX)        NOT NULL,
        [CreatedAt]      DATETIMEOFFSET        NOT NULL,
        [Metadata]       VARBINARY(MAX)        NULL,
        -- Part of the primary key so two tenants holding the same aggregate identifier occupy
        -- separate rows instead of overwriting one another. NOT NULL and no default:
        -- SQL Server forbids a nullable column in a PRIMARY KEY, and the reserved '__untenanted__' sentinel is the single-tenant value the
        -- store writes explicitly — omitting it must fail the INSERT, not silently land in that partition.
        [TenantId]       NVARCHAR(64) COLLATE Latin1_General_BIN2         NOT NULL,

        CONSTRAINT [PK_EventStoreSnapshots] PRIMARY KEY CLUSTERED ([AggregateId], [AggregateType], [TenantId])
    );

    PRINT 'Created table: dbo.EventStoreSnapshots';
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

-- ============================================================================
-- Identity Map (Anti-Corruption Layer ID resolution)
-- ============================================================================
-- Maps external legacy identifiers to internal aggregate IDs. This is what makes
-- CDC replay IDEMPOTENT: when the same legacy row is re-delivered after a restart
-- or a capture-instance reset, the handler resolves it back to the SAME aggregate
-- instead of creating a new one.
--
-- The table must be durable for that to hold across restarts, which is why this
-- sample registers the SQL Server identity map rather than the in-memory provider
-- (AddInMemoryIdentityMap is documented for testing/development only).
--
-- Copied verbatim from the canonical script at
-- Excalibur.Data.IdentityMap.SqlServer/Scripts/CreateIdentityMapTable.sql.

IF OBJECT_ID(N'dbo.IdentityMap', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[IdentityMap] (
        ExternalSystem  NVARCHAR(128)    NOT NULL,
        ExternalId      NVARCHAR(256)    NOT NULL,
        AggregateType   NVARCHAR(256)    NOT NULL,
        AggregateId     NVARCHAR(256)    NOT NULL,
        CreatedAt       DATETIMEOFFSET   NOT NULL CONSTRAINT DF_IdentityMap_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIMEOFFSET   NOT NULL CONSTRAINT DF_IdentityMap_UpdatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_IdentityMap PRIMARY KEY CLUSTERED (ExternalSystem, ExternalId, AggregateType)
    );

    CREATE NONCLUSTERED INDEX IX_IdentityMap_AggregateId
        ON [dbo].[IdentityMap] (AggregateType, AggregateId);

    PRINT 'Created dbo.IdentityMap table';
END;
GO

PRINT '';
PRINT '============================================================================';
PRINT 'SQL Server #2 (Event Store) setup complete!';
PRINT '';
PRINT 'Tables created:';
PRINT '  - dbo.EventStoreEvents     (domain events)';
PRINT '  - dbo.EventStoreSnapshots  (aggregate snapshots)';
PRINT '  - dbo.IdentityMap          (external-to-internal ID resolution)';
PRINT '';
PRINT 'Note: The outbox table (dbo.OutboxMessages) is managed separately by';
PRINT '      Excalibur.Outbox.SqlServer via services.AddExcalibur(x => x.AddOutbox(...)).';
PRINT '============================================================================';
GO


-- ============================================================================
-- SECTION 3: Optional - Insert test data (run on SQL Server #1)
-- ============================================================================

/*
USE LegacyDb;
GO

-- Insert sample customers to trigger CDC changes
INSERT INTO dbo.LegacyCustomers (CustId, CustomerName, Email, Phone, City, Country)
VALUES
    ('CUST-001', 'Alice Johnson', 'alice@example.com', '+1-555-0101', 'New York', 'USA'),
    ('CUST-002', 'Bob Smith', 'bob@example.com', '+1-555-0102', 'Los Angeles', 'USA'),
    ('CUST-003', 'Carol Davis', 'carol@example.com', '+1-555-0103', 'Chicago', 'USA'),
    ('CUST-004', 'David Lee', 'david@example.com', '+1-555-0104', 'Houston', 'USA'),
    ('CUST-005', 'Eve Wilson', 'eve@example.com', '+1-555-0105', 'Phoenix', 'USA');

PRINT 'Inserted 5 sample customers';

-- Update a customer to generate CDC change
UPDATE dbo.LegacyCustomers
SET CustomerName = 'Alice M. Johnson', Email = 'alice.johnson@example.com', ModifiedDate = SYSUTCDATETIME()
WHERE CustId = 'CUST-001';

PRINT 'Updated customer CUST-001';

-- Delete a customer to generate CDC change
DELETE FROM dbo.LegacyCustomers WHERE CustId = 'CUST-005';

PRINT 'Deleted customer CUST-005';

*/
