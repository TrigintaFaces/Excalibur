-- PostgreSQL MIGRATION for Excalibur.Compliance.Postgres — SINGLE-TENANT IDENTITY BRIDGE
-- Version: 1.0
--
-- Converges every row this package's compliance and data-inventory stores own from the
-- single-tenant identity '__default__' onto the reserved untenanted sentinel '__untenanted__'.
-- This is the INVERSE of the event store's 006 convergence (which moves '__untenanted__' rows
-- onto '__default__'). Run this ONLY on a deployment that is, and will remain, single-tenant.
--
--
-- WHY THIS EXISTS
-- ----------------
-- PostgresComplianceStore and PostgresDataInventoryStore used to resolve their tenant term from
-- whether an ITenantContext happened to be REGISTERED, not from the deployment-mode flag
-- (TenantContextOptions.RequireTenant). Both stores' own DI registration
-- (AddPostgresComplianceStore / AddPostgresDataInventoryStore) calls AddDefaultTenantContext
-- itself, so a context was ALWAYS registered — which meant these stores ALWAYS resolved the
-- framework's single-tenant default identity '__default__' (TenantDefaults.DefaultTenantId),
-- never the untenanted sentinel, even on a deployment that never called AddMultiTenancy(...).
--
-- The fix makes these stores read RequireTenant like every other tenant-aware store in the
-- framework: RequireTenant == false now binds '__untenanted__', matching the column's own
-- DEFAULT and matching every sibling store (erasure, legal-hold, the event store). That is the
-- correct, permanent behaviour — but a single-tenant deployment that was already running has
-- existing rows filed under '__default__', and a read that now binds '__untenanted__' does not
-- find them. They are silently unreachable, not merely mis-labelled — and for
-- data_inventory_registrations / discovered_data_locations specifically, tenant_id is part of
-- the PRIMARY KEY, so a stranded row also blocks a legitimate untenanted re-registration of the
-- same table/field from ever landing (it is not a duplicate the database would reject; it is a
-- silently different key that never gets read back).
--
-- Converging is only correct for a genuinely single-tenant host. A multi-tenant deployment's
-- '__default__' partition, if anything is ever filed there, is a partition belonging to a
-- specific named tenant literally called "__default__" is not possible (TenantScope rejects a
-- caller-supplied tenant equal to either reserved sentinel before it reaches the database) — but
-- a multi-tenant host is never in the affected state to begin with, since AddMultiTenancy(...)
-- sets RequireTenant true and these stores' fix does not change multi-tenant behaviour at all.
-- The pre-flight check below still refuses on any named tenant as a defensive floor, the same way
-- 006 does, in case some other writer ever filed a named-tenant row here.
-- DO NOT RUN THIS SCRIPT AGAINST A MULTI-TENANT DEPLOYMENT.
--
--
-- WHAT THIS SCRIPT DOES NOT DO
-- ------------------------------
-- It does not change any schema. tenant_id is already NOT NULL on every table below (001 for the
-- compliance store's own tables when provisioned by this package's DDL string, 001 and 003 for
-- the two data-inventory tables). This script only ever rewrites a VALUE.
--
-- It is adopted the same way the other scripts under Scripts/ are: copied into the deployment's
-- own migration set and applied from there.
--
--
-- SCOPE: FIVE INDEPENDENT TABLES, EACH GUARDED SEPARATELY
-- ---------------------------------------------------------
-- dispatch_consent_records, dispatch_erasure_logs, dispatch_subject_access_requests (the
-- PostgresComplianceStore tables), and data_inventory_registrations, discovered_data_locations
-- (the PostgresDataInventoryStore tables) are converged in one script because they are the same
-- operation applied to every table these two stores own — but a deployment need not use both
-- stores. Each block checks for its own table before touching anything, so running this against
-- a database that only has one of the two stores is safe and converges only what is present.
--
-- Table and schema names use the defaults these packages ship ("compliance" schema,
-- "dispatch_" prefix on the compliance-store tables); edit the literals below if you overrode
-- either.
--
--
-- COLLISION HANDLING
-- --------------------
-- dispatch_consent_records and dispatch_subject_access_requests carry tenant_id in their primary
-- key; data_inventory_registrations and discovered_data_locations do too (see 003). Converging
-- '__default__' to '__untenanted__' collides if a given natural key already holds a row under
-- BOTH values — written once while a context was registered (the old, defective resolution) and
-- once genuinely untenanted (a hand-written row, or a row from before either identity concept
-- existed). Each guarded block below checks for that case first and REFUSES, naming the
-- colliding identity, rather than letting the UPDATE fail partway or silently pick a winner.
-- dispatch_erasure_logs carries no uniqueness constraint at all (it is an append-only log), so no
-- collision is possible there and no pre-flight check is needed for that table.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Run during a maintenance window with both stores stopped. Take a backup you have restored at
-- least once. Every step is guarded against the state it is about to create, so the script is
-- safe to re-run; a database with nothing left under '__default__' is a no-op.

-- ---------------------------------------------------------------------------------------
-- PRE-FLIGHT: REFUSE ON A MULTI-TENANT DEPLOYMENT
--
-- See 006's twin comment for the full rationale. The test is the data: a row filed under any
-- tenant other than the untenanted sentinel or the single-tenant default identity is proof this
-- deployment has real, named tenants. Refusing raises, which aborts the surrounding migration
-- transaction, so nothing is rewritten: either every table converges or none does.
--
-- KNOWN LIMIT, stated rather than left to be discovered: this detects a multi-tenant deployment
-- that has written at least one named-tenant row. As explained above, the defect this script
-- fixes could never itself have produced a named-tenant row while RequireTenant was false, so
-- this check is a defensive floor rather than the primary safeguard.
-- ---------------------------------------------------------------------------------------
-- Explicit transaction wrapper (this script only -- not present in 006, which this script is
-- otherwise modelled on). Measured against a real Postgres container: run standalone with
-- `psql -f`, PL/pgSQL's RAISE EXCEPTION only unwinds the DO block it fires in and psql's default
-- (no ON_ERROR_STOP) continues to the next statement -- so a REFUSE on table N does not stop the
-- convergence blocks that follow it in the file from running. Wrapping the whole script in one
-- transaction closes that: a RAISE EXCEPTION anywhere aborts the transaction, so a trailing COMMIT
-- becomes a no-op rollback and nothing this script touched is kept, regardless of how it is
-- invoked.
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

DO $refuse_if_multi_tenant$
DECLARE
    v_table   text;
    v_tenant  text;
BEGIN
    FOREACH v_table IN ARRAY ARRAY[
        '"compliance"."dispatch_consent_records"',
        '"compliance"."dispatch_erasure_logs"',
        '"compliance"."dispatch_subject_access_requests"',
        '"compliance"."data_inventory_registrations"',
        '"compliance"."discovered_data_locations"'
    ]
    LOOP
        IF to_regclass(v_table) IS NULL THEN
            CONTINUE;
        END IF;

        -- %s rather than %I: v_table is schema-qualified, which %I would quote as a single
        -- identifier, and every value comes from the literal array above rather than from input.
        EXECUTE format(
            'SELECT tenant_id FROM %s WHERE tenant_id NOT IN (''__untenanted__'', ''__default__'') LIMIT 1',
            v_table)
        INTO v_tenant;

        IF v_tenant IS NOT NULL THEN
            RAISE EXCEPTION '004 REFUSED: % holds rows under tenant ''%'', which is neither the untenanted sentinel nor the single-tenant identity ''__default__''. This deployment has named tenants; converging its default-identity rows to untenanted would be wrong for this host. Nothing has been changed. Do not run this script against a multi-tenant deployment.', v_table, v_tenant;
        END IF;
    END LOOP;
END
$refuse_if_multi_tenant$;

-- ---------------------------------------------------------------------------------------
-- DISPATCH_CONSENT_RECORDS
--
-- Natural key excluding the tenant: (subject_id, purpose). A collision means one subject/purpose
-- pair holds both a default-identity row and an already-untenanted row.
-- ---------------------------------------------------------------------------------------
DO $converge_consent$
DECLARE
    v_collisions BIGINT;
BEGIN
    IF to_regclass('"compliance"."dispatch_consent_records"') IS NULL THEN
        RAISE NOTICE '004: dispatch_consent_records is not present; nothing to converge.';
        RETURN;
    END IF;

    SELECT count(*) INTO v_collisions
      FROM (
            SELECT subject_id, purpose
              FROM "compliance"."dispatch_consent_records"
             WHERE tenant_id IN ('__default__', '__untenanted__')
             GROUP BY subject_id, purpose
            HAVING count(*) > 1
           ) AS c;

    IF v_collisions > 0 THEN
        RAISE EXCEPTION '004 ABORT: % consent record(s) hold BOTH a default-identity row and a row already under the untenanted sentinel for the same (subject_id, purpose). Delete or re-key whichever is stale, then re-run.', v_collisions;
    END IF;

    UPDATE "compliance"."dispatch_consent_records"
       SET tenant_id = '__untenanted__'
     WHERE tenant_id = '__default__';
END
$converge_consent$;

-- ---------------------------------------------------------------------------------------
-- DISPATCH_ERASURE_LOGS
--
-- Append-only log, no uniqueness constraint on (tenant_id, subject_id) or anything else -- no
-- collision is possible, so no pre-flight check is needed here.
-- ---------------------------------------------------------------------------------------
DO $converge_erasure_logs$
BEGIN
    IF to_regclass('"compliance"."dispatch_erasure_logs"') IS NULL THEN
        RAISE NOTICE '004: dispatch_erasure_logs is not present; nothing to converge.';
        RETURN;
    END IF;

    UPDATE "compliance"."dispatch_erasure_logs"
       SET tenant_id = '__untenanted__'
     WHERE tenant_id = '__default__';
END
$converge_erasure_logs$;

-- ---------------------------------------------------------------------------------------
-- DISPATCH_SUBJECT_ACCESS_REQUESTS
--
-- Natural key excluding the tenant: (request_id). A collision means one request id holds both a
-- default-identity row and an already-untenanted row.
-- ---------------------------------------------------------------------------------------
DO $converge_sar$
DECLARE
    v_collisions BIGINT;
BEGIN
    IF to_regclass('"compliance"."dispatch_subject_access_requests"') IS NULL THEN
        RAISE NOTICE '004: dispatch_subject_access_requests is not present; nothing to converge.';
        RETURN;
    END IF;

    SELECT count(*) INTO v_collisions
      FROM (
            SELECT request_id
              FROM "compliance"."dispatch_subject_access_requests"
             WHERE tenant_id IN ('__default__', '__untenanted__')
             GROUP BY request_id
            HAVING count(*) > 1
           ) AS c;

    IF v_collisions > 0 THEN
        RAISE EXCEPTION '004 ABORT: % subject access request(s) hold BOTH a default-identity row and a row already under the untenanted sentinel for the same request_id. Delete or re-key whichever is stale, then re-run.', v_collisions;
    END IF;

    UPDATE "compliance"."dispatch_subject_access_requests"
       SET tenant_id = '__untenanted__'
     WHERE tenant_id = '__default__';
END
$converge_sar$;

-- ---------------------------------------------------------------------------------------
-- DATA_INVENTORY_REGISTRATIONS
--
-- Natural key excluding the tenant: (table_name, field_name). A collision means one table/field
-- registration holds both a default-identity row and an already-untenanted row -- and until it
-- is resolved, neither an untenanted nor a default-identity re-registration of that field can
-- land cleanly.
-- ---------------------------------------------------------------------------------------
DO $converge_registrations$
DECLARE
    v_collisions BIGINT;
BEGIN
    IF to_regclass('"compliance"."data_inventory_registrations"') IS NULL THEN
        RAISE NOTICE '004: data_inventory_registrations is not present; nothing to converge.';
        RETURN;
    END IF;

    SELECT count(*) INTO v_collisions
      FROM (
            SELECT table_name, field_name
              FROM "compliance"."data_inventory_registrations"
             WHERE tenant_id IN ('__default__', '__untenanted__')
             GROUP BY table_name, field_name
            HAVING count(*) > 1
           ) AS c;

    IF v_collisions > 0 THEN
        RAISE EXCEPTION '004 ABORT: % registration(s) in data_inventory_registrations hold BOTH a default-identity row and a row already under the untenanted sentinel for the same (table_name, field_name). Resolve by hand -- decide which registration is current -- then re-run. Leaving this unresolved means the erasure path may skip the field entirely, silently narrowing erasure coverage.', v_collisions;
    END IF;

    UPDATE "compliance"."data_inventory_registrations"
       SET tenant_id = '__untenanted__'
     WHERE tenant_id = '__default__';
END
$converge_registrations$;

-- ---------------------------------------------------------------------------------------
-- DISCOVERED_DATA_LOCATIONS
--
-- Natural key excluding the tenant: (data_subject_id_hash, table_name, field_name, record_id).
-- A collision means one discovered record holds both a default-identity row and an
-- already-untenanted row.
-- ---------------------------------------------------------------------------------------
DO $converge_locations$
DECLARE
    v_collisions BIGINT;
BEGIN
    IF to_regclass('"compliance"."discovered_data_locations"') IS NULL THEN
        RAISE NOTICE '004: discovered_data_locations is not present; nothing to converge.';
        RETURN;
    END IF;

    SELECT count(*) INTO v_collisions
      FROM (
            SELECT data_subject_id_hash, table_name, field_name, record_id
              FROM "compliance"."discovered_data_locations"
             WHERE tenant_id IN ('__default__', '__untenanted__')
             GROUP BY data_subject_id_hash, table_name, field_name, record_id
            HAVING count(*) > 1
           ) AS c;

    IF v_collisions > 0 THEN
        RAISE EXCEPTION '004 ABORT: % discovered location(s) hold BOTH a default-identity row and a row already under the untenanted sentinel for the same (data_subject_id_hash, table_name, field_name, record_id). Resolve by hand, then re-run. Leaving this unresolved means an erasure request may miss one of the two rows for the same subject, understating what was actually located and erased.', v_collisions;
    END IF;

    UPDATE "compliance"."discovered_data_locations"
       SET tenant_id = '__untenanted__'
     WHERE tenant_id = '__default__';
END
$converge_locations$;

COMMIT;
