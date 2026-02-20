-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: Apache-2.0

-- ============================================================================
-- CDC + Event Store + Elasticsearch Sample - Database Setup
-- ============================================================================
--
-- This script sets up the required databases for the sample:
--
--   1. SQL Server #1 (port 1433): LegacyDb with CDC enabled
--   2. SQL Server #2 (port 1434): EventStore (schema auto-created by framework)
--
-- Run this script after docker-compose up -d and containers are healthy.
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
-- SECTION 2: Run on SQL Server #2 (port 1434) - Event Store
-- ============================================================================
-- NOTE: Run this section on SQL Server #2 (port 1434)!

/*

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'EventStore')
BEGIN
    CREATE DATABASE EventStore;
    PRINT 'Created database: EventStore';
END
GO

-- The Excalibur.EventSourcing.SqlServer package will automatically create
-- the required schema (eventsourcing.Events, eventsourcing.Snapshots, etc.)
-- when the application first connects.

PRINT '';
PRINT '============================================================================';
PRINT 'SQL Server #2 (Event Store) setup complete!';
PRINT '';
PRINT 'The EventStore database is ready.';
PRINT 'Schema will be created automatically by the framework on first use.';
PRINT '============================================================================';

*/


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
