-- =============================================================================
-- 01-create-legacy-db.sql
-- Creates the legacy database and initial tables for CDC sample
-- =============================================================================
-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: Apache-2.0
-- =============================================================================

USE [master]
GO

-- Create the legacy database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'LegacyDb')
BEGIN
    CREATE DATABASE [LegacyDb]
    PRINT 'Created database LegacyDb'
END
ELSE
BEGIN
    PRINT 'Database LegacyDb already exists'
END
GO

USE [LegacyDb]
GO

-- =============================================================================
-- Create LegacyCustomers table
-- This represents the source of truth in the legacy system
-- =============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LegacyCustomers')
BEGIN
    CREATE TABLE [dbo].[LegacyCustomers]
    (
        -- V2 schema (2020) - current naming convention
        [ExternalId]    NVARCHAR(50)    NOT NULL PRIMARY KEY,
        [Name]          NVARCHAR(200)   NOT NULL,
        [Email]         NVARCHAR(200)   NOT NULL,
        [Phone]         NVARCHAR(50)    NULL,
        [CreatedAt]     DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]     DATETIME2       NULL,

        -- V1 columns kept for backwards compatibility (aliased in some views)
        -- CustId -> ExternalId
        -- CustomerName -> Name

        CONSTRAINT [UQ_LegacyCustomers_Email] UNIQUE ([Email])
    )

    CREATE INDEX [IX_LegacyCustomers_Email] ON [dbo].[LegacyCustomers]([Email])
    CREATE INDEX [IX_LegacyCustomers_Name] ON [dbo].[LegacyCustomers]([Name])

    PRINT 'Created table LegacyCustomers'
END
ELSE
BEGIN
    PRINT 'Table LegacyCustomers already exists'
END
GO

-- =============================================================================
-- Create audit trigger for UpdatedAt
-- =============================================================================

IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_LegacyCustomers_UpdatedAt')
BEGIN
    EXEC('
    CREATE TRIGGER [dbo].[TR_LegacyCustomers_UpdatedAt]
    ON [dbo].[LegacyCustomers]
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        UPDATE [dbo].[LegacyCustomers]
        SET [UpdatedAt] = GETUTCDATE()
        FROM [dbo].[LegacyCustomers] c
        INNER JOIN inserted i ON c.[ExternalId] = i.[ExternalId]
    END
    ')
    PRINT 'Created trigger TR_LegacyCustomers_UpdatedAt'
END
GO

PRINT 'Script 01-create-legacy-db.sql completed successfully'
GO
