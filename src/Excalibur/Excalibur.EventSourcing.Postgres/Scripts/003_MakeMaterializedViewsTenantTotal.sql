-- PostgreSQL MIGRATION for Excalibur.EventSourcing.Postgres — MATERIALIZED-VIEW TENANT PARTITIONING
-- Version: 1.0
--
-- Adds the tenant term to materialized_views and materialized_view_positions and makes it part of each
-- table's primary key, so two tenants projecting the same named view no longer share a row.
--
-- BEFORE: materialized_views is keyed on (view_name, view_id) and materialized_view_positions on
--         (view_name) alone. Both are caller-supplied strings with no tenant discriminator, so two
--         tenants projecting the same named view write and read ONE row. The view upsert has no guard,
--         so the later writer's data silently wins and a read returns whichever tenant wrote last.
-- AFTER:  Both tables carry a NOT NULL tenant_id that leads the primary key. Each tenant holds its own
--         view rows and its own checkpoint.
--
-- The checkpoint half is the worse of the two and is the reason this is not merely a disclosure fix.
-- One checkpoint per view name across all tenants means tenant A's progress advances tenant B's
-- checkpoint, so B's projector skips every event in between — silently, with no error raised. The
-- monotonic guard makes that permanent rather than transient: it exists to stop the checkpoint moving
-- backwards, so the skipped range can never be re-read.
--
--
-- THIS SCRIPT ALSO SHIPS THE FRESH-INSTALL SHAPE, WHICH DID NOT PREVIOUSLY EXIST
-- ------------------------------------------------------------------------------
-- Unlike the SQL Server provider, the Postgres materialized-view store never created its own tables;
-- the schema existed only as a comment on the store class, and each consumer hand-wrote it. Sections 1
-- and 3 therefore CREATE the tables if they are absent, in the tenant-partitioned shape, and sections 2
-- and 4 migrate them if a hand-written copy is already present in the old shape. Both paths converge on
-- the same schema.
--
--
-- WHAT THIS MIGRATION PRODUCES FOR EXISTING ROWS, AND WHY THAT DIRECTION IS THE SAFE ONE
-- --------------------------------------------------------------------------------------
-- The backfill sets every existing row to the reserved '__untenanted__' sentinel. Two consequences,
-- and the distinction between them is the whole point:
--
--   A host that was single-tenant resolves the SAME sentinel at run time (the store binds it through
--   KeyedTenantPartition, which has no empty inhabitant). Its rows are found exactly as before. No
--   replay, no skip, no change in behaviour.
--
--   A host that was already multi-tenant, writing unscoped rows, leaves those rows in a partition that
--   NO REAL TENANT CAN EVER RESOLVE TO — a scoped tenant is rejected outright if it names the sentinel.
--   So a scoped tenant reads its checkpoint, finds nothing, and REPLAYS FROM THE BEGINNING, which
--   re-derives its view from its own events.
--
-- Replay is the failure this migration chooses. The alternative — distributing the legacy rows among
-- real tenants by some guess — would hand a tenant a checkpoint written by another, and that tenant
-- would SKIP the events in between with no error and no way to detect it afterwards. Replay costs
-- time. A skip costs data. The sentinel is chosen precisely because it is unreachable from a scoped
-- read, which makes the skip outcome unconstructable rather than merely unlikely.
--
-- The two tables are backfilled to the SAME sentinel in the same script, so a view and the checkpoint
-- recording how far that view was built stay consistent. Backfilling one and not the other would leave
-- a tenant reading a stale view behind an advanced checkpoint.
--
--
-- WHY A BACKFILL INTO A KEY IS SAFE HERE, WHICH IS NOT SOMETHING TO ASSUME
-- ------------------------------------------------------------------------
-- A value being promoted into a key must not be load-bearing for anything else, or the backfill is
-- itself the data-loss event. Checked, for these two tables specifically:
--
--   * tenant_id is a NEW column. Nothing reads it, so nothing can be invalidated by its value.
--   * data is an opaque serialized read model. It is not encrypted, not signed, and carries no
--     authenticated associated data derived from the row key, so re-keying the row cannot render it
--     unreadable.
--   * view_name and view_id are pure identity. The only other consumer of a view name is a telemetry
--     tag, which is unaffected by a schema change.
--
--
-- WHY THERE IS NO COLLISION PRE-FLIGHT, WHICH IS DELIBERATE
-- ----------------------------------------------------------
-- The sibling event-store migrations open with a collision check because collapsing NULL onto a
-- sentinel can merge two rows that were previously distinct. That cannot happen here and a check for
-- it would be one that can never fire — worse than absent, because it would read as protection.
--
-- (view_name, view_id) is already unique, enforced by the existing primary key. Adding a column whose
-- value is the SAME CONSTANT for every row is an injective transformation: distinct rows stay
-- distinct. There is no pre-existing state in which the new constraint can be violated.
--
--
-- INDEX KEY WIDTH
-- ---------------
-- Postgres bounds a btree entry at roughly a third of a page (~2704 bytes) rather than imposing the
-- 900-byte clustered-key cap SQL Server does. The tenant-qualified key here is three VARCHAR(255)
-- columns, well inside that bound, so the natural key remains the primary key and no surrogate is
-- needed. The SQL Server sibling of this migration reaches a different shape for that reason alone.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run during a maintenance window with projections stopped. The primary-key rebuild rewrites the index
-- on both tables. Take a backup you have restored at least once.
--
-- Every statement is guarded against the state it is about to create, so the script is safe to re-run.

-- ---------------------------------------------------------------------------------------
-- 0) PRE-FLIGHT. Reports what the migration is about to do rather than guarding against a collision,
--    because there is no collision to guard against (see the header). The count that matters is the
--    number of checkpoints moving into the untenanted partition: on a multi-tenant host, that is the
--    number of projections that will replay.
-- ---------------------------------------------------------------------------------------
DO $$
DECLARE
    v_views       BIGINT;
    v_checkpoints BIGINT;
BEGIN
    IF to_regclass('public.materialized_view_positions') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_name = 'materialized_view_positions' AND column_name = 'tenant_id')
    THEN
        EXECUTE 'SELECT count(*) FROM materialized_views' INTO v_views;
        EXECUTE 'SELECT count(*) FROM materialized_view_positions' INTO v_checkpoints;

        RAISE NOTICE '003 pre-flight: % view row(s) and % checkpoint(s) will move to the untenanted partition.',
            v_views, v_checkpoints;
        RAISE NOTICE '003 pre-flight: on a single-tenant host this is a no-op at run time. On a multi-tenant host each scoped tenant will find no checkpoint and replay its projections from the beginning.';
    END IF;
END
$$;

-- ---------------------------------------------------------------------------------------
-- 1) materialized_views — fresh install.
--
--    tenant_id carries NO DEFAULT. It is a component of identity, and you do not default a key
--    column: with one, a write that omitted the tenant would land silently in the untenanted
--    partition, so "I forgot to supply the tenant" and "this row is deliberately untenanted" would
--    become the same row. The store always binds the term explicitly through KeyedTenantPartition,
--    which has no empty inhabitant, so the column never needs a fallback.
-- ---------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS materialized_views (
    tenant_id  VARCHAR(64) NOT NULL,
    view_name  VARCHAR(255) NOT NULL,
    view_id    VARCHAR(255) NOT NULL,
    data       JSONB        NOT NULL,
    created_at TIMESTAMPTZ  NOT NULL,
    updated_at TIMESTAMPTZ  NOT NULL,
    CONSTRAINT pk_materialized_views PRIMARY KEY (tenant_id, view_name, view_id)
);

-- ---------------------------------------------------------------------------------------
-- 2) materialized_views — migrate a pre-existing hand-written table.
--
--    Order is not optional. The column is added nullable, backfilled, and only then made NOT NULL:
--    applying NOT NULL to a populated table before the backfill fails outright. The primary key is
--    dropped and recreated rather than altered, because Postgres will not alter a column a key
--    constraint depends on.
-- ---------------------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name = 'materialized_views' AND column_name = 'tenant_id')
    THEN
        ALTER TABLE materialized_views ADD COLUMN tenant_id VARCHAR(64);

        UPDATE materialized_views SET tenant_id = '__untenanted__' WHERE tenant_id IS NULL;

        ALTER TABLE materialized_views ALTER COLUMN tenant_id SET NOT NULL;

        ALTER TABLE materialized_views DROP CONSTRAINT IF EXISTS materialized_views_pkey;
        ALTER TABLE materialized_views DROP CONSTRAINT IF EXISTS pk_materialized_views;

        ALTER TABLE materialized_views
            ADD CONSTRAINT pk_materialized_views PRIMARY KEY (tenant_id, view_name, view_id);
    END IF;
END
$$;

-- ---------------------------------------------------------------------------------------
-- 3) materialized_view_positions — fresh install. Same reasons as section 1.
-- ---------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS materialized_view_positions (
    tenant_id  VARCHAR(64) NOT NULL,
    view_name  VARCHAR(255) NOT NULL,
    position   BIGINT       NOT NULL,
    created_at TIMESTAMPTZ  NOT NULL,
    updated_at TIMESTAMPTZ  NOT NULL,
    CONSTRAINT pk_materialized_view_positions PRIMARY KEY (tenant_id, view_name)
);

-- ---------------------------------------------------------------------------------------
-- 4) materialized_view_positions — migrate a pre-existing hand-written table.
-- ---------------------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name = 'materialized_view_positions' AND column_name = 'tenant_id')
    THEN
        ALTER TABLE materialized_view_positions ADD COLUMN tenant_id VARCHAR(64);

        UPDATE materialized_view_positions SET tenant_id = '__untenanted__' WHERE tenant_id IS NULL;

        ALTER TABLE materialized_view_positions ALTER COLUMN tenant_id SET NOT NULL;

        ALTER TABLE materialized_view_positions DROP CONSTRAINT IF EXISTS materialized_view_positions_pkey;
        ALTER TABLE materialized_view_positions DROP CONSTRAINT IF EXISTS pk_materialized_view_positions;

        ALTER TABLE materialized_view_positions
            ADD CONSTRAINT pk_materialized_view_positions PRIMARY KEY (tenant_id, view_name);
    END IF;
END
$$;
