-- Oracle Schema for Excalibur.Inbox.Oracle — MULTI-TENANT
-- Version: 2.0
--
-- Creates the table required by the Oracle inbox store for idempotent message
-- processing in a MULTI-TENANT deployment (an ITenantContext is registered). The store
-- never creates this table at runtime: run this script against the target database
-- before the first message is processed.
--
-- Table and schema names are configurable. This script uses the default table name:
--
--     OracleInboxOptions.TableName = "INBOX_MESSAGES"
--
-- If you override it (or qualify with a schema), rename the object below to match.
--
-- Oracle has no "CREATE TABLE IF NOT EXISTS"; re-running raises ORA-00955, safe to ignore.
--
--
-- DEPLOYMENT MODE: MULTI-TENANT (use this ONLY when multi-tenancy is registered)
-- -----------------------------------------------------------------------------
-- The dedup/claim key is the TRIPLE (MessageId, HandlerType, TenantId), and TenantId is
-- NOT NULL. TenantId is a component of identity, not an optional filter, so two tenants
-- carrying the same (MessageId, HandlerType) never dedup against each other; first-writer-
-- wins is enforced by the triple PRIMARY KEY (a duplicate INSERT raises ORA-00001).
--
-- A genuinely untenanted SYSTEM row (or a row anchored during a single-tenant→multi-tenant
-- migration) binds the reserved sentinel '__untenanted__'. The framework rejects that exact
-- identifier as a tenant id, so it can never collide with a real tenant.
--
-- If you do NOT register multi-tenancy, use the single-tenant script
-- (001_CreateInboxSchema.sql) instead — no TenantId column. The store verifies at startup
-- that the physical schema matches the registered mode and FAILS FAST on a mismatch.

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
    TenantId           VARCHAR2(255)                   NOT NULL,
    Source             VARCHAR2(255),

    -- Multi-tenant: tenant is part of identity. The dedup/claim key is the triple.
    CONSTRAINT PK_INBOX_MESSAGES PRIMARY KEY (MessageId, HandlerType, TenantId)
);

CREATE INDEX IX_INBOX_MESSAGES_STATUS_RECV
    ON INBOX_MESSAGES (Status, ReceivedAt);
