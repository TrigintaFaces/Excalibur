-- Postgres MIGRATION for Excalibur.Outbox.Postgres — DEAD-LETTER TENANT PROVENANCE
-- Version: 1.0
--
-- Adds public.outbox_dead_letters.tenant_id and makes it a component of the primary key, so a
-- dead-lettered message still records the tenant it came from.
--
-- BEFORE: the dead-letter table has eight columns and no tenant. The move copies seven columns out
--         of the outbox and then DELETES the source row, so the tenant that produced the message
--         exists nowhere in the database afterwards. An operator cannot attribute a dead letter,
--         and a redrive cannot return the message to the partition it came from.
-- AFTER:  the tenant is copied by the move, stored NOT NULL, and part of the key.
--
-- RUN ORDER
-- ---------
--   Fresh install .......... 001 already produces this shape. This script is then a no-op.
--   Existing install ....... run this once, after 001 and 002.
--
-- It is guarded and re-runnable: each step tests for the state it is about to create, so running it
-- twice, or against a database that is already converged, changes nothing.
--
--
-- PAIRS WITH A CODE CHANGE. RUN THEM TOGETHER.
-- --------------------------------------------
-- The move path now names tenant_id in its INSERT. Against a database that has NOT run this script
-- every dead-letter move fails with 42703 (column does not exist), which means a message that
-- exhausted its retries stays in the outbox and is retried forever. Deploy the package and run this
-- script in the same window.
--
--
-- ============================================================================================
-- WHAT HAPPENS TO DEAD LETTERS THAT ARE ALREADY IN THE TABLE. READ THIS BEFORE RUNNING.
-- ============================================================================================
-- Their originating tenant is NOT RECOVERABLE. It was never written here, and the outbox row it
-- could have been read from was deleted by the move that put the entry in this table. There is no
-- other copy. This script cannot restore information that was never recorded, and it does not
-- pretend to: it makes the loss explicit rather than leaving the column ambiguous.
--
-- Those rows are backfilled to the reserved untenanted key, because the column is being made total
-- and every row must carry a value. THAT VALUE IS NOT AN ATTRIBUTION. On a pre-existing row it
-- means "no tenant was recorded", which is NOT the same claim as "this message had no tenant".
--
--   *** DO NOT REDRIVE A PRE-EXISTING DEAD LETTER ON THE ASSUMPTION IT IS UNTENANTED. ***
--
-- A message that really belonged to a tenant would re-enter the untenanted partition instead of
-- that tenant's, which is a misrouting the store cannot detect for you.
--
-- The two populations ARE separable, and step 0 gives you what you need to separate them: note the
-- timestamp it prints. Every row already present carries a moved_on strictly earlier than that
-- instant; every row written afterwards carries a real, copied tenant. Keep the timestamp with your
-- deployment record — it is the only discriminator, and this script stores it nowhere.
--
--   SELECT * FROM public.outbox_dead_letters WHERE moved_on < TIMESTAMPTZ '<the printed instant>';
--
-- If those entries still matter to you, the cleanest option is to deal with them BEFORE upgrading,
-- while nothing is ambiguous: drain, redrive, or export them, then run this script against a table
-- whose remaining rows you are content to mark unattributable.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- The backfill writes every existing row. On a large dead-letter table that is the dominant cost;
-- it is a single set-based UPDATE and is not resumable. The primary-key rebuild takes an ACCESS
-- EXCLUSIVE lock and rewrites the index. Run both with the processor stopped, and size the window
-- against the count reported below.

-- ---------------------------------------------------------------------------------------
-- 0) PRE-FLIGHT. Reports how many existing dead letters will become unattributable, and prints the
--    instant that separates them from every row written afterwards. RECORD BOTH.
-- ---------------------------------------------------------------------------------------
DO $$
DECLARE
    v_rows BIGINT;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_schema = 'public' AND table_name = 'outbox_dead_letters')
    THEN
        RAISE NOTICE '003 pre-flight: public.outbox_dead_letters not present; run 001 first.';
    ELSIF EXISTS (SELECT 1 FROM information_schema.columns
                  WHERE table_schema = 'public' AND table_name = 'outbox_dead_letters'
                    AND column_name = 'tenant_id')
    THEN
        RAISE NOTICE '003 pre-flight: tenant_id already present; this run is a no-op.';
    ELSE
        SELECT COUNT(*) INTO v_rows FROM public.outbox_dead_letters;
        RAISE NOTICE '003 pre-flight: % existing dead letter(s) have no recorded tenant and CANNOT be attributed. They are backfilled to the reserved untenanted key, which records "not captured" and is NOT evidence the message was untenanted. Separator instant (RECORD THIS): %. Rows with moved_on < that instant are the unattributable ones.',
            v_rows, clock_timestamp();
    END IF;
END $$;

-- ---------------------------------------------------------------------------------------
-- 1) Add the column NULLABLE first, then backfill, then constrain. The order is not optional:
--    adding a NOT NULL column with no default fails outright on a table that already has rows, and
--    SET NOT NULL fails while any row still holds NULL.
--
--    The fresh-install schema declares this column with NO DEFAULT, deliberately: the move copies
--    the value from the outbox row, whose own tenant column is already total, so a writer never has
--    to supply it, and a hand-written INSERT that omits it should fail loudly rather than silently
--    record a message as untenanted. This script therefore leaves no default behind either.
-- ---------------------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables
               WHERE table_schema = 'public' AND table_name = 'outbox_dead_letters')
       AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_schema = 'public' AND table_name = 'outbox_dead_letters'
                         AND column_name = 'tenant_id')
    THEN
        ALTER TABLE public.outbox_dead_letters ADD COLUMN tenant_id VARCHAR(64);

        -- Every existing row, by definition: the column did not exist a statement ago, so all of
        -- them hold NULL and all of them are unattributable. See the block above.
        UPDATE public.outbox_dead_letters SET tenant_id = '__untenanted__' WHERE tenant_id IS NULL;

        ALTER TABLE public.outbox_dead_letters ALTER COLUMN tenant_id SET NOT NULL;
    END IF;
END $$;

-- ---------------------------------------------------------------------------------------
-- 2) Fold NULL or blank into the reserved key, for a database that reached this column by some
--    other route. Blanks are folded alongside NULLs because the read path already treats a blank
--    stored value as untenanted, so leaving one would satisfy NOT NULL while preserving the split
--    the reserved key exists to remove. The reserved key is non-empty on purpose: the empty string
--    is not portable, since Oracle folds '' to NULL.
-- ---------------------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'outbox_dead_letters'
                 AND column_name = 'tenant_id' AND is_nullable = 'YES')
    THEN
        UPDATE public.outbox_dead_letters
            SET tenant_id = '__untenanted__'
            WHERE tenant_id IS NULL OR btrim(tenant_id) = '';

        ALTER TABLE public.outbox_dead_letters ALTER COLUMN tenant_id SET NOT NULL;
    END IF;
END $$;

-- ---------------------------------------------------------------------------------------
-- 3) Widen the primary key to (message_id, tenant_id).
--
--    This is safe HERE and would NOT be safe on the outbox table. The outbox is drained by a
--    claim-then-mark protocol that addresses a row by its id alone, so widening that key lets one
--    mark hit another partition's row or no row at all. Nothing addresses a dead letter by id: the
--    read path pages by age and attempts, and the statistics path counts. Widening cannot admit a
--    duplicate either, because the outbox message id it inherits is already unique.
--
--    The existing constraint is dropped by its real name rather than an assumed one: a table created
--    by an older version of the create script carries Postgres's implicit 'outbox_dead_letters_pkey',
--    while the current script names it explicitly.
-- ---------------------------------------------------------------------------------------
DO $$
DECLARE
    v_pk_name TEXT;
    v_pk_cols TEXT;
BEGIN
    SELECT c.conname,
           (SELECT string_agg(a.attname, ',' ORDER BY a.attname)
              FROM unnest(c.conkey) AS k(attnum)
              JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum)
      INTO v_pk_name, v_pk_cols
      FROM pg_constraint c
     WHERE c.conrelid = 'public.outbox_dead_letters'::regclass
       AND c.contype = 'p';

    IF v_pk_name IS NULL THEN
        ALTER TABLE public.outbox_dead_letters
            ADD CONSTRAINT pk_outbox_dead_letters PRIMARY KEY (message_id, tenant_id);
    ELSIF v_pk_cols IS DISTINCT FROM 'message_id,tenant_id' THEN
        EXECUTE format('ALTER TABLE public.outbox_dead_letters DROP CONSTRAINT %I', v_pk_name);
        ALTER TABLE public.outbox_dead_letters
            ADD CONSTRAINT pk_outbox_dead_letters PRIMARY KEY (message_id, tenant_id);
    END IF;
EXCEPTION
    WHEN undefined_table THEN
        RAISE NOTICE '003: public.outbox_dead_letters not present; run 001 first.';
END $$;

-- ---------------------------------------------------------------------------------------
-- 4) VERIFY. Returns no rows when the migration is complete. Any row returned is a failure.
-- ---------------------------------------------------------------------------------------
SELECT 'tenant_id still nullable' AS failure
  FROM information_schema.columns
 WHERE table_schema = 'public' AND table_name = 'outbox_dead_letters'
   AND column_name = 'tenant_id' AND is_nullable = 'YES'
UNION ALL
SELECT 'primary key does not include tenant_id' AS failure
 WHERE NOT EXISTS (
    SELECT 1
      FROM pg_constraint c
      JOIN unnest(c.conkey) AS k(attnum) ON TRUE
      JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum
     WHERE c.conrelid = 'public.outbox_dead_letters'::regclass
       AND c.contype = 'p' AND a.attname = 'tenant_id');
