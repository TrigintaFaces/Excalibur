-- PostgreSQL MIGRATION for Excalibur.Saga.Postgres — SAGA TENANT KEY, PORTABLE MAXIMUM
-- Version: 1.0
--
-- Narrows dispatch.sagas.tenant_id from VARCHAR(200) to VARCHAR(64) — the shape 01-SagaSchema.sql
-- now provisions. Run this ONLY against a sagas table created by an earlier version of that script.
-- A database provisioned from the current script already has this shape, and this script detects
-- that and does nothing.
--
-- Schema and table names are the defaults 01 uses (dispatch.sagas). If you overrode either, edit
-- the literals below to match.
--
--
-- WHY THE CREATE SCRIPT CANNOT DELIVER THIS ON ITS OWN
-- -----------------------------------------------------
-- 01 guards its table with CREATE TABLE IF NOT EXISTS. On a database that already ran an earlier
-- version, the guard sees the table and skips the whole definition, so the narrowed column never
-- arrives. The upgrade statements 01 carries for this column set the DEFAULT and the NOT NULL, and
-- re-key the table — none of them touches the width. Re-running 01 is not an upgrade path; this
-- script is.
--
-- This script is guarded on the property that is ACTUALLY changing, the declared length, so it
-- reaches exactly the databases that still need it and is a no-op everywhere else.
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
-- Nothing is backfilled, and no row changes partition. tenant_id was already NOT NULL with the
-- untenanted sentinel as its default in the version this script upgrades from, so there is no
-- absent value to fill: every row keeps its exact tenant bytes, under its existing primary key.
--
-- A value LONGER than 64 characters is REFUSED, never truncated. Truncation here is not a lossy
-- label, it is a KEY MERGE: tenant_id leads PRIMARY KEY (tenant_id, saga_id), and sagas are
-- correlated by a BUSINESS key (OrderId, CorrelationId) rather than a per-tenant identifier, so
-- tenant A's Order-123 saga and tenant B's Order-123 saga are distinguished by the tenant term
-- alone. Two tenants whose identifiers share their first 64 characters stop being two rows: one
-- tenant's save then satisfies the other tenant's key and overwrites that saga's state AND its
-- tenant stamp — the exact cross-tenant overwrite the leading tenant term exists to make
-- inexpressible. So this script stops and names the rows rather than choosing for you. Re-key the
-- reported rows to identifiers of 64 characters or fewer, then re-run.
--
-- Such rows can only predate the length guard the framework now applies at construction. A
-- deployment that has only ever written through a current release has none.
--
--
-- COLLATION
-- ----------
-- No COLLATE clause below, and that omission is what makes an upgraded column match a fresh one.
-- ALTER COLUMN ... TYPE does NOT carry an explicit collation across: with no COLLATE clause the
-- column comes out under the DATABASE DEFAULT collation, which is exactly what 01 gives a fresh
-- install, since it names no collation either. Both paths converge on one collation rather than on
-- two that merely look alike.
--
-- That reset is harmless for a DETERMINISTIC collation, and only because of what deterministic
-- means: PostgreSQL falls back to a byte comparison for equality under any of them, so 'Acme' and
-- 'acme' remain two tenants whichever one is in force. Ordering changes; matching does not, and
-- neither does the set of rows the primary key considers distinct.
--
-- A NONDETERMINISTIC collation is REFUSED rather than reset. Under one, 'Acme' and 'acme' compare
-- EQUAL, and because this column LEADS the primary key that is not merely a read matching too much:
-- the two tenants cannot both hold a saga for the same business key, because the key already treats
-- them as one row. Resetting the collation here would silently re-split rows the key had merged, in
-- the same statement that was supposed to be a width change, so this refuses instead.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Narrowing a column length rewrites the table and takes an ACCESS EXCLUSIVE lock for the duration,
-- and rebuilds the primary key index that tenant_id leads. Run it during a maintenance window with
-- the saga host stopped.

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

DO $narrow_saga_tenant_id$
DECLARE
    v_type       text;
    v_offenders  bigint;
    v_longest    integer;
    v_collation  text;
BEGIN
    IF to_regclass('dispatch.sagas') IS NULL THEN
        RAISE NOTICE '02: dispatch.sagas is not present; nothing to upgrade. Provision with 01-SagaSchema.sql instead.';
        RETURN;
    END IF;

    SELECT format_type(a.atttypid, a.atttypmod)
      INTO v_type
      FROM pg_attribute a
     WHERE a.attrelid = 'dispatch.sagas'::regclass
       AND a.attname = 'tenant_id'
       AND NOT a.attisdropped;

    IF v_type IS NULL THEN
        RAISE EXCEPTION '02 REFUSED: dispatch.sagas has no tenant_id column at all, so this table did not come from any version of 01-SagaSchema.sql. Nothing has been changed. Re-run 01-SagaSchema.sql, which adds the tenant discriminator and re-keys the table, then re-run this script; this one narrows a column, it does not add one.';
    END IF;

    IF v_type = 'character varying(64)' THEN
        RAISE NOTICE '02: dispatch.sagas.tenant_id is already character varying(64); nothing to do.';
        RETURN;
    END IF;

    -- A non-default collation is only ever explicit, so this cannot fire on a table 01 built.
    SELECT co.collname
      INTO v_collation
      FROM pg_attribute a
      JOIN pg_collation co ON co.oid = a.attcollation
     WHERE a.attrelid = 'dispatch.sagas'::regclass
       AND a.attname = 'tenant_id'
       AND NOT co.collisdeterministic;

    IF v_collation IS NOT NULL THEN
        RAISE EXCEPTION '02 REFUSED: dispatch.sagas.tenant_id carries the nondeterministic collation ''%''. It leads the primary key, so under that collation two tenant identifiers differing only in case or accent are ONE key: one tenant''s save overwrites the other''s saga for the same business key, and no error is raised. Nothing has been changed. Re-create the column with the database default collation (01-SagaSchema.sql names none), reconcile any rows the merged key admitted, then re-run.', v_collation;
    END IF;

    SELECT count(*), max(length(tenant_id))
      INTO v_offenders, v_longest
      FROM dispatch.sagas
     WHERE length(tenant_id) > 64;

    IF v_offenders > 0 THEN
        RAISE EXCEPTION '02 REFUSED: % row(s) in dispatch.sagas hold a tenant identifier longer than 64 characters (longest: %). tenant_id leads the primary key, and sagas are correlated by a business key rather than a per-tenant identifier, so narrowing the column would not merely truncate a label: two tenants sharing their first 64 characters would collapse onto ONE key, and one tenant''s save would overwrite the other''s saga state and its tenant stamp. Nothing has been changed. Re-key those rows to identifiers of 64 characters or fewer, then re-run.', v_offenders, v_longest;
    END IF;

    ALTER TABLE dispatch.sagas
        ALTER COLUMN tenant_id TYPE VARCHAR(64);

    -- Restated rather than assumed: ALTER ... TYPE keeps the existing default, and stating it here
    -- makes the upgraded column match 01 by declaration instead of by inheritance.
    ALTER TABLE dispatch.sagas
        ALTER COLUMN tenant_id SET DEFAULT '__untenanted__';

    RAISE NOTICE '02: dispatch.sagas.tenant_id narrowed from % to character varying(64).', v_type;
END
$narrow_saga_tenant_id$;

COMMIT;
