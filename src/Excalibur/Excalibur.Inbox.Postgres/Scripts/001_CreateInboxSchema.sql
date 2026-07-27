-- Postgres Schema for Excalibur.Inbox.Postgres — SINGLE-TENANT (default)
-- Version: 2.0
--
-- Creates the table required by the Postgres inbox store for idempotent message
-- processing. The store never creates this table at runtime: run this script against
-- the target database before the first message is processed.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     PostgresInboxOptions.SchemaName = "public"
--     PostgresInboxOptions.TableName  = "inbox_messages"
--
-- If you override either, rename the object below to match.
--
--
-- DEPLOYMENT MODE: SINGLE-TENANT (the default; use this UNLESS you register multi-tenancy)
-- --------------------------------------------------------------------------------------
-- This is the single-tenant schema: the dedup/claim key is the pair
-- (message_id, handler_type) and there is NO tenant_id column — a single-tenant consumer
-- pays nothing for a tenant discriminator it never uses. Isolation is trivial: a single
-- tenant has no other tenant's rows to collide with, and ON CONFLICT keys on the pair.
--
-- For a MULTI-TENANT deployment (an ITenantContext is registered), use the sibling script
-- 001_CreateInboxSchema.MultiTenant.sql instead — it adds a NOT NULL tenant_id column to
-- the key. The store verifies at startup that the physical schema matches the registered
-- mode and FAILS FAST on a mismatch. To grow from single- to multi-tenant later, run the
-- expand-contract migration script (002_MigrateToMultiTenant.sql).

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
    source           TEXT         NULL,

    -- Single-tenant: the dedup/claim key is the pair. No tenant_id column.
    CONSTRAINT pk_inbox_messages PRIMARY KEY (message_id, handler_type)
);

CREATE INDEX IF NOT EXISTS ix_inbox_messages_status_received_at
    ON public.inbox_messages (status, received_at);
