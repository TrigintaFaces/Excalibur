-- PostgreSQL MIGRATION for Excalibur.EventSourcing.Postgres — SINGLE-TENANT IDENTITY BRIDGE
-- Version: 1.0
--
-- Converges every row this package owns from the reserved '__untenanted__' partition onto the
-- framework's single-tenant identity, '__default__'. Run this ONLY on a deployment that is, and
-- will remain, single-tenant.
--
--
-- WHY THIS EXISTS
-- ----------------
-- A single-tenant host resolves its ambient ITenantContext.TenantId as '__default__'
-- (TenantDefaults.DefaultTenantId) everywhere the framework reads it. But rows written by any
-- code path that supplied no tenant at all — the default construction path this store used
-- before it required a tenant context, a hand-written INSERT, an omitted column on an older
-- release — land under the reserved '__untenanted__' sentinel instead, because that is what an
-- absent tenant folds to at the storage boundary. One deployment ends up with its own data split
-- across two names for the same tenant, and a read scoped to '__default__' does not find rows
-- filed under '__untenanted__' — they are silently unreachable, not merely mis-labelled.
--
-- Converging the two identities is only correct for a genuinely single-tenant host. A
-- multi-tenant deployment's untenanted partition is a LIVE partition: rows that belong to no
-- real tenant (system records, rows predating a single-to-multi-tenant graduation) coexist with
-- rows that belong to named tenants, and folding "no tenant" into "the tenant named
-- '__default__'" would misattribute ownerless data to a specific, wrong tenant.
-- DO NOT RUN THIS SCRIPT AGAINST A MULTI-TENANT DEPLOYMENT.
--
--
-- WHAT THIS SCRIPT DOES NOT DO
-- ------------------------------
-- It does not change any schema. tenant_id is already NOT NULL and already participates in each
-- table's identity on every deployment this script can run against (004/005 for the event table,
-- 002 for snapshots, 003 for the materialized-view pair already close that gap). This script only
-- ever rewrites a VALUE, never a constraint, an index, or a column definition.
--
-- It is adopted the same way the other scripts under Scripts/ are: copied into the deployment's
-- own migration set and applied from there. That set is not always applied by hand -- a host
-- configured to migrate on startup applies it at boot, this script with it -- so "an operator
-- runs this deliberately" is a description of the common case, not a property anything enforces.
-- That is why the single-tenant precondition below is a machine check and not only a paragraph.
--
--
-- SCOPE: FOUR INDEPENDENT TABLES, EACH GUARDED SEPARATELY
-- ---------------------------------------------------------
-- events, event_store_snapshots, materialized_views, and materialized_view_positions are
-- converged in this one script because they are the same operation applied to every table this
-- package owns — but a deployment need not use all four (a consumer who never registered the
-- materialized-view store has no such tables). Each block below checks for its own table before
-- touching anything, so running this against a database that only has the event store is safe
-- and converges only what is present.
--
-- Table and schema names use the defaults this package ships (public.events,
-- public.event_store_snapshots, materialized_views, materialized_view_positions); edit the
-- literals below if you overrode any of them.
--
--
-- COLLISION HANDLING
-- --------------------
-- The rewrite is safe in the general case: no real tenant can occupy the sentinel (a scoped
-- tenant that names it is rejected before it reaches the database), so collapsing
-- '__untenanted__' onto '__default__' preserves every existing uniqueness class UNLESS a given
-- identity already holds a row under BOTH values — a stream, aggregate, or view that was written
-- to at some point under an explicit '__default__' tenant AND also has legacy untenanted rows.
-- Each block below checks for that case first and REFUSES, naming the colliding identity, rather
-- than letting the UPDATE fail partway or silently pick a winner. Resolve the reported rows
-- (delete or re-key whichever is stale), then re-run.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Run during a maintenance window with the store stopped, for the same reason 005 does: the
-- backfill is a single set-based UPDATE per table and is not resumable. Take a backup you have
-- restored at least once.
--
-- Every step is guarded against the state it is about to create, so the script is safe to
-- re-run; a database with nothing left under '__untenanted__' is a no-op.

-- ---------------------------------------------------------------------------------------
-- PRE-FLIGHT: REFUSE ON A MULTI-TENANT DEPLOYMENT
--
-- The header says do not run this against a multi-tenant deployment. Prose does not stop
-- anything, and it is read once, by whoever assembles the migration set -- not by whatever
-- applies it later. This script is adopted the way the others here are: copied into a
-- deployment's own migration set and applied with it. Where that set is applied automatically
-- at startup, this script goes with it, and nothing on that path pauses to ask whether the
-- deployment is single-tenant. So the instruction is made a machine check.
--
-- The test is the data, because the data is what this script is about to rewrite. A row filed
-- under any tenant other than the untenanted sentinel or the single-tenant default identity is
-- proof that this deployment has real, named tenants -- and therefore that its untenanted
-- partition is a live partition holding ownerless rows, not a legacy spelling of '__default__'.
-- Converging it would misattribute that data to one named tenant.
--
-- Refusing raises, which aborts the ENCLOSING transaction -- see the explicit BEGIN/COMMIT
-- immediately below, added because this script does not run inside one on its own. That matters
-- because the four tables below are converged in sequence and a failure discovered at the fourth
-- would otherwise leave the first three already moved, with no resumable path back.
--
-- CORRECTED: an earlier revision of this comment claimed the refuse "aborts the surrounding
-- migration transaction" as though one always exists. It does not: run standalone with plain
-- `psql -f` (no ON_ERROR_STOP, no framework-supplied transaction -- the realistic way an operator
-- without a migration tool applies a copied script), a RAISE EXCEPTION in one table's guard does NOT
-- stop psql from proceeding to the next table's block -- exactly the partially-converged,
-- multi-table state this guard exists to prevent, reached BY the guard. Measured live against a
-- real Postgres container. Fixed below by making this script supply its own transaction rather
-- than assuming the caller does.
--
-- KNOWN LIMIT, stated rather than left to be discovered: this detects a multi-tenant deployment
-- that has written at least one named-tenant row. A multi-tenant host that has so far written
-- only untenanted rows is indistinguishable, in its data, from a single-tenant one -- and for
-- that host the convergence is also harmless, because there is no named tenant for the rows to
-- be misattributed away from. The case the check does not cover is the case that does no harm.
-- ---------------------------------------------------------------------------------------
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
        'public.events',
        'public.event_store_snapshots',
        'public.materialized_views',
        'public.materialized_view_positions'
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
            RAISE EXCEPTION '006 REFUSED: % holds rows under tenant ''%'', which is neither the untenanted sentinel nor the single-tenant identity ''__default__''. This deployment has named tenants, so its untenanted rows belong to no tenant rather than to ''__default__'', and converging them would file ownerless data under one specific, wrong tenant. Nothing has been changed. Do not run this script against a multi-tenant deployment; if this host is genuinely single-tenant, re-key or remove the rows under ''%'' first.', v_table, v_tenant, v_tenant;
        END IF;
    END LOOP;
END
$refuse_if_multi_tenant$;

-- ---------------------------------------------------------------------------------------
-- EVENTS
--
-- Natural key excluding the tenant: (aggregate_id, aggregate_type, version). A collision means
-- one stream position is claimed by both an untenanted and a default-tenant row.
-- ---------------------------------------------------------------------------------------
DO $converge_events$
DECLARE
    v_collisions BIGINT;
BEGIN
    IF to_regclass('public.events') IS NULL THEN
        RAISE NOTICE '006: public.events is not present; nothing to converge for the event store.';
        RETURN;
    END IF;

    SELECT count(*) INTO v_collisions
      FROM (
            SELECT aggregate_id, aggregate_type, version
              FROM public.events
             WHERE tenant_id IN ('__untenanted__', '__default__')
             GROUP BY aggregate_id, aggregate_type, version
            HAVING count(*) > 1
           ) AS c;

    IF v_collisions > 0 THEN
        RAISE EXCEPTION '006 ABORT: % stream position(s) in public.events hold BOTH an untenanted row and a row already under the single-tenant identity ''__default__''. This deployment is configured as single-tenant, so the untenanted rows would be moved onto ''__default__'' -- but that stream position already has one there, and both would occupy the same key. Delete or re-key whichever event is stale, then re-run. If this host is actually multi-tenant, do not run this script at all.', v_collisions;
    END IF;

    UPDATE public.events
       SET tenant_id = '__default__'
     WHERE tenant_id = '__untenanted__';
END
$converge_events$;

-- ---------------------------------------------------------------------------------------
-- EVENT_STORE_SNAPSHOTS
--
-- Natural key excluding the tenant: (aggregate_id, aggregate_type). A collision means one
-- aggregate's snapshot is claimed by both an untenanted and a default-tenant row.
-- ---------------------------------------------------------------------------------------
DO $converge_snapshots$
DECLARE
    v_collisions BIGINT;
BEGIN
    IF to_regclass('public.event_store_snapshots') IS NULL THEN
        RAISE NOTICE '006: public.event_store_snapshots is not present; nothing to converge for the snapshot store.';
        RETURN;
    END IF;

    SELECT count(*) INTO v_collisions
      FROM (
            SELECT aggregate_id, aggregate_type
              FROM public.event_store_snapshots
             WHERE tenant_id IN ('__untenanted__', '__default__')
             GROUP BY aggregate_id, aggregate_type
            HAVING count(*) > 1
           ) AS c;

    IF v_collisions > 0 THEN
        RAISE EXCEPTION '006 ABORT: % aggregate(s) in public.event_store_snapshots hold BOTH an untenanted snapshot and one already under the single-tenant identity ''__default__''. Delete or re-key whichever snapshot is stale, then re-run. Snapshots are a rebuildable cache: deleting either row and letting it regenerate from the event stream is a legitimate alternative to resolving the collision by hand.', v_collisions;
    END IF;

    UPDATE public.event_store_snapshots
       SET tenant_id = '__default__'
     WHERE tenant_id = '__untenanted__';
END
$converge_snapshots$;

-- ---------------------------------------------------------------------------------------
-- MATERIALIZED_VIEWS
--
-- Natural key excluding the tenant: (view_name, view_id). A collision means one named view is
-- claimed by both an untenanted and a default-tenant row.
-- ---------------------------------------------------------------------------------------
DO $converge_views$
DECLARE
    v_collisions BIGINT;
BEGIN
    IF to_regclass('public.materialized_views') IS NULL THEN
        RAISE NOTICE '006: materialized_views is not present; nothing to converge for the materialized-view store.';
        RETURN;
    END IF;

    SELECT count(*) INTO v_collisions
      FROM (
            SELECT view_name, view_id
              FROM public.materialized_views
             WHERE tenant_id IN ('__untenanted__', '__default__')
             GROUP BY view_name, view_id
            HAVING count(*) > 1
           ) AS c;

    IF v_collisions > 0 THEN
        RAISE EXCEPTION '006 ABORT: % view(s) in materialized_views hold BOTH an untenanted row and a row already under the single-tenant identity ''__default__''. Delete or re-key whichever is stale, then re-run. Views are rebuildable from the event stream: deleting either row and letting the projection replay it is a legitimate alternative.', v_collisions;
    END IF;

    UPDATE public.materialized_views
       SET tenant_id = '__default__'
     WHERE tenant_id = '__untenanted__';
END
$converge_views$;

-- ---------------------------------------------------------------------------------------
-- MATERIALIZED_VIEW_POSITIONS
--
-- Natural key excluding the tenant: (view_name). A collision means one view's checkpoint is
-- claimed by both an untenanted and a default-tenant row.
--
-- Converging a checkpoint is not cosmetic: it decides which position a single-tenant host's
-- projector resumes from. Leaving a checkpoint under '__untenanted__' while reads resolve
-- '__default__' means the checkpoint is never found, and the projection replays from the
-- beginning on every restart -- silent, and it never converges on its own, because nothing ever
-- finds and advances the '__default__' checkpoint that reads are actually looking for.
-- ---------------------------------------------------------------------------------------
DO $converge_positions$
DECLARE
    v_collisions BIGINT;
BEGIN
    IF to_regclass('public.materialized_view_positions') IS NULL THEN
        RAISE NOTICE '006: materialized_view_positions is not present; nothing to converge for the materialized-view store.';
        RETURN;
    END IF;

    SELECT count(*) INTO v_collisions
      FROM (
            SELECT view_name
              FROM public.materialized_view_positions
             WHERE tenant_id IN ('__untenanted__', '__default__')
             GROUP BY view_name
            HAVING count(*) > 1
           ) AS c;

    IF v_collisions > 0 THEN
        RAISE EXCEPTION '006 ABORT: % checkpoint(s) in materialized_view_positions hold BOTH an untenanted position and one already under the single-tenant identity ''__default__''. Picking a survivor decides which position the projector resumes from -- an operator must choose, not this script. Delete whichever checkpoint is stale, then re-run.', v_collisions;
    END IF;

    UPDATE public.materialized_view_positions
       SET tenant_id = '__default__'
     WHERE tenant_id = '__untenanted__';
END
$converge_positions$;

COMMIT;
