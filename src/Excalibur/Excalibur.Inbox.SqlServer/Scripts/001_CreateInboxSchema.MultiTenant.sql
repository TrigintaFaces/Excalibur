-- SQL Server Schema for Excalibur.Inbox.SqlServer — MULTI-TENANT
-- Version: 2.0
--
-- Creates the table required by the SQL Server inbox store for idempotent message
-- processing in a MULTI-TENANT deployment (an ITenantContext is registered). The store
-- never creates this table at runtime: run this script against the target database
-- before the first message is processed.
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
-- DEPLOYMENT MODE: MULTI-TENANT (use this ONLY when multi-tenancy is registered)
-- -----------------------------------------------------------------------------
-- The dedup/claim key is the TRIPLE (MessageId, HandlerType, TenantId), and TenantId is
-- NOT NULL. TenantId is a component of identity, not an optional filter, so two tenants
-- carrying the same (MessageId, HandlerType) never dedup against each other. The store
-- binds the resolved tenant on every keyed path.
--
-- A genuinely untenanted SYSTEM row (or a row anchored during a single-tenant→multi-tenant
-- migration) binds the reserved sentinel '__untenanted__'. The framework rejects that exact
-- identifier as a tenant id, so the sentinel can never collide with a tenant literal.
--
-- If you do NOT register multi-tenancy, use the single-tenant script
-- (001_CreateInboxSchema.sql) instead — no TenantId column. The store verifies at startup
-- that the physical schema matches the registered mode and FAILS FAST on a mismatch.

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
        TenantId           NVARCHAR(255) COLLATE Latin1_General_BIN2    NOT NULL,
        Source             NVARCHAR(255)    NULL,

        -- Multi-tenant: tenant is part of identity. The dedup/claim key is the triple.
        CONSTRAINT PK_inbox_messages PRIMARY KEY (MessageId, HandlerType, TenantId)
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
