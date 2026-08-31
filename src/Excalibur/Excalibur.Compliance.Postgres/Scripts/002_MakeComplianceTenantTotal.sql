-- PostgreSQL MIGRATION for Excalibur.Compliance.Postgres — ERASURE + LEGAL HOLD TENANT TOTALITY
-- Version: 1.0
--
-- Converges "compliance"."erasure_requests".tenant_id and "compliance"."legal_holds".tenant_id onto a
-- TOTAL representation: NOT NULL, defaulting to the reserved '__untenanted__' sentinel. After this
-- script there is exactly ONE way to say "this row has no tenant", and it is a value rather than the
-- absence of one — the same shape "compliance"."data_inventory_registrations" and
-- "compliance"."discovered_data_locations" already carry in this schema.
--
-- BEFORE: tenant_id is nullable. A row written before tenancy existed holds NULL; a row written since
--         holds '__untenanted__'. The legal-hold read path folds the two together with
--         "(tenant_id = @AmbientTenantId OR tenant_id IS NULL)". Two spellings, one meaning.
-- AFTER:  tenant_id is NOT NULL and every untenanted row holds '__untenanted__'. The legal-hold read
--         matches the sentinel explicitly, so a global hold stays visible to a scoped tenant.
--
--
-- RUN THIS TOGETHER WITH THE PACKAGE THAT INTRODUCED IT. NOT BEFORE, NOT ALONE.
-- ----------------------------------------------------------------------------
-- This script and the read predicate that goes with it are one change. Applied on its own, against a
-- package version whose legal-hold read still says "OR tenant_id IS NULL" and nothing else, the
-- backfill below moves every global hold from NULL to the sentinel — and that predicate then matches
-- NEITHER arm. Global holds go dark for every scoped tenant.
--
-- A legal hold BLOCKS erasure. Losing one does not fail safe. It erases data a court order says to
-- keep. That is why the package's read path was widened to match the sentinel in the SAME release
-- that added this file, and why running this script against an older package is not a partial
-- upgrade but a data-destruction path.
--
-- The reverse order is safe: the new package's read still carries an "OR tenant_id IS NULL" arm, so
-- it reads a not-yet-migrated database correctly. Upgrade the package first, then run this. That arm
-- is transition tolerance for exactly this window and is dead once the column is total.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run during a maintenance window with the erasure and legal-hold stores stopped. Take a backup you
-- have restored at least once.
--
-- Two things this needs that the SQL Server twin needs and this does not, recorded so the difference
-- is not mistaken for an omission:
--
--   No index drop.      ix_erasure_requests_tenant and ix_legal_holds_tenant lead with this column,
--                       but ALTER COLUMN ... SET NOT NULL only attaches a constraint; it does not
--                       rewrite the column type, so PostgreSQL keeps the indexes in place. SQL Server
--                       refuses the equivalent ALTER while an index depends on the column, which is
--                       why its twin drops and recreates.
--   No collation clause. PostgreSQL's default collations are deterministic, so '=' on VARCHAR is
--                       already case-sensitive and agrees with the framework's ordinal comparison.
--                       SQL Server's database default is typically case-INSENSITIVE, so its twin has
--                       to state a binary collation to reach the behaviour this dialect starts with.
--
-- No pre-flight collision check is needed. tenant_id participates in NO primary key and NO unique
-- constraint on either table — the keys are request_id and hold_id — so collapsing NULL onto the
-- sentinel cannot manufacture a uniqueness violation. It only ever rewrites a column value.
--
-- SCOPE: this script targets the DEFAULT object names created by 001. A deployment that configured
-- custom schema or table names provisions through the store's own auto-create path, which emits the
-- total shape directly; such a database needs no migration and this script correctly does nothing.
--
-- It is guarded and re-runnable: every step tests for the state it is about to create, so running it
-- twice, or against a database that is already converged, changes nothing.

-- ---------------------------------------------------------------------------------------
-- 1) "compliance"."erasure_requests"
--
--    Backfill BEFORE the constraint. This order is not optional: SET NOT NULL fails outright if any
--    row still holds NULL.
-- ---------------------------------------------------------------------------------------
DO $$
DECLARE
    null_rows BIGINT;
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'compliance'
          AND table_name = 'erasure_requests'
          AND column_name = 'tenant_id'
          AND is_nullable = 'YES'
    ) THEN
        SELECT COUNT(*) INTO null_rows
        FROM "compliance"."erasure_requests"
        WHERE tenant_id IS NULL;

        RAISE NOTICE '002: erasure_requests — % row(s) backfilled to the sentinel.', null_rows;

        UPDATE "compliance"."erasure_requests"
            SET tenant_id = '__untenanted__'
            WHERE tenant_id IS NULL;

        ALTER TABLE "compliance"."erasure_requests"
            ALTER COLUMN tenant_id SET NOT NULL;
    END IF;
END
$$;

-- ---------------------------------------------------------------------------------------
-- 2) "compliance"."legal_holds". Same shape as step 1.
-- ---------------------------------------------------------------------------------------
DO $$
DECLARE
    null_rows BIGINT;
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'compliance'
          AND table_name = 'legal_holds'
          AND column_name = 'tenant_id'
          AND is_nullable = 'YES'
    ) THEN
        SELECT COUNT(*) INTO null_rows
        FROM "compliance"."legal_holds"
        WHERE tenant_id IS NULL;

        RAISE NOTICE '002: legal_holds — % GLOBAL hold(s) backfilled to the sentinel. These stay visible to every scoped tenant through the sentinel arm of the read predicate.', null_rows;

        UPDATE "compliance"."legal_holds"
            SET tenant_id = '__untenanted__'
            WHERE tenant_id IS NULL;

        ALTER TABLE "compliance"."legal_holds"
            ALTER COLUMN tenant_id SET NOT NULL;
    END IF;
END
$$;

-- ---------------------------------------------------------------------------------------
-- 3) The defaults. Stated unconditionally because SET DEFAULT is idempotent: re-running it writes
--    the same default the column already carries. Kept separate from the blocks above because a
--    database that reached NOT NULL by some other route still needs it, and those blocks no longer
--    fire once the column is total.
--
--    The default is what makes the column total for a writer that omits it entirely. Both stores
--    always bind the term explicitly (through KeyedTenantPartition, which has no empty inhabitant),
--    so this is a backstop for hand-written INSERTs rather than something the stores rely on.
-- ---------------------------------------------------------------------------------------
ALTER TABLE "compliance"."erasure_requests"
    ALTER COLUMN tenant_id SET DEFAULT '__untenanted__';

ALTER TABLE "compliance"."legal_holds"
    ALTER COLUMN tenant_id SET DEFAULT '__untenanted__';
