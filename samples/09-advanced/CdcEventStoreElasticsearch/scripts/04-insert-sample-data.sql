-- =============================================================================
-- 04-insert-sample-data.sql
-- Inserts sample data to demonstrate CDC event capture
-- =============================================================================
-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: Apache-2.0
-- =============================================================================
--
-- This script inserts sample data that will generate CDC events:
-- - INSERT events for new customers, orders, and order items
-- - UPDATE events for status changes and modifications
-- - The CDC processor will capture these changes and translate them to domain events
-- =============================================================================

USE [LegacyDb]
GO

-- =============================================================================
-- Insert Sample Customers (will generate INSERT CDC events)
-- =============================================================================

PRINT 'Inserting sample customers...'

-- Customer 1: Active high-value customer
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyCustomers] WHERE [ExternalId] = 'CUST-001')
BEGIN
    INSERT INTO [dbo].[LegacyCustomers] ([ExternalId], [Name], [Email], [Phone])
    VALUES ('CUST-001', 'Acme Corporation', 'orders@acme.com', '+1-555-0100')
END

-- Customer 2: Active regular customer
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyCustomers] WHERE [ExternalId] = 'CUST-002')
BEGIN
    INSERT INTO [dbo].[LegacyCustomers] ([ExternalId], [Name], [Email], [Phone])
    VALUES ('CUST-002', 'TechStart Inc', 'purchasing@techstart.io', '+1-555-0200')
END

-- Customer 3: New customer with minimal orders
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyCustomers] WHERE [ExternalId] = 'CUST-003')
BEGIN
    INSERT INTO [dbo].[LegacyCustomers] ([ExternalId], [Name], [Email], [Phone])
    VALUES ('CUST-003', 'Global Widgets LLC', 'info@globalwidgets.com', '+1-555-0300')
END

-- Customer 4: Customer with no phone
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyCustomers] WHERE [ExternalId] = 'CUST-004')
BEGIN
    INSERT INTO [dbo].[LegacyCustomers] ([ExternalId], [Name], [Email])
    VALUES ('CUST-004', 'Startup Dreams', 'hello@startupdreams.io')
END

-- Customer 5: International customer
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyCustomers] WHERE [ExternalId] = 'CUST-005')
BEGIN
    INSERT INTO [dbo].[LegacyCustomers] ([ExternalId], [Name], [Email], [Phone])
    VALUES ('CUST-005', 'Euro Electronics GmbH', 'orders@euroelec.de', '+49-30-12345')
END

PRINT 'Inserted 5 sample customers'
GO

-- =============================================================================
-- Insert Sample Orders (will generate INSERT CDC events)
-- =============================================================================

PRINT 'Inserting sample orders...'

-- Wait a moment to ensure CDC captures customers first
WAITFOR DELAY '00:00:01'

-- Order 1: Completed order for CUST-001
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrders] WHERE [ExternalOrderId] = 'ORD-2024-001')
BEGIN
    INSERT INTO [dbo].[LegacyOrders]
        ([ExternalOrderId], [CustomerExternalId], [OrderDate], [Status], [TotalAmount], [ShippedDate], [DeliveredDate])
    VALUES
        ('ORD-2024-001', 'CUST-001', '2024-01-15 09:30:00', 'Delivered', 2500.00, '2024-01-17 14:00:00', '2024-01-20 10:15:00')
END

-- Order 2: Shipped order for CUST-001
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrders] WHERE [ExternalOrderId] = 'ORD-2024-002')
BEGIN
    INSERT INTO [dbo].[LegacyOrders]
        ([ExternalOrderId], [CustomerExternalId], [OrderDate], [Status], [TotalAmount], [ShippedDate])
    VALUES
        ('ORD-2024-002', 'CUST-001', '2024-02-01 11:45:00', 'Shipped', 1750.00, '2024-02-03 16:30:00')
END

-- Order 3: Pending order for CUST-002
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrders] WHERE [ExternalOrderId] = 'ORD-2024-003')
BEGIN
    INSERT INTO [dbo].[LegacyOrders]
        ([ExternalOrderId], [CustomerExternalId], [OrderDate], [Status], [TotalAmount])
    VALUES
        ('ORD-2024-003', 'CUST-002', '2024-02-10 08:00:00', 'Pending', 890.00)
END

-- Order 4: Confirmed order for CUST-003
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrders] WHERE [ExternalOrderId] = 'ORD-2024-004')
BEGIN
    INSERT INTO [dbo].[LegacyOrders]
        ([ExternalOrderId], [CustomerExternalId], [OrderDate], [Status], [TotalAmount])
    VALUES
        ('ORD-2024-004', 'CUST-003', '2024-02-12 15:20:00', 'Confirmed', 3200.00)
END

-- Order 5: New order for CUST-005
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrders] WHERE [ExternalOrderId] = 'ORD-2024-005')
BEGIN
    INSERT INTO [dbo].[LegacyOrders]
        ([ExternalOrderId], [CustomerExternalId], [OrderDate], [Status], [TotalAmount])
    VALUES
        ('ORD-2024-005', 'CUST-005', GETUTCDATE(), 'Pending', 0.00)
END

PRINT 'Inserted 5 sample orders'
GO

-- =============================================================================
-- Insert Sample Order Items (will generate INSERT CDC events)
-- =============================================================================

PRINT 'Inserting sample order items...'

-- Wait a moment to ensure CDC captures orders first
WAITFOR DELAY '00:00:01'

-- Items for Order 1 (ORD-2024-001)
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrderItems] WHERE [ExternalItemId] = 'ITEM-001-A')
BEGIN
    INSERT INTO [dbo].[LegacyOrderItems]
        ([ExternalItemId], [ExternalOrderId], [ProductName], [Quantity], [UnitPrice])
    VALUES
        ('ITEM-001-A', 'ORD-2024-001', 'Enterprise Server License', 1, 2000.00)
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrderItems] WHERE [ExternalItemId] = 'ITEM-001-B')
BEGIN
    INSERT INTO [dbo].[LegacyOrderItems]
        ([ExternalItemId], [ExternalOrderId], [ProductName], [Quantity], [UnitPrice])
    VALUES
        ('ITEM-001-B', 'ORD-2024-001', 'Premium Support Package', 1, 500.00)
END

-- Items for Order 2 (ORD-2024-002)
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrderItems] WHERE [ExternalItemId] = 'ITEM-002-A')
BEGIN
    INSERT INTO [dbo].[LegacyOrderItems]
        ([ExternalItemId], [ExternalOrderId], [ProductName], [Quantity], [UnitPrice])
    VALUES
        ('ITEM-002-A', 'ORD-2024-002', 'Developer Workstation', 5, 350.00)
END

-- Items for Order 3 (ORD-2024-003)
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrderItems] WHERE [ExternalItemId] = 'ITEM-003-A')
BEGIN
    INSERT INTO [dbo].[LegacyOrderItems]
        ([ExternalItemId], [ExternalOrderId], [ProductName], [Quantity], [UnitPrice])
    VALUES
        ('ITEM-003-A', 'ORD-2024-003', 'Cloud Storage Plan - Annual', 1, 600.00)
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrderItems] WHERE [ExternalItemId] = 'ITEM-003-B')
BEGIN
    INSERT INTO [dbo].[LegacyOrderItems]
        ([ExternalItemId], [ExternalOrderId], [ProductName], [Quantity], [UnitPrice])
    VALUES
        ('ITEM-003-B', 'ORD-2024-003', 'API Access Tier 2', 1, 290.00)
END

-- Items for Order 4 (ORD-2024-004)
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrderItems] WHERE [ExternalItemId] = 'ITEM-004-A')
BEGIN
    INSERT INTO [dbo].[LegacyOrderItems]
        ([ExternalItemId], [ExternalOrderId], [ProductName], [Quantity], [UnitPrice])
    VALUES
        ('ITEM-004-A', 'ORD-2024-004', 'IoT Sensor Kit', 10, 250.00)
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrderItems] WHERE [ExternalItemId] = 'ITEM-004-B')
BEGIN
    INSERT INTO [dbo].[LegacyOrderItems]
        ([ExternalItemId], [ExternalOrderId], [ProductName], [Quantity], [UnitPrice])
    VALUES
        ('ITEM-004-B', 'ORD-2024-004', 'Gateway Hub', 2, 350.00)
END

-- Items for Order 5 (ORD-2024-005) - Fresh order for CDC demonstration
IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrderItems] WHERE [ExternalItemId] = 'ITEM-005-A')
BEGIN
    INSERT INTO [dbo].[LegacyOrderItems]
        ([ExternalItemId], [ExternalOrderId], [ProductName], [Quantity], [UnitPrice])
    VALUES
        ('ITEM-005-A', 'ORD-2024-005', 'Industrial Controller', 3, 450.00)
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[LegacyOrderItems] WHERE [ExternalItemId] = 'ITEM-005-B')
BEGIN
    INSERT INTO [dbo].[LegacyOrderItems]
        ([ExternalItemId], [ExternalOrderId], [ProductName], [Quantity], [UnitPrice])
    VALUES
        ('ITEM-005-B', 'ORD-2024-005', 'Safety Relay Module', 5, 120.00)
END

PRINT 'Inserted 9 sample order items'
GO

-- =============================================================================
-- Update order totals (will generate UPDATE CDC events)
-- =============================================================================

PRINT 'Updating order totals...'

WAITFOR DELAY '00:00:01'

-- Update Order 5 total (this will trigger CDC UPDATE event)
UPDATE [dbo].[LegacyOrders]
SET [TotalAmount] = (
    SELECT COALESCE(SUM([Quantity] * [UnitPrice]), 0)
    FROM [dbo].[LegacyOrderItems]
    WHERE [ExternalOrderId] = 'ORD-2024-005'
)
WHERE [ExternalOrderId] = 'ORD-2024-005'

PRINT 'Updated order totals'
GO

-- =============================================================================
-- Simulate status change (will generate UPDATE CDC event)
-- =============================================================================

PRINT 'Simulating status changes for CDC demonstration...'

WAITFOR DELAY '00:00:01'

-- Update Order 5 status to Confirmed (simulates business process)
UPDATE [dbo].[LegacyOrders]
SET [Status] = 'Confirmed'
WHERE [ExternalOrderId] = 'ORD-2024-005'

PRINT 'Updated Order 5 status to Confirmed'
GO

-- =============================================================================
-- Summary
-- =============================================================================

PRINT ''
PRINT '=== Sample Data Summary ==='
PRINT ''

SELECT 'Customers' as [Table], COUNT(*) as [Count] FROM [dbo].[LegacyCustomers]
UNION ALL
SELECT 'Orders', COUNT(*) FROM [dbo].[LegacyOrders]
UNION ALL
SELECT 'Order Items', COUNT(*) FROM [dbo].[LegacyOrderItems]

PRINT ''
PRINT 'Script 04-insert-sample-data.sql completed successfully'
PRINT ''
PRINT 'CDC events have been generated. Run the sample application to process them.'
GO
