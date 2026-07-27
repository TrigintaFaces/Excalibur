-- Postgres Schema for Excalibur.Inbox.Postgres — MULTI-TENANT
-- Version: 2.0
--
-- Creates the table required by the Postgres inbox store for idempotent message
-- processing in a MULTI-TENANT deployment (an ITenantContext is registered). The store
-- never creates this table at runtime: run this script against the target database
-- before the first message is processed.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     PostgresInboxOptions.SchemaName = "public"
--     PostgresInboxOptions.TableName  = "inbox_messages"
--
-- If you override either, rename the object below to match.
--
--
-- DEPLOYMENT MODE: MULTI-TENANT (use this ONLY when multi-tenancy is registered)
-- -----------------------------------------------------------------------------
-- The dedup/claim key is the TRIPLE (message_id, handler_type, tenant_id), and tenant_id
-- is NOT NULL. tenant_id is a component of identity, not an optional filter, so two
-- tenants carrying the same (message_id, handler_type) never dedup against each other and
-- ON CONFLICT always fires (a NULLABLE tenant_id would let pre-15 Postgres treat NULLs as
-- distinct so ON CONFLICT never matches — exactly-once silently broken).
--
-- A genuinely untenanted SYSTEM row (or a row anchored during a single-tenant→multi-tenant
-- migration) binds the reserved sentinel '__untenanted__'. The framework rejects that exact
-- identifier as a tenant id, so it can never collide with a real tenant.
--
-- If you do NOT register multi-tenancy, use the single-tenant script
-- (001_CreateInboxSchema.sql) instead — no tenant_id column. The store verifies at startup
-- that the physical schema matches the registered mode and FAILS FAST on a mismatch.

CREATE TABLE IF NOT EXISTS public.inbox_messages (
    message_id       TEXT         NOT NULL,
    handler_type     TEXT         NOT NULL,
    message_type     TEXT         NOT NULL,
    payload          BYTEA        NOT NULL,
    metadata         JSONB        NOT NULL DEFAULT '{}'::jsonb,
    received_at      TIMESTAMPTZ  NOT NULL,
    processed_at     TIMESTAMPTZ  NULL,
    status           INT          NOT NULL DEFAULT 0,
    retry_count      INT          NOT NULL DEFAULT 0,
    last_error       TEXT         NULL,
    last_attempt_at  TIMESTAMPTZ  NULL,
    lease_expires_at TIMESTAMPTZ  NULL,
    correlation_id   TEXT         NULL,
    tenant_id        TEXT         NOT NULL,
    source           TEXT         NULL,

    -- Multi-tenant: tenant is part of identity. The dedup/claim key is the triple.
    CONSTRAINT pk_inbox_messages PRIMARY KEY (message_id, handler_type, tenant_id)
);

CREATE INDEX IF NOT EXISTS ix_inbox_messages_status_received_at
    ON public.inbox_messages (status, received_at);
