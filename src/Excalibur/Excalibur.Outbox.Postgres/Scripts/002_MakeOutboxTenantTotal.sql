-- Postgres MIGRATION for Excalibur.Outbox.Postgres — OUTBOX TENANT TOTALITY
-- Version: 1.0
--
-- Converges public.outbox.tenant_id onto a TOTAL representation: NOT NULL, defaulting to the
-- reserved '__untenanted__' sentinel. After this script there is exactly ONE way to say "this
-- message has no tenant", and it is a value rather than the absence of one.
--
-- BEFORE: tenant_id is nullable. A message staged without a tenant holds NULL, and a scoped
--         predicate compares a value against the absence of one -- which is never true, and is
--         never false either.
-- AFTER:  tenant_id is NOT NULL and every untenanted row holds '__untenanted__'. Existing
--         COALESCE(tenant_id, ...) folds on the read path still run and are now no-ops, so no
--         query changes and nothing breaks.
--
-- RUN ORDER
-- ---------
--   Fresh install .......... 001 already produces this shape. This script is then a no-op.
--   Existing install ....... run this once, after 001.
--
-- It is guarded and re-runnable: each step tests for the state it is about to create, so running
-- it twice, or against a database that is already converged, changes nothing.
--
--
-- PAIRS WITH A CODE CHANGE. RUN THEM TOGETHER.
-- --------------------------------------------
-- The staging path used to bind the caller's raw tenant argument, which is null for an untenanted
-- message; it now binds the partition's term, which is never null. Applying this script against
-- an OLDER package would make every untenanted stage fail with a not-null violation. Deploy the
-- package first, or in the same window.
--
--
-- WHY THERE IS NO PRE-FLIGHT COLLISION CHECK
-- -------------------------------------------
-- The event-store equivalent of this script has to check for duplicates before collapsing NULLs,
-- because its tenant column participates in a UNIQUE constraint over stream identity, so two rows
-- distinguished only by NULL-vs-sentinel would collide once folded together. This table's primary
-- key is message_id alone and tenant_id appears in no unique constraint and no index, so the
-- backfill cannot manufacture a collision. The check is omitted because it has nothing to find,
-- not because it was overlooked.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- The backfill writes every row whose tenant_id IS NULL. On a large or badly-drained outbox that
-- is the dominant cost; it is a single set-based UPDATE and is not resumable. Run it with the
-- processor stopped, and size the window against the count reported below.

-- ---------------------------------------------------------------------------------------
-- 0) PRE-FLIGHT. Reports how many rows the backfill will touch. Read it before proceeding.
-- ---------------------------------------------------------------------------------------
DO $$
DECLARE
    v_null_rows BIGINT;
    v_empty_rows BIGINT;
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'outbox' AND column_name = 'tenant_id')
    THEN
        SELECT COUNT(*) INTO v_null_rows FROM public.outbox WHERE tenant_id IS NULL;
        SELECT COUNT(*) INTO v_empty_rows FROM public.outbox WHERE btrim(tenant_id) = '';
        RAISE NOTICE '002 pre-flight: % NULL and % blank tenant row(s) will be backfilled to the sentinel.',
            v_null_rows, v_empty_rows;
    ELSE
        RAISE NOTICE '002 pre-flight: public.outbox.tenant_id not present; nothing to do.';
    END IF;
END $$;

-- ---------------------------------------------------------------------------------------
-- 1) Backfill, THEN constrain. The order is not optional: SET NOT NULL fails outright while any
--    row still holds NULL.
--
--    Blank tenants are folded onto the sentinel alongside NULLs. This is not scope creep: the
--    read path already treats a blank stored value as untenanted (KeyedTenantPartition.
--    FromStoredValue maps null, empty and the sentinel alike onto the untenanted partition), so a
--    blank row is ALREADY read as untenanted while being stored as something else. Leaving it
--    would satisfy NOT NULL while preserving the very split this script exists to remove.
-- ---------------------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'outbox'
                 AND column_name = 'tenant_id' AND is_nullable = 'YES')
    THEN
        UPDATE public.outbox
            SET tenant_id = '__untenanted__'
            WHERE tenant_id IS NULL OR btrim(tenant_id) = '';

        ALTER TABLE public.outbox ALTER COLUMN tenant_id SET NOT NULL;
    END IF;
END $$;

-- ---------------------------------------------------------------------------------------
-- 2) The default. This is what makes the column total for a writer that omits it entirely; the
--    store always binds the term explicitly, so it is a backstop for hand-written INSERTs.
--
--    Unguarded on purpose. ALTER COLUMN ... SET DEFAULT is idempotent in Postgres -- re-applying
--    the same default is a no-op rather than an error -- so a guard here would add a branch
--    without removing a failure. (The SQL Server equivalent IS guarded, because ADD CONSTRAINT
--    fails when the constraint already exists. Same intent, different dialect.)
--
--    Applied outside the block above so that a database which reached NOT NULL by some other
--    route still gets the default: once that block runs, the column is no longer nullable, and a
--    single combined guard would skip this step forever.
-- ---------------------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'outbox' AND column_name = 'tenant_id')
    THEN
        ALTER TABLE public.outbox ALTER COLUMN tenant_id SET DEFAULT '__untenanted__';
    END IF;
END $$;
