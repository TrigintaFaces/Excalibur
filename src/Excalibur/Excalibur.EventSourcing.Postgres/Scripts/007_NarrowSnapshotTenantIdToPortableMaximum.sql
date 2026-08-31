-- PostgreSQL MIGRATION for Excalibur.EventSourcing.Postgres — SNAPSHOT TENANT KEY, PORTABLE MAXIMUM
-- Version: 1.0
--
-- Narrows public.event_store_snapshots.tenant_id from VARCHAR(255) to VARCHAR(64) — the shape
-- 001_CreateSnapshotSchema.sql now provisions. Run this ONLY against an event_store_snapshots table
-- created by an earlier version of that script. A database provisioned from the current script
-- already has this shape, and this script detects that and does nothing.
--
-- Table and schema names are configurable; this script uses the defaults 001 uses
-- (public.event_store_snapshots). If you overrode either, edit the literals below to match.
--
--
-- WHY THE CREATE SCRIPT CANNOT DELIVER THIS ON ITS OWN
-- -----------------------------------------------------
-- 001 guards its table with CREATE TABLE IF NOT EXISTS. On a database that already ran an earlier
-- version, the guard sees the table and skips the whole definition, so the narrowed column never
-- arrives. Re-running 001 is not an upgrade path; this script is.
--
--
-- WHY 64 AND NOT WIDER
-- ---------------------
-- 64 is the NARROWEST tenant column across every shipped provider, and the framework now rejects a
-- longer identifier where it is constructed, before it can reach a database. Fixing every provider
-- at the narrowest is the only choice that cannot truncate: an identifier the framework accepts
-- must be storable whole by any provider it can reach. A column wider than that guard is not
-- harmless slack — it is a provider that accepts what a sibling provider silently shortens.
--
--
-- WHAT HAPPENS TO EXISTING ROWS
-- ------------------------------
-- Nothing is backfilled, and no row changes partition. tenant_id was already NOT NULL in the
-- version this script upgrades from, so there is no absent value to fill and no row is re-filed:
-- every row keeps its exact tenant bytes, under its existing primary key.
--
-- A value LONGER than 64 characters is REFUSED, never truncated. Truncation here is not a lossy
-- label, it is a KEY MERGE: tenant_id is a component of PRIMARY KEY (aggregate_id, aggregate_type,
-- tenant_id), so two tenants whose identifiers share their first 64 characters stop being two rows.
-- One tenant snapshot then satisfies the other tenant upsert target, and the store overwrites a
-- snapshot it does not own — the exact cross-tenant overwrite the triple key exists to make
-- inexpressible. So this script stops and names the rows rather than choosing for you. Re-key the
-- reported rows to identifiers of 64 characters or fewer, then re-run.
--
-- Such rows can only predate the length guard the framework now applies at construction. A
-- deployment that has only ever written through a current release has none.
--
--
-- NO DEFAULT IS SET, AND THAT IS DELIBERATE
-- -------------------------------------------
-- 001 gives this column no DEFAULT on purpose: tenant_id is a component of IDENTITY, not an
-- optional filter, and you do not default a key column. With a default, an INSERT that omitted the
-- tenant would land silently in the untenanted partition instead of failing outright. This script
-- therefore narrows the type and adds no default. Adding one to "match the create script" would
-- introduce exactly the divergence it looks like it is preventing.
--
--
-- COLLATION
-- ----------
-- No COLLATE clause below, and that omission is what makes an upgraded column match a fresh one.
-- ALTER COLUMN ... TYPE does NOT carry an explicit collation across: with no COLLATE clause the
-- column comes out under the DATABASE DEFAULT collation, which is exactly what 001 gives a fresh
-- install, since it names no collation either. Both paths converge on one collation rather than on
-- two that merely look alike. (Measured on PostgreSQL 16: a column declared COLLATE "C" reads back
-- as the default after a plain ALTER ... TYPE, and keeps "C" only if the clause is restated.)
--
-- That reset is harmless for a DETERMINISTIC collation, and only because of what deterministic
-- means: PostgreSQL falls back to a byte comparison for equality under any of them, so 'Acme' and
-- 'acme' remain two tenants whichever one is in force. Ordering changes; matching does not, and
-- neither does the set of rows the primary key considers distinct.
--
-- A NONDETERMINISTIC collation is REFUSED rather than reset. Under one, 'Acme' and 'acme' compare
-- EQUAL, and because this column is IN THE PRIMARY KEY that is not merely a read matching too much:
-- the two tenants cannot both hold a snapshot for the same aggregate, because the key already
-- treats them as one row. Resetting the collation here would silently re-split rows the key had
-- merged, in the same statement that was supposed to be a width change, so this refuses instead.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Narrowing a column length rewrites the table and takes an ACCESS EXCLUSIVE lock for the duration,
-- and rebuilds the primary key index that tenant_id participates in. Run it during a maintenance
-- window with the store stopped, as 005 and 006 require for the same reason.

-- ONE THING THIS SCRIPT CANNOT DO IS SET THE PROCESS EXIT CODE, and that is worth stating because
-- the failure it leaves is silent. On a refusal psql still exits 0 unless it is told otherwise: it
-- prints the error, sends the rest of the file (every statement failing against the now-aborted
-- transaction), turns the trailing COMMIT into a rollback, and reports success. Nothing is kept --
-- but a pipeline branching on $? reads a refused, no-op migration as a SUCCESS. If yours does, run
-- this file as
--
--     psql -v ON_ERROR_STOP=1 -f <this script>
--
-- That setting is deliberately NOT written into the file. It is a psql CLIENT command, so every
-- other runner -- Npgsql, JDBC, Flyway, Liquibase, a migration tool, your own connection loop --
-- sends it to the server and the whole script dies on it with 42601 syntax error at or near
-- backslash, having provisioned nothing at all. A client setting belongs on the invocation. This
-- script is meant to be applied by whatever your deployment already uses, not only by psql.

BEGIN;

DO $narrow_snapshot_tenant_id$
DECLARE
    v_type       text;
    v_offenders  bigint;
    v_longest    integer;
    v_collation  text;
BEGIN
    IF to_regclass('public.event_store_snapshots') IS NULL THEN
        RAISE NOTICE '007: public.event_store_snapshots is not present; nothing to upgrade. Provision with 001_CreateSnapshotSchema.sql instead.';
        RETURN;
    END IF;

    SELECT format_type(a.atttypid, a.atttypmod)
      INTO v_type
      FROM pg_attribute a
     WHERE a.attrelid = 'public.event_store_snapshots'::regclass
       AND a.attname = 'tenant_id'
       AND NOT a.attisdropped;

    IF v_type IS NULL THEN
        RAISE EXCEPTION '007 REFUSED: public.event_store_snapshots has no tenant_id column at all, so this table did not come from any version of 001_CreateSnapshotSchema.sql. Nothing has been changed. Reconcile the table against 001 before upgrading it.';
    END IF;

    IF v_type = 'character varying(64)' THEN
        RAISE NOTICE '007: public.event_store_snapshots.tenant_id is already character varying(64); nothing to do.';
        RETURN;
    END IF;

    -- A non-default collation is only ever explicit, so this cannot fire on a table 001 built.
    SELECT co.collname
      INTO v_collation
      FROM pg_attribute a
      JOIN pg_collation co ON co.oid = a.attcollation
     WHERE a.attrelid = 'public.event_store_snapshots'::regclass
       AND a.attname = 'tenant_id'
       AND NOT co.collisdeterministic;

    IF v_collation IS NOT NULL THEN
        RAISE EXCEPTION '007 REFUSED: public.event_store_snapshots.tenant_id carries the nondeterministic collation %. It is a component of the primary key, so under that collation two tenant identifiers differing only in case or accent are ONE key: one tenant save overwrites the other snapshot for the same aggregate, and no error is raised. Nothing has been changed. Re-create the column with the database default collation (001_CreateSnapshotSchema.sql names none), reconcile any rows the merged key admitted, then re-run.', v_collation;
    END IF;

    SELECT count(*), max(length(tenant_id))
      INTO v_offenders, v_longest
      FROM public.event_store_snapshots
     WHERE length(tenant_id) > 64;

    IF v_offenders > 0 THEN
        RAISE EXCEPTION '007 REFUSED: % row(s) in public.event_store_snapshots hold a tenant identifier longer than 64 characters (longest: %). tenant_id is a component of the primary key, so narrowing the column would not merely truncate a label: two tenants sharing their first 64 characters would collapse onto ONE key, and one tenant snapshot would overwrite the other. Nothing has been changed. Re-key those rows to identifiers of 64 characters or fewer, then re-run.', v_offenders, v_longest;
    END IF;

    ALTER TABLE public.event_store_snapshots
        ALTER COLUMN tenant_id TYPE VARCHAR(64);

    RAISE NOTICE '007: public.event_store_snapshots.tenant_id narrowed from % to character varying(64).', v_type;
END
$narrow_snapshot_tenant_id$;

COMMIT;
