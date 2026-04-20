-- =============================================================================
-- 02-enable-cdc.sql
-- Enables Change Data Capture on the database and LegacyCustomers table
-- =============================================================================
-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: Apache-2.0
-- =============================================================================
--
-- PREREQUISITES:
-- - SQL Server must be Enterprise, Developer, or Standard edition (2016 SP1+)
-- - SQL Server Agent must be running for CDC capture job
--
-- IMPORTANT NOTES:
-- - CDC requires SQL Server Agent to be running
-- - The CDC capture job runs every 5 seconds by default
-- - The cleanup job runs daily at 2 AM by default
-- =============================================================================

USE [LegacyDb]
GO

-- =============================================================================
-- Enable CDC on the database
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'LegacyDb' AND is_cdc_enabled = 1)
BEGIN
    EXEC sys.sp_cdc_enable_db
    PRINT 'Enabled CDC on database LegacyDb'
END
ELSE
BEGIN
    PRINT 'CDC already enabled on database LegacyDb'
END
GO

-- =============================================================================
-- Enable CDC on LegacyCustomers table
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables t
               JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id
               WHERE t.name = 'LegacyCustomers')
BEGIN
    EXEC sys.sp_cdc_enable_table
        @source_schema = N'dbo',
        @source_name   = N'LegacyCustomers',
        @role_name     = NULL,                      -- No role restriction
        @capture_instance = N'dbo_LegacyCustomers', -- Capture instance name
        @supports_net_changes = 1                   -- Enable net changes query

    PRINT 'Enabled CDC on table LegacyCustomers (capture instance: dbo_LegacyCustomers)'
END
ELSE
BEGIN
    PRINT 'CDC already enabled on table LegacyCustomers'
END
GO

-- =============================================================================
-- Verify CDC setup
-- =============================================================================

PRINT ''
PRINT '=== CDC Verification ==='
PRINT ''

-- Check database CDC status
SELECT
    'Database CDC Status' as [Check],
    name as [DatabaseName],
    CASE WHEN is_cdc_enabled = 1 THEN 'Enabled' ELSE 'Disabled' END as [Status]
FROM sys.databases
WHERE name = 'LegacyDb'

-- Check table CDC status
SELECT
    'Table CDC Status' as [Check],
    SCHEMA_NAME(t.schema_id) + '.' + t.name as [TableName],
    ct.capture_instance as [CaptureInstance],
    ct.create_date as [EnabledDate]
FROM sys.tables t
JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id

-- Check CDC jobs
SELECT
    'CDC Jobs' as [Check],
    j.name as [JobName],
    CASE WHEN j.enabled = 1 THEN 'Enabled' ELSE 'Disabled' END as [Status]
FROM msdb.dbo.sysjobs j
WHERE j.name LIKE 'cdc.LegacyDb%'

GO

PRINT ''
PRINT 'Script 02-enable-cdc.sql completed successfully'
PRINT ''
PRINT 'IMPORTANT: Ensure SQL Server Agent is running for CDC to capture changes!'
GO
