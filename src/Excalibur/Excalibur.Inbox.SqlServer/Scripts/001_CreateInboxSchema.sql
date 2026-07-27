-- SQL Server Schema for Excalibur.Inbox.SqlServer — SINGLE-TENANT (default)
-- Version: 2.0
--
-- Creates the table required by the SQL Server inbox store for idempotent message
-- processing. The store never creates this table at runtime: run this script against
-- the target database before the first message is processed.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     SqlServerInboxOptions.SchemaName = "dbo"
--     SqlServerInboxOptions.TableName  = "inbox_messages"
--
-- If you override either, rename the object below to match.
--
-- The script is idempotent: every statement is guarded, so it is safe to re-run.
--
--
-- DEPLOYMENT MODE: SINGLE-TENANT (the default; use this UNLESS you register multi-tenancy)
-- --------------------------------------------------------------------------------------
-- This is the single-tenant schema: the dedup/claim key is the pair
-- (MessageId, HandlerType) and there is NO TenantId column — a single-tenant consumer
-- pays nothing for a tenant discriminator it never uses. Isolation is trivial: a single
-- tenant has no other tenant's rows to collide with.
--
-- For a MULTI-TENANT deployment (an ITenantContext is registered), use the sibling
-- script 001_CreateInboxSchema.MultiTenant.sql instead — it adds a NOT NULL TenantId
-- column to the key. The store verifies at startup that the physical schema matches the
-- registered mode and FAILS FAST on a mismatch (a multi-tenant store can never silently
-- run against this single-tenant schema). To grow from single- to multi-tenant later,
-- run the expand-contract migration script (002_MigrateToMultiTenant.sql).

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[inbox_messages]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[inbox_messages] (
        MessageId          NVARCHAR(255)    NOT NULL,
        HandlerType        NVARCHAR(500)    NOT NULL,
        MessageType        NVARCHAR(500)    NOT NULL,
        Payload            VARBINARY(MAX)   NOT NULL,
        Metadata           NVARCHAR(MAX)    NULL,
        ReceivedAt         DATETIMEOFFSET   NOT NULL,
        ProcessedAt        DATETIMEOFFSET   NULL,
        Status             INT              NOT NULL DEFAULT 0,
        RetryCount         INT              NOT NULL DEFAULT 0,
        LastError          NVARCHAR(MAX)    NULL,
        LastAttemptAt      DATETIMEOFFSET   NULL,
        NextAttemptAt      DATETIMEOFFSET   NULL,
        LeaseExpiresAtUtc  DATETIMEOFFSET   NULL,
        CorrelationId      NVARCHAR(255)    NULL,
        Source             NVARCHAR(255)    NULL,

        -- Single-tenant: the dedup/claim key is the pair. No TenantId column.
        CONSTRAINT PK_inbox_messages PRIMARY KEY (MessageId, HandlerType)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[inbox_messages]') AND name = N'IX_inbox_messages_Status_ReceivedAt')
BEGIN
    CREATE NONCLUSTERED INDEX IX_inbox_messages_Status_ReceivedAt
        ON [dbo].[inbox_messages] (Status, ReceivedAt);
END
GO
