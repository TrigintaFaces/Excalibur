-- PostgreSQL MIGRATION for Excalibur.EventSourcing.Postgres — EVENT STREAM IDENTITY, TENANT-SCOPED
-- Version: 1.0
--
-- Adds tenant_id to the event table's uniqueness key, converging this provider onto the shape SQL
-- Server and Oracle already ship. After this script a stream is identified by
-- (aggregate_id, aggregate_type, version, tenant_id) rather than by the first three alone.
--
-- BEFORE: 004 declares tenant_id NOT NULL and then leaves it out of uq_events_aggregate_version. The
--         write side is total, the identity side is not, and the two halves disagree about what
--         identifies a row.
-- AFTER:  The tenant participates in stream identity, so optimistic concurrency is per-tenant rather
--         than global — the same rule the read path has always applied.
--
--
-- WHAT THE DIVERGENCE ACTUALLY DOES, BECAUSE IT DOES NOT PRESENT AS A TENANCY BUG
-- -------------------------------------------------------------------------------
-- The version probe is tenant-scoped (GetCurrentVersionRequest.cs:60-64, the
-- "AND COALESCE(tenant_id, @UntenantedSentinel) = @TenantId" term); the uniqueness key is not. So when
-- two tenants use the same natural aggregate id — routine when the id is an order number or a customer
-- reference, not exotic:
--
--   tenant B probes for the current version  -> -1, "this aggregate does not exist"
--   tenant B appends version 0               -> 23505, the row already exists (tenant A wrote it)
--
-- The read says the stream is absent and the write says it is present. It surfaces to the caller as an
-- optimistic-concurrency conflict, which is a RETRYABLE error, and the retry re-probes and gets -1
-- again. It cannot converge. A conflict that resolves on retry and one that never does are reported
-- identically, which is why this reads as a flapping concurrency problem rather than as the schema
-- disagreement it is.
--
--
-- WHY THIS NEEDS A COLLISION PRE-FLIGHT WHEN THE SIBLING MIGRATION 003 DELIBERATELY HAS NONE
-- ------------------------------------------------------------------------------------------
-- Widening a unique key is, by itself, incapable of causing a collision: every dataset satisfying
-- UNIQUE (a, b, version) also satisfies UNIQUE (a, b, version, tenant_id), because adding a column can
-- only split a uniqueness class, never merge two. On a database provisioned by 004 this script cannot
-- fail, and a guard written for that case alone would be one that can never fire — worse than absent,
-- because it would read as protection.
--
-- The guard below is not written for that case. It is written for the two states in which this table
-- does NOT carry 004's constraint:
--
--   * A HAND-WRITTEN events table. The store provisions with CREATE TABLE IF NOT EXISTS, and the
--     published documentation carried a tenant-less CREATE TABLE for several releases, so a table
--     built from those instructions may have no stream uniqueness constraint at all. Such a table can
--     already hold two rows at the same (aggregate_id, aggregate_type, version, tenant_id), and step 2
--     would then fail partway with a bare 23505 naming the constraint but not the data.
--
--   * A row whose tenant_id IS NULL alongside a literal sentinel row at the same version. The backfill
--     in step 1 collapses those onto one value, which is a genuine merge.
--
-- In both cases the failure is CORRECT — the data already holds two rows each claiming to be version N
-- of one stream, and choosing which append survives is a data question this script has no authority to
-- decide. So it refuses, and NAMES THE ROWS rather than picking a winner.
--
--
-- THE NULL HAZARD IS WHY STEP 1 IS NOT OPTIONAL, AND IT RUNS OPPOSITE TO INTUITION
-- --------------------------------------------------------------------------------
-- Postgres treats NULLs in a unique constraint as DISTINCT FROM EACH OTHER. SQL Server does the
-- opposite, which is why its sibling migration 004 can treat a NULL tenant as a merely cosmetic
-- divergence and this one cannot.
--
-- So if any row reaches step 2 still holding a NULL tenant_id, the new constraint does not merely fail
-- to help that row — it STOPS CONSTRAINING IT ENTIRELY. Two concurrent appends of version N for the
-- same untenanted aggregate would both succeed, and optimistic concurrency, the one thing this
-- constraint exists to provide, would be silently gone for exactly the rows a pre-tenancy database is
-- made of. This migration would then have introduced a worse defect than the one it closes.
--
-- That is why the column is closed to NULL BEFORE it is put in the key, and why the order of steps 1
-- and 2 is load-bearing rather than tidy.
--
--
-- INDEX KEY WIDTH — STATED, INCLUDING THE PART THAT DOES NOT FIT
-- --------------------------------------------------------------
-- Postgres bounds a btree entry at roughly a third of a page, ~2704 bytes on the default 8KB page. It
-- does not impose SQL Server's 900/1700-byte key limits, so that provider's arithmetic does not carry
-- over and must not be reused here.
--
-- The new key is three VARCHAR(255) columns plus a BIGINT. VARCHAR(255) bounds CHARACTERS, not bytes,
-- so under UTF-8 the byte width depends on the data:
--
--   ASCII identifiers (GUIDs, order numbers, type names)    3 x  255 + 8 =  773 bytes    well inside
--   worst case, every character a 4-byte codepoint          3 x 1020 + 8 = 3068 bytes    EXCEEDS 2704
--
-- The worst case is reachable rather than theoretical: an aggregate id, an aggregate type AND a tenant
-- id all at or near 255 characters of 4-byte codepoints would be rejected on INSERT with "index row
-- size exceeds btree version 4 maximum 2704". Adding the third column is what brings that within
-- reach — the previous two-column key could not exceed 2048 bytes.
--
-- It is accepted rather than mitigated, and the reasoning is recorded so it is not rediscovered as a
-- surprise: narrowing the columns would be a breaking change to stored data, and a surrogate hash key
-- would trade a reachable-but-implausible failure for a permanent loss of readability in the index
-- every stream read depends on. A deployment using long non-ASCII identifiers for all three should
-- measure its actual widths before running this.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run during a maintenance window with the store stopped. Step 2 rebuilds a unique constraint on the
-- system of record. Take a backup you have restored at least once.
--
-- The backfill in step 1 writes every row whose tenant_id IS NULL. On a large event store that is the
-- dominant cost; it is a single set-based UPDATE and is not resumable, so size the window against the
-- count reported by pre-flight B.
--
-- SCOPE: this script addresses public.events — the default names 004 creates. The store accepts schema
-- and table overrides (PostgresEventStore takes both), so a deployment that used them holds its data
-- elsewhere; pre-flight A stops rather than completing silently. Edit the literals to match, then
-- re-run.
--
-- Every statement is guarded against the state it is about to create, so the script is safe to re-run.
--
-- TESTING THIS SCRIPT: 004_CreateEventStoreSchema.sql now creates the TARGET shape directly
-- (tenant_id already NOT NULL, already in the stream UNIQUE constraint) -- this script is a no-op
-- against a fresh install and exists only for a database created by an OLDER 004. To exercise it
-- you must deliberately revert a fresh install's schema first (drop the tenant column from the key,
-- make it nullable), not write a synthetic table from memory -- the SQL Server twin's header names
-- the same trap and a real example of what it hides.

-- ---------------------------------------------------------------------------------------
-- PRE-FLIGHT A: THE TABLE MUST EXIST UNDER THE NAME THIS SCRIPT ADDRESSES.
--
-- Deliberately first, before any guarded block, so a wrong-named deployment cannot get partway.
-- FATAL rather than a warning: a halted deploy is recoverable in minutes, whereas a green run that did
-- nothing surfaces later as an unconverged key, somewhere else, with no signal in between. The check
-- can only fire for a deployment this script was ALREADY doing nothing for, so nobody who currently
-- succeeds starts failing.
-- ---------------------------------------------------------------------------------------

-- Explicit transaction wrapper -- see 006_ConvergeUntenantedToDefaultTenant.sql's header for why:
-- without it, a REFUSE partway through a guarded multi-step script does not roll back steps that
-- already ran (measured live; not specific to that script's dialect or shape).
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

DO $preflight_a$
BEGIN
    IF to_regclass('public.events') IS NULL THEN
        RAISE EXCEPTION '005 ABORT: public.events is not present under that name. Two different deployments reach this line and they need different fixes, so establish which one you are before editing anything -- \dt will show what this database actually holds. (1) The event store schema was never created here: 004_CreateEventStoreSchema.sql has not been run against this database. Run it, then re-run this script -- editing the literals in this script would not help you, because there is nothing to rename to. (2) The table exists under another name: PostgresEventStore accepts schema and table overrides, so a deployment that used them holds its data elsewhere. Edit the table literals in this script to match your deployment, then re-run. Refusing rather than completing silently having done nothing.';
    END IF;
END
$preflight_a$;

-- ---------------------------------------------------------------------------------------
-- PRE-FLIGHT B: COLLISION CHECK. RUN THIS AND READ THE RESULT. DO NOT SKIP.
--
-- Evaluates the key this script is about to create against the values the backfill is about to
-- produce — COALESCE(tenant_id, sentinel), not the column as it stands — so it sees the post-backfill
-- state without mutating anything.
--
-- See the header for when this can fire. It refuses and names the offending streams; it does not
-- choose which duplicate append survives, because that is a data decision and this is a schema script.
-- ---------------------------------------------------------------------------------------
DO $preflight_b$
DECLARE
    v_null_rows  BIGINT;
    v_collisions BIGINT;
    v_sample     TEXT;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema = 'public' AND table_name = 'events'
                     AND column_name = 'tenant_id')
    THEN
        RAISE NOTICE '005 pre-flight: tenant_id is absent; it will be added, backfilled to the sentinel, and closed to NULL.';
        RETURN;
    END IF;

    SELECT count(*) INTO v_null_rows FROM public.events WHERE tenant_id IS NULL;

    SELECT count(*), string_agg(d.sample, '; ')
      INTO v_collisions, v_sample
      FROM (
            SELECT format('(aggregate_id=%L, aggregate_type=%L, version=%s, tenant_id=%L, %s rows)',
                          aggregate_id, aggregate_type, version,
                          COALESCE(tenant_id, '__untenanted__'), count(*)) AS sample
              FROM public.events
             GROUP BY aggregate_id, aggregate_type, version, COALESCE(tenant_id, '__untenanted__')
            HAVING count(*) > 1
             LIMIT 20
           ) AS d;

    RAISE NOTICE '005 pre-flight: % row(s) will be backfilled to the sentinel.', v_null_rows;

    IF COALESCE(v_collisions, 0) > 0 THEN
        RAISE EXCEPTION '005 ABORT: % stream version(s) would hold more than one row under the tenant-scoped key. This table already contains duplicate appends — either it was created without a stream uniqueness constraint (a hand-written table, or one built from documentation that shipped a tenant-less CREATE TABLE), or it holds both a NULL and a literal sentinel row at the same version. Deciding which append survives is a data question this script has no authority to settle. Offending streams (first 20): %. Resolve them, then re-run.', v_collisions, v_sample;
    END IF;
END
$preflight_b$;

-- ---------------------------------------------------------------------------------------
-- 1) The column: present, populated, and closed to NULL — in that order.
--
--    The order is not optional. ALTER COLUMN ... SET NOT NULL fails outright against a populated table
--    still holding NULLs, and a NULL surviving into step 2 would silently disable the constraint for
--    that row rather than fail loudly (see the header).
--
--    tenant_id carries NO DEFAULT, matching 004 and the materialized-view migration 003: a key column
--    is not defaulted, because with one a write that omitted the tenant would land silently in the
--    untenanted partition, making "I forgot to supply the tenant" and "this row is deliberately
--    untenanted" the same row. The store always binds the term explicitly through KeyedTenantPartition,
--    which has no empty inhabitant, so the column never needs a fallback.
-- ---------------------------------------------------------------------------------------
DO $close_column$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema = 'public' AND table_name = 'events'
                     AND column_name = 'tenant_id')
    THEN
        ALTER TABLE public.events ADD COLUMN tenant_id VARCHAR(64);
    END IF;

    UPDATE public.events SET tenant_id = '__untenanted__' WHERE tenant_id IS NULL;

    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'events'
                 AND column_name = 'tenant_id' AND is_nullable = 'YES')
    THEN
        ALTER TABLE public.events ALTER COLUMN tenant_id SET NOT NULL;
    END IF;
END
$close_column$;

-- ---------------------------------------------------------------------------------------
-- 2) Rebuild stream identity around the tenant term.
--
--    The outgoing constraint is located by its COLUMN SET rather than by name. 004 names it
--    uq_events_aggregate_version; the published documentation used that same name on a differently
--    shaped table; and an inline UNIQUE (...) in a hand-written CREATE TABLE gets the server-generated
--    events_aggregate_id_aggregate_type_version_key. Matching on names would leave the tenant-less
--    constraint in place on exactly the deployments most likely to have one, and the script would
--    report success having converged nothing.
--
--    Dropped and recreated rather than altered: Postgres has no ALTER CONSTRAINT that changes a key's
--    column list.
--
--    Guarded on the TARGET state, not the source, so a database already carrying the tenant-scoped key
--    (a fresh install once 004 is updated, or a second run of this script) does no work.
-- ---------------------------------------------------------------------------------------
DO $rebuild_identity$
DECLARE
    v_old_constraint TEXT;
BEGIN
    -- Already converged? Any unique constraint over exactly the four key columns, whatever its name.
    IF EXISTS (
        SELECT 1
          FROM pg_constraint c
         WHERE c.conrelid = 'public.events'::regclass
           AND c.contype = 'u'
           AND (SELECT array_agg(a.attname::text ORDER BY a.attname)
                  FROM unnest(c.conkey) AS k(attnum)
                  JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum)
               = ARRAY['aggregate_id', 'aggregate_type', 'tenant_id', 'version']
    ) THEN
        RAISE NOTICE '005: stream identity already carries the tenant term — nothing to do.';
        RETURN;
    END IF;

    -- The tenant-less constraint over exactly the three original columns, whatever its name.
    SELECT c.conname INTO v_old_constraint
      FROM pg_constraint c
     WHERE c.conrelid = 'public.events'::regclass
       AND c.contype = 'u'
       AND (SELECT array_agg(a.attname::text ORDER BY a.attname)
              FROM unnest(c.conkey) AS k(attnum)
              JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum)
           = ARRAY['aggregate_id', 'aggregate_type', 'version']
     LIMIT 1;

    IF v_old_constraint IS NOT NULL THEN
        EXECUTE format('ALTER TABLE public.events DROP CONSTRAINT %I', v_old_constraint);
        RAISE NOTICE '005: dropped tenant-less stream constraint %.', v_old_constraint;
    ELSE
        -- A hand-written table with no stream constraint at all. Pre-flight B has already proved the
        -- data satisfies the new key, so adding it is safe and closes a table that until now had no
        -- optimistic concurrency whatsoever.
        RAISE NOTICE '005: no tenant-less stream constraint found; adding the tenant-scoped key to a table that had none.';
    END IF;

    ALTER TABLE public.events
        ADD CONSTRAINT uq_events_aggregate_version_tenant
            UNIQUE (aggregate_id, aggregate_type, version, tenant_id);
END
$rebuild_identity$;

COMMIT;
