-- PostgreSQL MIGRATION for Excalibur.AuditLogging.Postgres — TENANT COLUMN, PORTABLE MAXIMUM
-- Version: 1.0
--
-- Narrows "audit"."audit_events".tenant_id from TEXT to VARCHAR(64) — the shape
-- 001_CreateAuditSchema.sql now provisions. Run this ONLY against an audit_events table created by
-- an earlier version of that script. A database provisioned from the current script already has
-- this shape, and this script detects that and does nothing.
--
-- The identifiers below are the provider defaults (PostgresAuditOptions: schema "audit",
-- table "audit_events"). If you override SchemaName or TableName, apply the same overrides here.
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
-- at the narrowest is the only choice that cannot truncate: an identifier accepted by the framework
-- must be storable whole by any provider it can reach. A column wider than the guard is not
-- harmless slack — it is a provider that accepts what a sibling provider silently shortens.
--
--
-- WHAT HAPPENS TO EXISTING ROWS
-- ------------------------------
-- Nothing is backfilled, and no row acquires or loses a tenant. tenant_id was already NOT NULL with
-- the untenanted sentinel as its default in the version this script upgrades from, so there is no
-- absent value to fill: every row keeps its exact tenant bytes.
--
-- A value LONGER than 64 characters is REFUSED, never truncated. Truncation is not a lossy-but-
-- acceptable outcome here — two distinct tenants whose identifiers share their first 64 characters
-- become ONE tenant, and their audit trails merge into a single scope that neither of them owns.
-- Silently merging two tenants' audit evidence is worse than failing to upgrade, so this script
-- stops and names the rows instead of choosing for you. Re-key the reported rows to identifiers of
-- 64 characters or fewer, then re-run.
--
-- Such rows can only predate the framework's length guard. A deployment that has only ever written
-- through a current release has none, and this check costs it one indexed-free scan.
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
-- 'acme' remain two tenants whichever one is in force. Ordering changes; matching does not.
--
-- A NONDETERMINISTIC collation is REFUSED rather than reset, and the refusal is not squeamishness
-- about an unsupported shape. Under one, 'Acme' and 'acme' compare EQUAL, so the store's tenant
-- predicate is already returning another tenant's audit events with no error to show for it —
-- resetting the collation here would silently CHANGE which rows match, on live audit data, in the
-- same statement that was supposed to be a width change. 001 warns against provisioning that way;
-- this declines to decide it for you.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Narrowing a column's length rewrites the table and takes an ACCESS EXCLUSIVE lock for the
-- duration, and rebuilds the two indexes that lead on tenant_id. Run it in a maintenance window
-- sized for the table's row count, not alongside live audit writes.

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

DO $narrow_audit_tenant_id$
DECLARE
    v_type       text;
    v_offenders  bigint;
    v_longest    integer;
    v_collation  text;
BEGIN
    IF to_regclass('"audit"."audit_events"') IS NULL THEN
        RAISE NOTICE '002: "audit"."audit_events" is not present; nothing to upgrade. Provision with 001_CreateAuditSchema.sql instead.';
        RETURN;
    END IF;

    SELECT format_type(a.atttypid, a.atttypmod)
      INTO v_type
      FROM pg_attribute a
     WHERE a.attrelid = '"audit"."audit_events"'::regclass
       AND a.attname = 'tenant_id'
       AND NOT a.attisdropped;

    IF v_type IS NULL THEN
        RAISE EXCEPTION '002 REFUSED: "audit"."audit_events" has no tenant_id column at all, so this table did not come from any version of 001_CreateAuditSchema.sql. Nothing has been changed. Reconcile the table against 001 before upgrading it.';
    END IF;

    IF v_type = 'character varying(64)' THEN
        RAISE NOTICE '002: "audit"."audit_events".tenant_id is already character varying(64); nothing to do.';
        RETURN;
    END IF;

    -- A non-default collation is only ever explicit, so this cannot fire on a table 001 built.
    SELECT co.collname
      INTO v_collation
      FROM pg_attribute a
      JOIN pg_collation co ON co.oid = a.attcollation
     WHERE a.attrelid = '"audit"."audit_events"'::regclass
       AND a.attname = 'tenant_id'
       AND NOT co.collisdeterministic;

    IF v_collation IS NOT NULL THEN
        RAISE EXCEPTION '002 REFUSED: "audit"."audit_events".tenant_id carries the nondeterministic collation ''%''. Under it two tenant identifiers differing only in case or accent compare EQUAL, so a tenant-scoped read returns another tenant''s audit events and nothing reports an error. Nothing has been changed. Re-create the column with the database default collation (001_CreateAuditSchema.sql names none), reconcile any rows the merged comparison allowed in, then re-run.', v_collation;
    END IF;

    SELECT count(*), max(length(tenant_id))
      INTO v_offenders, v_longest
      FROM "audit"."audit_events"
     WHERE length(tenant_id) > 64;

    IF v_offenders > 0 THEN
        RAISE EXCEPTION '002 REFUSED: % row(s) in "audit"."audit_events" hold a tenant identifier longer than 64 characters (longest: %). Narrowing the column would truncate them, and two tenants sharing their first 64 characters would collapse into one scope: their audit trails would merge under an identifier neither of them owns. Nothing has been changed. Re-key those rows to identifiers of 64 characters or fewer, then re-run.', v_offenders, v_longest;
    END IF;

    ALTER TABLE "audit"."audit_events"
        ALTER COLUMN tenant_id TYPE VARCHAR(64);

    -- Restated rather than assumed: ALTER ... TYPE keeps the existing default, and stating it here
    -- makes the upgraded column match 001 by declaration instead of by inheritance.
    ALTER TABLE "audit"."audit_events"
        ALTER COLUMN tenant_id SET DEFAULT '__untenanted__';

    RAISE NOTICE '002: "audit"."audit_events".tenant_id narrowed from % to character varying(64).', v_type;
END
$narrow_audit_tenant_id$;

COMMIT;
