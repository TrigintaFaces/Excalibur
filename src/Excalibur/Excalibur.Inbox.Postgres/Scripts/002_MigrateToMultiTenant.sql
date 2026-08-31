-- Postgres MIGRATION for Excalibur.Inbox.Postgres — SINGLE-TENANT -> MULTI-TENANT
-- Version: 2.0
--
-- Run this ONCE to grow an existing single-tenant inbox table (created by
-- 001_CreateInboxSchema.sql, keyed on the pair (message_id, handler_type), no tenant_id
-- column) into the multi-tenant schema (keyed on the triple, tenant_id NOT NULL) before
-- registering multi-tenancy.
--
-- This is an expand-contract migration. Existing pre-multi-tenant rows are anchored to
-- the reserved sentinel '__untenanted__': it is a concrete, non-null, reserved value
-- (the framework rejects that exact identifier, so it can never name a real tenant), so
-- migrated rows sit in their own partition and can never collide with a future real tenant.
-- Rows added after the migration carry their real tenant id.
--
-- Table/schema names use the defaults (public.inbox_messages); rename to match if overridden.
--
-- ORDERING / DOWNTIME: run this during a maintenance window (or with a dual-read strategy)
-- with the store stopped, because it rebuilds the primary key. After it completes,
-- register multi-tenancy and restart — the startup handshake then confirms the triple key.
--
-- OPTIONAL: if all pre-migration rows belong to one known first tenant, replace the
-- DEFAULT below (and the backfill) with that real tenant id instead of the sentinel.

-- 1) Add the tenant_id column, anchoring existing rows to the reserved sentinel.
ALTER TABLE public.inbox_messages
    ADD COLUMN IF NOT EXISTS tenant_id VARCHAR(64) NOT NULL DEFAULT '__untenanted__';

-- 2) Rebuild the unique key: drop the pair PK, add the triple PK.
ALTER TABLE public.inbox_messages DROP CONSTRAINT IF EXISTS pk_inbox_messages;
ALTER TABLE public.inbox_messages
    ADD CONSTRAINT pk_inbox_messages PRIMARY KEY (message_id, handler_type, tenant_id);

-- 3) Optional: drop the sentinel default now that the key is rebuilt, so future inserts
--    must supply a real tenant id (the store always binds one on the multi-tenant path).
ALTER TABLE public.inbox_messages ALTER COLUMN tenant_id DROP DEFAULT;
