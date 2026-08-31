-- PostgreSQL MIGRATION for Excalibur.Inbox.Postgres — TENANT COLUMN, PORTABLE MAXIMUM
-- Version: 1.0
--
-- Narrows public.inbox_messages.tenant_id from TEXT to VARCHAR(64) — the shape
-- 001_CreateInboxSchema.MultiTenant.sql now provisions. Run this ONLY against an inbox_messages
-- table created by an earlier version of that script, or grown from the single-tenant schema by an
-- earlier version of 002_MigrateToMultiTenant.sql (which also added the column as TEXT). A database
-- provisioned by the current scripts already has this shape, and this script detects that and does
-- nothing.
--
-- The identifiers below are the provider defaults (PostgresInboxOptions: schema "public", table
-- "inbox_messages"). If you override SchemaName or TableName, apply the same overrides here.
--
-- The SINGLE-TENANT schema (001_CreateInboxSchema.sql) has no tenant_id column and needs nothing
-- here; this script says so and refuses rather than reporting success over it.
--
--
-- WHY THE CREATE SCRIPT CANNOT DELIVER THIS ON ITS OWN
-- -----------------------------------------------------
-- 001 guards its table with CREATE TABLE IF NOT EXISTS. On a database that already ran an earlier
-- version, the guard sees the table and skips the whole definition, so the narrowed column never
-- arrives. Re-running 001 is not an upgrade path; this script is.
--
--
-- WHY THIS ONE IS NOT COSMETIC
-- -----------------------------
-- tenant_id is the third component of PRIMARY KEY (message_id, handler_type, tenant_id) — the
-- inbox's deduplication and claim key. Tenant is part of identity here, not a filter. TEXT is
-- unbounded, so on a database at the prior version two tenants whose identifiers agree in their
-- first 64 characters and differ after are DISTINCT, and their messages never dedup against each
-- other. Under VARCHAR(64) those same two identifiers are the SAME key. Truncating to reach the
-- narrower column would therefore merge two tenants' deduplication scopes: one tenant's message
-- would suppress another tenant's message as a duplicate, and that message would never be
-- processed. There is no error to observe when it happens. That is why the width is enforced here
-- by REFUSING, and why truncation is not an acceptable fallback.
--
--
-- WHY 64 AND NOT WIDER
-- ---------------------
-- 64 is the NARROWEST tenant column across every shipped provider, and the framework now rejects a
-- longer identifier where it is constructed, before it can reach a database. Fixing every provider
-- at the narrowest is the only choice that cannot truncate: an identifier accepted by the framework
-- must be storable whole by any provider it can reach.
--
--
-- WHAT HAPPENS TO EXISTING ROWS
-- ------------------------------
-- Nothing is backfilled, and no row acquires or loses a tenant: tenant_id was already NOT NULL in
-- the version this script upgrades from, so there is no absent value to fill. Every row keeps its
-- exact tenant bytes. A value LONGER than 64 characters is REFUSED, never truncated, and the
-- refusal names how many rows and the longest one. Re-key those rows to identifiers of 64
-- characters or fewer, then re-run.
--
-- Such rows can only predate the framework's length guard. A deployment that has only ever written
-- through a current release has none, and this check costs it one scan.
--
-- The column's DEFAULT is left exactly as it is. A table grown by 002_MigrateToMultiTenant.sql
-- carries the untenanted sentinel as its default and keeps it; a table created directly by
-- 001_CreateInboxSchema.MultiTenant.sql has no default and gains none. ALTER ... TYPE preserves
-- either, so both paths stay as their own create script left them.
--
--
-- COLLATION
-- ----------
-- No COLLATE clause below, and that omission is what makes an upgraded column match a fresh one.
-- ALTER COLUMN ... TYPE does not carry an explicit collation across: with no COLLATE clause the
-- column comes out under the DATABASE DEFAULT collation, which is what 001 gives a fresh install,
-- since it names none either. Both paths converge on one collation rather than on two that merely
-- look alike. That reset is harmless under any DETERMINISTIC collation, because PostgreSQL falls
-- back to a byte comparison for equality: 'Acme' and 'acme' remain two tenants whichever is in
-- force. Ordering changes; matching does not.
--
-- A NONDETERMINISTIC collation is REFUSED rather than reset. Under one, 'Acme' and 'acme' compare
-- EQUAL, so this table is ALREADY deduplicating two tenants against each other through the primary
-- key, with no error to show for it — and resetting the collation here would silently change which
-- rows collide, on live data, in the same statement that was supposed to be a width change. That is
-- a decision this script will not make for you.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Narrowing a column's length rewrites the table and takes an ACCESS EXCLUSIVE lock for the
-- duration, and rebuilds the primary key that tenant_id participates in. Run it in a maintenance
-- window sized for the table's row count, not alongside live inbox writes.
--
--
-- ONE THING THIS SCRIPT CANNOT DO IS SET THE PROCESS EXIT CODE, and that is worth stating because
-- the failure it leaves is silent. On a refusal psql still exits 0 unless it is told otherwise: it
-- prints the error, sends the rest of the file, turns the trailing COMMIT into a rollback, and
-- reports success. Nothing is kept -- but a pipeline branching on $? reads a refused, no-op
-- migration as a SUCCESS. If yours does, run this file as
--
--     psql -v ON_ERROR_STOP=1 -f <this script>
--
-- That setting is deliberately NOT written into the file. It is a psql CLIENT command, so every
-- other runner -- Npgsql, JDBC, Flyway, Liquibase, your own connection loop -- sends it to the
-- server and the whole script dies on it with 42601 syntax error at or near backslash, having
-- provisioned nothing at all. A client setting belongs on the invocation.

BEGIN;

DO $narrow_inbox_tenant_id$
DECLARE
    v_type       text;
    v_offenders  bigint;
    v_longest    integer;
    v_collation  text;
BEGIN
    IF to_regclass('public.inbox_messages') IS NULL THEN
        RAISE NOTICE '003: public.inbox_messages is not present; nothing to upgrade. Provision with 001_CreateInboxSchema.MultiTenant.sql instead.';
        RETURN;
    END IF;

    SELECT format_type(a.atttypid, a.atttypmod)
      INTO v_type
      FROM pg_attribute a
     WHERE a.attrelid = 'public.inbox_messages'::regclass
       AND a.attname = 'tenant_id'
       AND NOT a.attisdropped;

    IF v_type IS NULL THEN
        RAISE EXCEPTION '003 REFUSED: public.inbox_messages has no tenant_id column at all. A SINGLE-TENANT inbox (created by 001_CreateInboxSchema.sql) has no tenant column by design and needs nothing here — do not run this script against one. Otherwise this table did not come from any version of 001_CreateInboxSchema.MultiTenant.sql. Nothing has been changed.';
    END IF;

    IF v_type = 'character varying(64)' THEN
        RAISE NOTICE '003: public.inbox_messages.tenant_id is already character varying(64); nothing to do.';
        RETURN;
    END IF;

    -- A non-default collation is only ever explicit, so this cannot fire on a table 001 built.
    SELECT co.collname
      INTO v_collation
      FROM pg_attribute a
      JOIN pg_collation co ON co.oid = a.attcollation
     WHERE a.attrelid = 'public.inbox_messages'::regclass
       AND a.attname = 'tenant_id'
       AND NOT co.collisdeterministic;

    IF v_collation IS NOT NULL THEN
        RAISE EXCEPTION '003 REFUSED: public.inbox_messages.tenant_id carries the nondeterministic collation ''%''. Under it two tenant identifiers differing only in case or accent compare EQUAL, so this table is already deduplicating one tenant''s messages against another''s through the primary key, and nothing reports an error. Nothing has been changed. Re-create the column with the database default collation (001_CreateInboxSchema.MultiTenant.sql names none), reconcile the messages the merged comparison suppressed, then re-run.', v_collation;
    END IF;

    SELECT count(*), max(length(tenant_id))
      INTO v_offenders, v_longest
      FROM public.inbox_messages
     WHERE length(tenant_id) > 64;

    IF v_offenders > 0 THEN
        RAISE EXCEPTION '003 REFUSED: % row(s) in public.inbox_messages hold a tenant identifier longer than 64 characters (longest: %). Narrowing the column would truncate them, and two tenants sharing their first 64 characters would collapse onto one deduplication key: one tenant''s message would be suppressed as a duplicate of another tenant''s and never processed. Nothing has been changed. Re-key those rows to identifiers of 64 characters or fewer, then re-run.', v_offenders, v_longest;
    END IF;

    ALTER TABLE public.inbox_messages
        ALTER COLUMN tenant_id TYPE VARCHAR(64);

    RAISE NOTICE '003: public.inbox_messages.tenant_id narrowed from % to character varying(64).', v_type;
END
$narrow_inbox_tenant_id$;

COMMIT;
