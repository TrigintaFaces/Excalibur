-- =============================================================================
-- 03-create-orders-tables.sql
-- Creates the LegacyOrders and LegacyOrderItems tables with CDC enabled
-- =============================================================================
-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: Apache-2.0
-- =============================================================================

USE [LegacyDb]
GO

-- =============================================================================
-- Create LegacyOrders table
-- =============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LegacyOrders')
BEGIN
    CREATE TABLE [dbo].[LegacyOrders]
    (
        -- V2 schema (2020) - current naming convention
        [ExternalOrderId]       NVARCHAR(50)    NOT NULL PRIMARY KEY,
        [CustomerExternalId]    NVARCHAR(50)    NOT NULL,
        [OrderDate]             DATETIME2       NOT NULL,
        [Status]                NVARCHAR(50)    NOT NULL DEFAULT 'Pending',
        [TotalAmount]           DECIMAL(18,2)   NOT NULL DEFAULT 0,
        [ShippedDate]           DATETIME2       NULL,
        [DeliveredDate]         DATETIME2       NULL,
        [CreatedAt]             DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]             DATETIME2       NULL,

        -- V1 columns aliased (for reference):
        -- OrderNum -> ExternalOrderId
        -- CustId -> CustomerExternalId
        -- OrderDt -> OrderDate
        -- OrderAmt -> TotalAmount
        -- ShipDt -> ShippedDate
        -- DelivDt -> DeliveredDate

        CONSTRAINT [FK_LegacyOrders_Customer]
            FOREIGN KEY ([CustomerExternalId])
            REFERENCES [dbo].[LegacyCustomers]([ExternalId])
    )

    CREATE INDEX [IX_LegacyOrders_CustomerExternalId] ON [dbo].[LegacyOrders]([CustomerExternalId])
    CREATE INDEX [IX_LegacyOrders_OrderDate] ON [dbo].[LegacyOrders]([OrderDate])
    CREATE INDEX [IX_LegacyOrders_Status] ON [dbo].[LegacyOrders]([Status])

    PRINT 'Created table LegacyOrders'
END
ELSE
BEGIN
    PRINT 'Table LegacyOrders already exists'
END
GO

-- =============================================================================
-- Create LegacyOrderItems table
-- =============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LegacyOrderItems')
BEGIN
    CREATE TABLE [dbo].[LegacyOrderItems]
    (
        -- V2 schema (2020) - current naming convention
        [ExternalItemId]    NVARCHAR(50)    NOT NULL PRIMARY KEY,
        [ExternalOrderId]   NVARCHAR(50)    NOT NULL,
        [ProductName]       NVARCHAR(200)   NOT NULL,
        [Quantity]          INT             NOT NULL DEFAULT 1,
        [UnitPrice]         DECIMAL(18,2)   NOT NULL DEFAULT 0,
        [CreatedAt]         DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]         DATETIME2       NULL,

        -- V1 columns aliased (for reference):
        -- LineNum -> ExternalItemId
        -- OrderNum -> ExternalOrderId
        -- ProdName -> ProductName
        -- Qty -> Quantity
        -- Price -> UnitPrice

        CONSTRAINT [FK_LegacyOrderItems_Order]
            FOREIGN KEY ([ExternalOrderId])
            REFERENCES [dbo].[LegacyOrders]([ExternalOrderId])
            ON DELETE CASCADE
    )

    CREATE INDEX [IX_LegacyOrderItems_ExternalOrderId] ON [dbo].[LegacyOrderItems]([ExternalOrderId])
    CREATE INDEX [IX_LegacyOrderItems_ProductName] ON [dbo].[LegacyOrderItems]([ProductName])

    PRINT 'Created table LegacyOrderItems'
END
ELSE
BEGIN
    PRINT 'Table LegacyOrderItems already exists'
END
GO

-- =============================================================================
-- Create audit triggers for UpdatedAt
-- =============================================================================

IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_LegacyOrders_UpdatedAt')
BEGIN
    EXEC('
    CREATE TRIGGER [dbo].[TR_LegacyOrders_UpdatedAt]
    ON [dbo].[LegacyOrders]
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        UPDATE [dbo].[LegacyOrders]
        SET [UpdatedAt] = GETUTCDATE()
        FROM [dbo].[LegacyOrders] o
        INNER JOIN inserted i ON o.[ExternalOrderId] = i.[ExternalOrderId]
    END
    ')
    PRINT 'Created trigger TR_LegacyOrders_UpdatedAt'
END
GO

IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_LegacyOrderItems_UpdatedAt')
BEGIN
    EXEC('
    CREATE TRIGGER [dbo].[TR_LegacyOrderItems_UpdatedAt]
    ON [dbo].[LegacyOrderItems]
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        UPDATE [dbo].[LegacyOrderItems]
        SET [UpdatedAt] = GETUTCDATE()
        FROM [dbo].[LegacyOrderItems] oi
        INNER JOIN inserted i ON oi.[ExternalItemId] = i.[ExternalItemId]
    END
    ')
    PRINT 'Created trigger TR_LegacyOrderItems_UpdatedAt'
END
GO

-- =============================================================================
-- Enable CDC on LegacyOrders table
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables t
               JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id
               WHERE t.name = 'LegacyOrders')
BEGIN
    EXEC sys.sp_cdc_enable_table
        @source_schema = N'dbo',
        @source_name   = N'LegacyOrders',
        @role_name     = NULL,
        @capture_instance = N'dbo_LegacyOrders',
        @supports_net_changes = 1

    PRINT 'Enabled CDC on table LegacyOrders (capture instance: dbo_LegacyOrders)'
END
ELSE
BEGIN
    PRINT 'CDC already enabled on table LegacyOrders'
END
GO

-- =============================================================================
-- Enable CDC on LegacyOrderItems table
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables t
               JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id
               WHERE t.name = 'LegacyOrderItems')
BEGIN
    EXEC sys.sp_cdc_enable_table
        @source_schema = N'dbo',
        @source_name   = N'LegacyOrderItems',
        @role_name     = NULL,
        @capture_instance = N'dbo_LegacyOrderItems',
        @supports_net_changes = 1

    PRINT 'Enabled CDC on table LegacyOrderItems (capture instance: dbo_LegacyOrderItems)'
END
ELSE
BEGIN
    PRINT 'CDC already enabled on table LegacyOrderItems'
END
GO

-- =============================================================================
-- Verify CDC setup for all tables
-- =============================================================================

PRINT ''
PRINT '=== CDC Verification for All Tables ==='
PRINT ''

SELECT
    SCHEMA_NAME(t.schema_id) + '.' + t.name as [TableName],
    ct.capture_instance as [CaptureInstance],
    ct.create_date as [EnabledDate],
    ct.supports_net_changes as [SupportsNetChanges]
FROM sys.tables t
JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id
ORDER BY t.name

GO

PRINT ''
PRINT 'Script 03-create-orders-tables.sql completed successfully'
GO
