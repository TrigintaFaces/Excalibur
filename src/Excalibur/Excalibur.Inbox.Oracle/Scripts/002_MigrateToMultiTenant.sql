-- Oracle MIGRATION for Excalibur.Inbox.Oracle — SINGLE-TENANT -> MULTI-TENANT
-- Version: 2.0
--
-- Run this ONCE to grow an existing single-tenant inbox table (created by
-- 001_CreateInboxSchema.sql, keyed on the pair (MessageId, HandlerType), no TenantId
-- column) into the multi-tenant schema (keyed on the triple, TenantId NOT NULL) before
-- registering multi-tenancy.
--
-- This is an expand-contract migration. Existing pre-multi-tenant rows are anchored to
-- the reserved sentinel '__untenanted__': it is a concrete, non-null, reserved value
-- (the framework rejects it as a tenant id), so migrated rows sit in their
-- own partition and can never collide with a future real tenant. Note DEFAULT '__untenanted__'
-- is Oracle-legal, whereas DEFAULT '' is not (Oracle folds '' to NULL). Rows added after
-- the migration carry their real tenant id.
--
-- Table name uses the default (INBOX_MESSAGES); rename to match if overridden.
--
-- ORDERING / DOWNTIME: run this during a maintenance window with the store stopped, because
-- it rebuilds the primary key. After it completes, register multi-tenancy and restart — the
-- startup handshake then confirms the triple key.
--
-- OPTIONAL: if all pre-migration rows belong to one known first tenant, replace the DEFAULT
-- below with that real tenant id instead of the sentinel.

-- 1) Add the TenantId column NOT NULL, anchoring existing rows to the reserved sentinel.
ALTER TABLE INBOX_MESSAGES ADD (TenantId VARCHAR2(64) DEFAULT '__untenanted__' NOT NULL);

-- 2) Rebuild the unique key: drop the pair PK, add the triple PK.
ALTER TABLE INBOX_MESSAGES DROP CONSTRAINT PK_INBOX_MESSAGES;
ALTER TABLE INBOX_MESSAGES ADD CONSTRAINT PK_INBOX_MESSAGES PRIMARY KEY (MessageId, HandlerType, TenantId);

-- 3) Optional: drop the sentinel default now that the key is rebuilt, so future inserts
--    must supply a real tenant id (the store always binds one on the multi-tenant path).
ALTER TABLE INBOX_MESSAGES MODIFY (TenantId DEFAULT NULL);
