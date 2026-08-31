-- Oracle MIGRATION for Excalibur.Outbox.Oracle — OUTBOX TENANT TOTALITY
-- Version: 1.0
--
-- Converges OUTBOX.TENANT_ID onto a TOTAL representation: NOT NULL, defaulting to the reserved
-- '__untenanted__' sentinel. After this script there is exactly ONE way to say "this message has
-- no tenant", and it is a value rather than the absence of one.
--
-- BEFORE: TENANT_ID is nullable. A message staged without a tenant holds NULL, and a scoped
--         predicate compares a value against the absence of one -- which is never true, and is
--         never false either.
-- AFTER:  TENANT_ID is NOT NULL and every untenanted row holds '__untenanted__'. Existing
--         NVL/COALESCE folds on the read path still run and are now no-ops, so no query changes
--         and nothing breaks.
--
-- RUN ORDER
-- ---------
--   Fresh install .......... 001 already produces this shape. This script is then a no-op.
--   Existing install ....... run this once, after 001.
--
-- It contains no SQL*Plus directives, so it runs under SQLcl, SQL*Plus, SQL Developer or any
-- driver that can execute an anonymous PL/SQL block. The guarded block is terminated by a lone
-- "/" on its own line, which is how every Oracle tool delimits a block.
--
--
-- PAIRS WITH A CODE CHANGE. RUN THEM TOGETHER.
-- --------------------------------------------
-- The staging path used to bind the caller's raw tenant argument, which is null for an untenanted
-- message; it now binds the partition's term, which is never null. Applying this script against
-- an OLDER package would make every untenanted stage fail with ORA-01400 (cannot insert NULL).
-- Deploy the package first, or in the same window.
--
--
-- WHY THE SENTINEL IS NON-EMPTY, AND WHY THAT MATTERS MORE HERE THAN ELSEWHERE
-- ----------------------------------------------------------------------------
-- Oracle folds the empty string to NULL. A sentinel of '' would therefore be stored as NULL, so
-- the two spellings of "absent" this script exists to collapse would survive the collapse. The
-- reserved value is non-empty precisely so Oracle has something to store. It is also why the
-- backfill tests only for NULL: in Oracle there is no separate blank case to fold, the way there
-- is in Postgres.
--
--
-- WHY THERE IS NO PRE-FLIGHT COLLISION CHECK
-- -------------------------------------------
-- The event-store equivalent must check for duplicates before collapsing NULLs, because its
-- tenant column participates in a UNIQUE constraint over stream identity -- and Oracle treats
-- NULLs as DISTINCT in a unique index, so rows admitted while the column was nullable can collide
-- once folded together. Neither hazard exists here: this table's primary key is MESSAGE_ID alone,
-- and TENANT_ID appears in no unique constraint and no index. The check is omitted because it has
-- nothing to find, not because it was overlooked.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- The backfill writes every row whose TENANT_ID IS NULL and COMMITs before the MODIFY (the DDL
-- would commit implicitly in any case; doing it explicitly keeps the transaction boundary
-- visible). On a large or badly-drained outbox that UPDATE is the dominant cost and is not
-- resumable. Run with the processor stopped, and size the window against the pre-flight count.


-- ---------------------------------------------------------------------------------------
-- 0) PRE-FLIGHT. RUN THIS FIRST AND READ THE RESULT.
--    Reports how many rows the backfill will rewrite. Returns no rows when there is nothing to
--    do, which is the expected result on a fresh install.
-- ---------------------------------------------------------------------------------------
-- A refusal below must be visible to an unattended runner. Without this directive SQL*Plus exits 0
-- even when a block raises, so a pipeline records a declined migration as applied and runs the next
-- step against a database that was never changed. SQLcl and SQL Developer honour it too; drivers
-- that execute statements directly ignore client directives.
-- An operator running this inside an interactive session is ended by that non-zero exit;
-- to keep the session, issue WHENEVER SQLERROR CONTINUE before @-ing the file.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

SELECT COUNT(*) AS ROWS_TO_BACKFILL
  FROM OUTBOX
 WHERE TENANT_ID IS NULL
HAVING COUNT(*) > 0;


-- ---------------------------------------------------------------------------------------
-- 1) Backfill, THEN constrain — guarded so the whole step is a no-op on a converged database.
--
--    The order is not optional: MODIFY ... NOT NULL fails with ORA-02296 while any row still
--    holds NULL.
--
--    DEFAULT precedes NOT NULL, which is Oracle's required order for an inline column
--    constraint, and both are applied in ONE statement so the column cannot be left
--    half-converged if the run is interrupted between them.
--
--    The guard reads USER_TAB_COLUMNS.NULLABLE rather than attempting the MODIFY and swallowing
--    the error, so a re-run is a genuine no-op rather than a silent rewrite of every row.
-- ---------------------------------------------------------------------------------------
DECLARE
    v_nullable  USER_TAB_COLUMNS.NULLABLE%TYPE;
BEGIN
    SELECT NULLABLE INTO v_nullable
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'OUTBOX' AND COLUMN_NAME = 'TENANT_ID';

    IF v_nullable = 'Y' THEN
        EXECUTE IMMEDIATE
            'UPDATE OUTBOX SET TENANT_ID = ''__untenanted__'' WHERE TENANT_ID IS NULL';
        COMMIT;

        EXECUTE IMMEDIATE
            'ALTER TABLE OUTBOX MODIFY (TENANT_ID VARCHAR2(64) DEFAULT ''__untenanted__'' NOT NULL)';
    END IF;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        -- The column is absent: this database predates the tenant column entirely. Run 001 first.
        RAISE_APPLICATION_ERROR(-20002,
            'OUTBOX.TENANT_ID is not present. Run 001_CreateOutboxSchema.sql first.');
END;
/


-- ---------------------------------------------------------------------------------------
-- 2) The DEFAULT, for a database that reached NOT NULL by some other route and therefore skipped
--    the block above. Applying the same default twice is a no-op in Oracle, so this is safe to
--    re-run; it is separated for the same reason the SQL Server and Postgres equivalents are —
--    once step 1 runs the column is no longer nullable, and a single combined guard would skip
--    this step forever.
-- ---------------------------------------------------------------------------------------
DECLARE
    v_has_default  NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_has_default
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'OUTBOX' AND COLUMN_NAME = 'TENANT_ID' AND DATA_DEFAULT IS NOT NULL;

    IF v_has_default = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE OUTBOX MODIFY (TENANT_ID DEFAULT ''__untenanted__'')';
    END IF;
END;
/


-- ---------------------------------------------------------------------------------------
-- 3) VERIFY. Returns no rows when the migration is complete. Any row returned is a failure.
-- ---------------------------------------------------------------------------------------
SELECT COUNT(*) AS REMAINING_NULL_TENANTS
  FROM OUTBOX
 WHERE TENANT_ID IS NULL
HAVING COUNT(*) > 0;
