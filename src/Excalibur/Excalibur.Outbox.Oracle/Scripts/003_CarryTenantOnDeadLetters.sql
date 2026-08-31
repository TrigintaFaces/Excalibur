-- Oracle MIGRATION for Excalibur.Outbox.Oracle — DEAD-LETTER TENANT PROVENANCE
-- Version: 1.0
--
-- Adds OUTBOX_DEAD_LETTERS.TENANT_ID and makes it a component of the table's unique key, so a
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
-- It contains no SQL*Plus directives, so it runs under SQLcl, SQL*Plus, SQL Developer or any driver
-- that can execute an anonymous PL/SQL block. Each guarded block is terminated by a lone "/" on its
-- own line, which is how every Oracle tool delimits a block. Every block tests for the state it is
-- about to create, so a second run changes nothing.
--
--
-- PAIRS WITH A CODE CHANGE. RUN THEM TOGETHER.
-- --------------------------------------------
-- The move path now names TENANT_ID in its INSERT. Against a database that has NOT run this script
-- every dead-letter move fails with ORA-00904 (invalid identifier), which means a message that
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
-- timestamp it prints. Every row already present carries a MOVED_ON strictly earlier than that
-- instant; every row written afterwards carries a real, copied tenant. Keep the timestamp with your
-- deployment record — it is the only discriminator, and this script stores it nowhere.
--
--   SELECT * FROM OUTBOX_DEAD_LETTERS WHERE MOVED_ON < TIMESTAMP '<the printed instant>';
--
-- If those entries still matter to you, the cleanest option is to deal with them BEFORE upgrading,
-- while nothing is ambiguous: drain, redrive, or export them, then run this script against a table
-- whose remaining rows you are content to mark unattributable.
--
--
-- WHY THE RESERVED KEY IS NON-EMPTY, AND WHY THAT MATTERS MORE HERE THAN ELSEWHERE
-- ---------------------------------------------------------------------------------
-- Oracle folds the empty string to NULL. A sentinel of '' would therefore be stored as NULL, so a
-- column declared NOT NULL could not hold it at all, and the two spellings of "absent" this script
-- exists to collapse would survive the collapse. The reserved value is non-empty precisely so Oracle
-- has something to store. It is also why the backfill tests only for NULL: in Oracle there is no
-- separate blank case to fold, the way there is in Postgres.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- The backfill writes every existing row and COMMITs before the MODIFY (the DDL would commit
-- implicitly in any case; doing it explicitly keeps the transaction boundary visible). On a large
-- dead-letter table that UPDATE is the dominant cost and is not resumable. The key rebuild drops and
-- recreates a unique index. Run both with the processor stopped, and size the window against the
-- pre-flight count.


-- ---------------------------------------------------------------------------------------
-- 0) PRE-FLIGHT. RUN THIS FIRST AND READ THE RESULT.
--    Reports how many existing dead letters become unattributable, and the instant that separates
--    them from every row written afterwards. RECORD BOTH. Returns no rows on a converged database.
-- ---------------------------------------------------------------------------------------
-- A refusal below must be visible to an unattended runner. Without this directive SQL*Plus exits 0
-- even when a block raises, so a pipeline records a declined migration as applied and runs the next
-- step against a database that was never changed. SQLcl and SQL Developer honour it too; drivers
-- that execute statements directly ignore client directives.
-- An operator running this inside an interactive session is ended by that non-zero exit;
-- to keep the session, issue WHENEVER SQLERROR CONTINUE before @-ing the file.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

SELECT COUNT(*)        AS UNATTRIBUTABLE_DEAD_LETTERS,
       SYSTIMESTAMP    AS SEPARATOR_INSTANT_RECORD_THIS
  FROM OUTBOX_DEAD_LETTERS
 WHERE NOT EXISTS (SELECT 1 FROM USER_TAB_COLUMNS
                    WHERE TABLE_NAME = 'OUTBOX_DEAD_LETTERS' AND COLUMN_NAME = 'TENANT_ID')
HAVING COUNT(*) > 0;


-- ---------------------------------------------------------------------------------------
-- 1) Add the column NULLABLE first, then backfill, then constrain. The order is not optional:
--    adding a NOT NULL column with no default fails outright on a table that already has rows
--    (ORA-01758), and MODIFY ... NOT NULL fails with ORA-02296 while any row still holds NULL.
--
--    The fresh-install schema declares this column with NO DEFAULT, deliberately: the move copies
--    the value from the outbox row, whose own tenant column is already total, so a writer never has
--    to supply it, and a hand-written INSERT that omits it should fail loudly with ORA-01400 rather
--    than silently record a message as untenanted. This script therefore leaves no default behind.
--
--    The guard reads USER_TAB_COLUMNS rather than attempting the ADD and swallowing the error, so a
--    re-run is a genuine no-op rather than a silent rewrite of every row.
-- ---------------------------------------------------------------------------------------
DECLARE
    v_table_count   NUMBER;
    v_column_count  NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_table_count
      FROM USER_TABLES WHERE TABLE_NAME = 'OUTBOX_DEAD_LETTERS';

    IF v_table_count = 0 THEN
        RAISE_APPLICATION_ERROR(-20003,
            'OUTBOX_DEAD_LETTERS is not present. Run 001_CreateOutboxSchema.sql first.');
    END IF;

    SELECT COUNT(*) INTO v_column_count
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'OUTBOX_DEAD_LETTERS' AND COLUMN_NAME = 'TENANT_ID';

    IF v_column_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE OUTBOX_DEAD_LETTERS ADD (TENANT_ID VARCHAR2(64))';

        -- Every existing row, by definition: the column did not exist a statement ago, so all of
        -- them hold NULL and all of them are unattributable. See the block above.
        EXECUTE IMMEDIATE
            'UPDATE OUTBOX_DEAD_LETTERS SET TENANT_ID = ''__untenanted__'' WHERE TENANT_ID IS NULL';
        COMMIT;

        EXECUTE IMMEDIATE 'ALTER TABLE OUTBOX_DEAD_LETTERS MODIFY (TENANT_ID VARCHAR2(64) NOT NULL)';
    END IF;
END;
/


-- ---------------------------------------------------------------------------------------
-- 2) Close the column for a database that reached it by some other route and left it nullable.
--    Separated from the block above for the same reason the sibling scripts separate their steps:
--    once step 1 runs the column exists, and a single combined guard would skip this forever.
-- ---------------------------------------------------------------------------------------
DECLARE
    v_nullable  USER_TAB_COLUMNS.NULLABLE%TYPE;
BEGIN
    SELECT NULLABLE INTO v_nullable
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'OUTBOX_DEAD_LETTERS' AND COLUMN_NAME = 'TENANT_ID';

    IF v_nullable = 'Y' THEN
        EXECUTE IMMEDIATE
            'UPDATE OUTBOX_DEAD_LETTERS SET TENANT_ID = ''__untenanted__'' WHERE TENANT_ID IS NULL';
        COMMIT;

        EXECUTE IMMEDIATE 'ALTER TABLE OUTBOX_DEAD_LETTERS MODIFY (TENANT_ID VARCHAR2(64) NOT NULL)';
    END IF;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        RAISE_APPLICATION_ERROR(-20004,
            'OUTBOX_DEAD_LETTERS.TENANT_ID is not present. Step 1 of this script should have added it.');
END;
/


-- ---------------------------------------------------------------------------------------
-- 3) Widen the unique key to (MESSAGE_ID, TENANT_ID).
--
--    This is safe HERE and would NOT be safe on the OUTBOX table. The outbox is drained by a
--    claim-then-mark protocol that addresses a row by its id alone, so widening that key lets one
--    mark hit another partition's row or no row at all. Nothing addresses a dead letter by id: the
--    read path pages by age and attempts, and the statistics path counts. Widening cannot admit a
--    duplicate either, because the outbox message id it inherits is already unique.
--
--    Oracle treats NULLs as DISTINCT in a unique index, which is why the column is closed in step 1
--    BEFORE the key is widened here: widening first would let two rows differing only by
--    NULL-versus-reserved-key coexist, and the MODIFY would then have nothing to reject.
-- ---------------------------------------------------------------------------------------
DECLARE
    v_is_correct  NUMBER;
    v_exists      NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists
      FROM USER_CONSTRAINTS
     WHERE TABLE_NAME = 'OUTBOX_DEAD_LETTERS' AND CONSTRAINT_NAME = 'UQ_OUTBOX_DLQ_MESSAGE_ID';

    SELECT COUNT(*) INTO v_is_correct
      FROM USER_CONS_COLUMNS
     WHERE TABLE_NAME = 'OUTBOX_DEAD_LETTERS' AND CONSTRAINT_NAME = 'UQ_OUTBOX_DLQ_MESSAGE_ID'
       AND COLUMN_NAME = 'TENANT_ID';

    IF v_exists > 0 AND v_is_correct = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE OUTBOX_DEAD_LETTERS DROP CONSTRAINT UQ_OUTBOX_DLQ_MESSAGE_ID';
    END IF;

    IF v_is_correct = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE OUTBOX_DEAD_LETTERS ADD CONSTRAINT UQ_OUTBOX_DLQ_MESSAGE_ID UNIQUE (MESSAGE_ID, TENANT_ID)';
    END IF;
END;
/


-- ---------------------------------------------------------------------------------------
-- 4) VERIFY. Returns no rows when the migration is complete. Any row returned is a failure.
-- ---------------------------------------------------------------------------------------
SELECT 'TENANT_ID still nullable' AS FAILURE
  FROM USER_TAB_COLUMNS
 WHERE TABLE_NAME = 'OUTBOX_DEAD_LETTERS' AND COLUMN_NAME = 'TENANT_ID' AND NULLABLE = 'Y'
UNION ALL
SELECT 'unique key does not include TENANT_ID' AS FAILURE
  FROM DUAL
 WHERE NOT EXISTS (SELECT 1 FROM USER_CONS_COLUMNS
                    WHERE TABLE_NAME = 'OUTBOX_DEAD_LETTERS'
                      AND CONSTRAINT_NAME = 'UQ_OUTBOX_DLQ_MESSAGE_ID'
                      AND COLUMN_NAME = 'TENANT_ID');
