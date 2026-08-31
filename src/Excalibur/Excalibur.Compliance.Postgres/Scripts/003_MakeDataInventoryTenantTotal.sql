-- PostgreSQL MIGRATION for Excalibur.Compliance.Postgres — DATA INVENTORY TENANT TOTALITY
-- Version: 1.0
--
-- Adds the tenant_id discriminator to "compliance"."data_inventory_registrations" and
-- "compliance"."discovered_data_locations", and puts it INTO THE PRIMARY KEY of both. After this
-- script a registration belongs to a tenant, reads can be confined to one, and two tenants
-- registering the same table and field are two rows rather than one.
--
-- BEFORE: neither table has any tenant column. The only tenant-shaped field is tenant_id_column,
--         which holds the NAME of a column in the consumer's own table — metadata, not an identity.
--         The read path filtered on "tenant_id_column IS NOT NULL", which asks whether a column name
--         was recorded, so every scoped read returned every tenant's rows. The primary keys were
--         (table_name, field_name) and (data_subject_id_hash, table_name, field_name, record_id),
--         neither carrying a tenant term, so one tenant's registration OVERWROTE another's.
-- AFTER:  tenant_id is NOT NULL, defaults to the reserved '__untenanted__' sentinel, and is part of
--         both primary keys. Reads bind the caller's tenant term and the sentinel; writes key on it.
--
--
-- WHO NEEDS THIS
-- --------------
-- Any database whose two inventory tables were created BEFORE the release that ships this file —
-- whether provisioned from an earlier 001 or, far more commonly, by the store's own AutoCreateSchema
-- path. Both creation paths guard on table existence (CREATE TABLE IF NOT EXISTS), so neither adds a
-- column to a table that is already there. Upgrading the package alone does NOT reshape the table,
-- and nothing about the running system says so until a query fails.
--
-- A database created by THIS release, from either path, already carries the total shape. This script
-- detects that and does nothing.
--
-- CUSTOM OBJECT NAMES: this targets the DEFAULT names. A deployment that configured a different
-- schema or table name needs the same steps against its own names — the auto-create path will NOT
-- retrofit them, for the reason above. Copy this file and substitute the names.
--
--
-- RUN THIS TOGETHER WITH THE PACKAGE THAT INTRODUCED IT
-- ----------------------------------------------------
-- This migration is not tolerant in either direction, and the asymmetry is worth stating plainly:
--
--   Old package, new schema.  The old package's INSERT names no tenant_id. The column's default
--                             supplies the sentinel, so writes succeed and every row lands untenanted
--                             — silently collapsing every tenant into one partition.
--   New package, old schema.  Every statement names tenant_id against a table that has no such
--                             column, so the store fails LOUDLY on first use rather than answering
--                             wrongly.
--
-- The second is the safe order and the one to plan for: upgrade the package, take the outage, run
-- this. It fails closed. The first does not.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run during a maintenance window with the data-inventory store stopped. Take a backup you have
-- restored at least once.
--
-- Two differences from the SQL Server twin, recorded so they do not read as omissions:
--
--   No collation clause. PostgreSQL's default collations are deterministic, so '=' on VARCHAR is
--                        already case-sensitive and agrees with the framework's ordinal tenant
--                        comparison. SQL Server's database default is typically case-INSENSITIVE, so
--                        its twin must state a binary collation to reach the behaviour this dialect
--                        starts with.
--   Cheaper key rebuild. Dropping and recreating a PRIMARY KEY here builds a new unique index; it
--                        does not rewrite the heap the way rebuilding a SQL Server CLUSTERED key
--                        does. Still take the window — the index build takes an exclusive lock.
--
-- It is guarded and re-runnable: every step tests for the state it is about to create, so running it
-- twice, or against a database that is already converged, changes nothing.
--
--
-- WHAT THIS CANNOT REPAIR
-- -----------------------
-- Rows already destroyed by the overwrite this fixes are GONE. Where two tenants had registered the
-- same table and field, the database retained one row; the other was overwritten in place and left no
-- trace to recover. This script stops further overwrites — it cannot reconstruct what earlier ones
-- took. After migrating, have each tenant re-run its registration so any silently-lost entry is
-- restored. A registration is how the erasure path knows a field holds personal data, so a missing one
-- means that field is skipped and the erasure still reports success.

-- Explicit transaction wrapper -- see Excalibur.EventSourcing.Postgres's
-- 006_ConvergeUntenantedToDefaultTenant.sql header for why: without it, the collision REFUSE in
-- step 3 below does not roll back the column adds / backfills steps 1-2 already made (measured
-- live against a real Postgres container). Either the whole script converges or none of it does.
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

-- ---------------------------------------------------------------------------------------
-- 1) Add the column to both tables.
--
--    Added WITH the default and NOT NULL in one statement: every pre-existing registration becomes
--    untenanted, which is the only truthful reading of a row written when the store had no concept of
--    a tenant. ADD COLUMN IF NOT EXISTS makes the step re-runnable on its own terms.
-- ---------------------------------------------------------------------------------------
DO $$
BEGIN
    IF to_regclass('"compliance"."data_inventory_registrations"') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'compliance'
             AND table_name = 'data_inventory_registrations'
             AND column_name = 'tenant_id'
       ) THEN
        RAISE NOTICE '003: data_inventory_registrations — adding tenant_id; existing rows become untenanted.';

        ALTER TABLE "compliance"."data_inventory_registrations"
            ADD COLUMN tenant_id VARCHAR(64) NOT NULL DEFAULT '__untenanted__';
    END IF;

    IF to_regclass('"compliance"."discovered_data_locations"') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'compliance'
             AND table_name = 'discovered_data_locations'
             AND column_name = 'tenant_id'
       ) THEN
        RAISE NOTICE '003: discovered_data_locations — adding tenant_id; existing rows become untenanted.';

        ALTER TABLE "compliance"."discovered_data_locations"
            ADD COLUMN tenant_id VARCHAR(64) NOT NULL DEFAULT '__untenanted__';
    END IF;
END
$$;

-- ---------------------------------------------------------------------------------------
-- 2) Converge a column that already exists but is NULLABLE onto the total shape.
--
--    Step 1 does not fire for such a database, so without this block it would reach the key rebuild
--    below still holding NULLs and fail there with a message about the key rather than about the
--    column. This is the state a half-applied run, or a consumer who added the column by hand, leaves
--    behind.
--
--    Backfill BEFORE the constraint: SET NOT NULL fails outright while any row is NULL.
-- ---------------------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'compliance'
          AND table_name = 'data_inventory_registrations'
          AND column_name = 'tenant_id'
          AND is_nullable = 'YES'
    ) THEN
        UPDATE "compliance"."data_inventory_registrations"
            SET tenant_id = '__untenanted__'
            WHERE tenant_id IS NULL;

        ALTER TABLE "compliance"."data_inventory_registrations"
            ALTER COLUMN tenant_id SET NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'compliance'
          AND table_name = 'discovered_data_locations'
          AND column_name = 'tenant_id'
          AND is_nullable = 'YES'
    ) THEN
        UPDATE "compliance"."discovered_data_locations"
            SET tenant_id = '__untenanted__'
            WHERE tenant_id IS NULL;

        ALTER TABLE "compliance"."discovered_data_locations"
            ALTER COLUMN tenant_id SET NOT NULL;
    END IF;
END
$$;

-- ---------------------------------------------------------------------------------------
-- 3) COLLISION PRE-FLIGHT, before either key is touched.
--
--    Widening a key cannot itself collide: the old key was already unique, and adding a column to it
--    only ever splits rows apart. The collision this guards is the BACKFILL in step 2 — a database
--    where the column already existed and held a mix of NULLs and real terms can, once the NULLs
--    become the sentinel, hold two rows that are identical under the new key.
--
--    It REFUSES and names the rows rather than choosing between them. Each duplicate is a
--    registration some tenant is relying on; picking a survivor silently would drop a field from that
--    tenant's erasure coverage, which is the same class of loss this migration exists to stop.
--    Resolve them by hand — decide which term each row belongs to — then re-run. This script is
--    re-runnable, so a resolved database proceeds cleanly on the next attempt.
-- ---------------------------------------------------------------------------------------
DO $$
DECLARE
    dupes TEXT;
BEGIN
    IF to_regclass('"compliance"."data_inventory_registrations"') IS NOT NULL THEN
        SELECT string_agg(format('(%s, %s, %s) x%s', table_name, field_name, tenant_id, n), '; ')
        INTO dupes
        FROM (
            SELECT table_name, field_name, tenant_id, COUNT(*) AS n
            FROM "compliance"."data_inventory_registrations"
            GROUP BY table_name, field_name, tenant_id
            HAVING COUNT(*) > 1
        ) d;

        IF dupes IS NOT NULL THEN
            RAISE EXCEPTION '003 ABORTED: "compliance"."data_inventory_registrations" holds rows that are duplicates under the new key (table_name, field_name, tenant_id). Nothing has been keyed and no row has been chosen over another. Resolve these by assigning each its correct tenant term, then re-run: %', dupes;
        END IF;
    END IF;

    IF to_regclass('"compliance"."discovered_data_locations"') IS NOT NULL THEN
        SELECT string_agg(
                   format('(%s, %s, %s, %s, %s) x%s',
                          data_subject_id_hash, table_name, field_name, record_id, tenant_id, n), '; ')
        INTO dupes
        FROM (
            SELECT data_subject_id_hash, table_name, field_name, record_id, tenant_id, COUNT(*) AS n
            FROM "compliance"."discovered_data_locations"
            GROUP BY data_subject_id_hash, table_name, field_name, record_id, tenant_id
            HAVING COUNT(*) > 1
        ) d;

        IF dupes IS NOT NULL THEN
            RAISE EXCEPTION '003 ABORTED: "compliance"."discovered_data_locations" holds rows that are duplicates under the new key (data_subject_id_hash, table_name, field_name, record_id, tenant_id). Nothing has been keyed and no row has been chosen over another. Resolve these, then re-run: %', dupes;
        END IF;
    END IF;
END
$$;

-- ---------------------------------------------------------------------------------------
-- 4) Rebuild the primary keys to include tenant_id.
--
--    The guard tests the KEY's COMPOSITION, not merely the constraint's existence, so a database
--    already carrying the wide key is left alone and one carrying the narrow key is rebuilt. Testing
--    only for the name would skip exactly the databases that need this.
--
--    The constraint name is read from the catalogue rather than assumed: 001 declares these keys
--    inline with no name, so PostgreSQL generates one, and a table created by the store's auto-create
--    path may carry a different generated name than one created by the script.
--
--    A column cannot be added to a key in place, so the constraint is dropped and recreated. Between
--    the DROP and the ADD the table has no primary key; run this with the store stopped.
-- ---------------------------------------------------------------------------------------
DO $$
DECLARE
    pk_name TEXT;
BEGIN
    SELECT con.conname INTO pk_name
    FROM pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
    WHERE nsp.nspname = 'compliance'
      AND rel.relname = 'data_inventory_registrations'
      AND con.contype = 'p'
      AND NOT EXISTS (
          SELECT 1
          FROM unnest(con.conkey) AS k(attnum)
          JOIN pg_attribute att ON att.attrelid = con.conrelid AND att.attnum = k.attnum
          WHERE att.attname = 'tenant_id'
      );

    IF pk_name IS NOT NULL THEN
        RAISE NOTICE '003: data_inventory_registrations — rebuilding PK to (table_name, field_name, tenant_id).';

        EXECUTE format(
            'ALTER TABLE "compliance"."data_inventory_registrations" DROP CONSTRAINT %I', pk_name);

        ALTER TABLE "compliance"."data_inventory_registrations"
            ADD PRIMARY KEY (table_name, field_name, tenant_id);
    END IF;

    SELECT con.conname INTO pk_name
    FROM pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
    WHERE nsp.nspname = 'compliance'
      AND rel.relname = 'discovered_data_locations'
      AND con.contype = 'p'
      AND NOT EXISTS (
          SELECT 1
          FROM unnest(con.conkey) AS k(attnum)
          JOIN pg_attribute att ON att.attrelid = con.conrelid AND att.attnum = k.attnum
          WHERE att.attname = 'tenant_id'
      );

    IF pk_name IS NOT NULL THEN
        RAISE NOTICE '003: discovered_data_locations — rebuilding PK to include tenant_id.';

        EXECUTE format(
            'ALTER TABLE "compliance"."discovered_data_locations" DROP CONSTRAINT %I', pk_name);

        ALTER TABLE "compliance"."discovered_data_locations"
            ADD PRIMARY KEY (data_subject_id_hash, table_name, field_name, record_id, tenant_id);
    END IF;
END
$$;

-- ---------------------------------------------------------------------------------------
-- 5) The defaults. Stated unconditionally because SET DEFAULT is idempotent: re-running it writes the
--    same default the column already carries. Kept separate from the blocks above because a database
--    that reached NOT NULL by some other route still needs it, and those blocks no longer fire once
--    the column is total.
--
--    The default is what makes the column total for a writer that omits it entirely. The store always
--    binds the term explicitly (through KeyedTenantPartition, which has no empty inhabitant), so this
--    is a backstop for hand-written INSERTs rather than something the store relies on.
-- ---------------------------------------------------------------------------------------
ALTER TABLE "compliance"."data_inventory_registrations"
    ALTER COLUMN tenant_id SET DEFAULT '__untenanted__';

ALTER TABLE "compliance"."discovered_data_locations"
    ALTER COLUMN tenant_id SET DEFAULT '__untenanted__';

COMMIT;
