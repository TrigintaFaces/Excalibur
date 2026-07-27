-- Oracle Schema for Excalibur.Inbox.Oracle — SINGLE-TENANT (default)
-- Version: 2.0
--
-- Creates the table required by the Oracle inbox store for idempotent message
-- processing. The store never creates this table at runtime: run this script against
-- the target database before the first message is processed.
--
-- Table and schema names are configurable. This script uses the default table name:
--
--     OracleInboxOptions.TableName = "INBOX_MESSAGES"
--
-- If you override it (or qualify with a schema), rename the object below to match.
--
-- Oracle has no "CREATE TABLE IF NOT EXISTS"; re-running this script against an
-- existing table raises ORA-00955 (name is already used by an existing object),
-- which is safe to ignore.
--
--
-- DEPLOYMENT MODE: SINGLE-TENANT (the default; use this UNLESS you register multi-tenancy)
-- --------------------------------------------------------------------------------------
-- This is the single-tenant schema: first-writer-wins is enforced by the PRIMARY KEY on
-- the pair (MessageId, HandlerType) and there is NO TenantId column — a single-tenant
-- consumer pays nothing for a tenant discriminator it never uses. Isolation is trivial: a
-- single tenant has no other tenant's rows to collide with.
--
-- For a MULTI-TENANT deployment (an ITenantContext is registered), use the sibling script
-- 001_CreateInboxSchema.MultiTenant.sql instead — it adds a NOT NULL TenantId column to
-- the key. The store verifies at startup that the physical schema matches the registered
-- mode and FAILS FAST on a mismatch. To grow from single- to multi-tenant later, run the
-- expand-contract migration script (002_MigrateToMultiTenant.sql).

CREATE TABLE INBOX_MESSAGES (
    MessageId          VARCHAR2(255)                   NOT NULL,
    HandlerType        VARCHAR2(500)                   NOT NULL,
    MessageType        VARCHAR2(500),
    Payload            BLOB,
    Metadata           CLOB,
    ReceivedAt         TIMESTAMP(7) WITH TIME ZONE     NOT NULL,
    ProcessedAt        TIMESTAMP(7) WITH TIME ZONE,
    Status             NUMBER(10)     DEFAULT 0        NOT NULL,
    LastError          VARCHAR2(4000),
    RetryCount         NUMBER(10)     DEFAULT 0        NOT NULL,
    LastAttemptAt      TIMESTAMP(7) WITH TIME ZONE,
    NextAttemptAt      TIMESTAMP(7) WITH TIME ZONE,
    LeaseExpiresAtUtc  TIMESTAMP(7) WITH TIME ZONE,
    CorrelationId      VARCHAR2(255),
    Source             VARCHAR2(255),

    -- Single-tenant: the dedup/claim key is the pair. No TenantId column.
    CONSTRAINT PK_INBOX_MESSAGES PRIMARY KEY (MessageId, HandlerType)
);

CREATE INDEX IX_INBOX_MESSAGES_STATUS_RECV
    ON INBOX_MESSAGES (Status, ReceivedAt);
