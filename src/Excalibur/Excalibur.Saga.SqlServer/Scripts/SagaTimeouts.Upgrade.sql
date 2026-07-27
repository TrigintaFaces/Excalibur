-- Saga Timeouts UPGRADE script for SQL Server
-- Part of Excalibur.Saga.SqlServer package
--
-- Copyright (c) 2026 The Excalibur Project
-- See LICENSE files in project root for license information.
--
-- Run this ONLY against a SagaTimeouts table created by an earlier version of SagaTimeouts.sql, to add
-- the tenant discriminator and re-key the saga indexes on the ruled (TenantId, SagaId) saga identity.
-- A database provisioned from the current SagaTimeouts.sql already has this shape and needs nothing here.
--
-- Kept as a SEPARATE script deliberately: SagaTimeouts.sql is executed by tooling that splits it on
-- semicolons, and the guarded IF blocks below contain their own semicolons. Mixing them would make the
-- create script unsplittable. Execute this file with a tool that honours GO batch separators (sqlcmd,
-- SSMS, Azure Data Studio).
--
-- Each statement is guarded on the condition it repairs, so running this repeatedly is safe.

-- ---------------------------------------------------------------------------------------------
-- Upgrade path for a SagaTimeouts table created by an earlier version of this script. The CREATE
-- above is unguarded, so a consumer re-running this script on an existing database gets an error
-- rather than a migration; these statements are the supported way to bring an existing table to the
-- shape above, and each is guarded on the condition it repairs so re-running is safe.
-- ---------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'SagaTimeouts') AND name = N'TenantId')
BEGIN
    -- Added WITH the default so existing rows adopt the untenanted sentinel rather than NULL. These
    -- rows predate tenant-aware timeouts, so the untenanted partition is their correct home.
    ALTER TABLE SagaTimeouts
        ADD TenantId NVARCHAR(200) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT DF_SagaTimeouts_TenantId DEFAULT '__untenanted__';
END
GO

-- Replace the tenant-less saga indexes with their tenant-leading equivalents.
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SagaTimeouts_SagaId'
           AND object_id = OBJECT_ID(N'SagaTimeouts'))
BEGIN
    DROP INDEX IX_SagaTimeouts_SagaId ON SagaTimeouts;
END
GO

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SagaTimeouts_SagaId_TimeoutId'
           AND object_id = OBJECT_ID(N'SagaTimeouts'))
BEGIN
    DROP INDEX IX_SagaTimeouts_SagaId_TimeoutId ON SagaTimeouts;
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SagaTimeouts_TenantId_SagaId'
               AND object_id = OBJECT_ID(N'SagaTimeouts'))
BEGIN
    CREATE INDEX IX_SagaTimeouts_TenantId_SagaId ON SagaTimeouts (TenantId, SagaId);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SagaTimeouts_TenantId_SagaId_TimeoutId'
               AND object_id = OBJECT_ID(N'SagaTimeouts'))
BEGIN
    CREATE INDEX IX_SagaTimeouts_TenantId_SagaId_TimeoutId ON SagaTimeouts (TenantId, SagaId, TimeoutId);
END
GO
