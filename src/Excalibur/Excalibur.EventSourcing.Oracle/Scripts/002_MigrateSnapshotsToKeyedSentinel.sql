-- Oracle Migration for Excalibur.EventSourcing.Oracle — SNAPSHOT STORE
-- Version: 1.0
--
-- Converges the snapshot store's untenanted representation onto the reserved
-- '__untenanted__' sentinel, matching the event store and the other providers.
--
-- BEFORE: TENANTID is nullable; an untenanted snapshot carries NULL, and uniqueness is
--         held by a function-based index over NVL(TENANTID, CHR(1)).
-- AFTER:  TENANTID is NOT NULL and carries '__untenanted__'; uniqueness is a plain index
--         over the triple, identical to the SQL Server and PostgreSQL schemas.
--
-- Run 001_CreateSnapshotSchema.sql for NEW deployments. This script is only for a schema
-- created by an earlier revision that still holds NULL tenants.
--
-- ---------------------------------------------------------------------------------------
-- STEP 0 — PRE-FLIGHT. RUN THIS FIRST AND READ THE RESULT. DO NOT SKIP.
-- ---------------------------------------------------------------------------------------
--
-- The rewrite below is safe in the general case: NVL(TENANTID, CHR(1)) already collapses
-- every untenanted row for a given (AGGREGATEID, AGGREGATETYPE) into ONE uniqueness class,
-- so rewriting those NULLs to '__untenanted__' preserves the identical classes and cannot
-- manufacture a new violation. No real tenant can occupy the sentinel: a scoped tenant that
-- names it is rejected before it reaches the database.
--
-- There is exactly ONE case where that does not hold. A table already holding BOTH a
-- literal '__untenanted__' row AND a NULL row for the same (AGGREGATEID, AGGREGATETYPE)
-- has two DISTINCT index entries today ('__untenanted__' and CHR(1)). After the rewrite
-- they collide and the migration fails partway.
--
-- That failure is CORRECT — it means the data already holds two rows each claiming to be
-- the same untenanted snapshot, and only an operator can decide which survives. But it must
-- surface HERE, as a query, not as a half-applied migration.
--
-- Expected result: NO ROWS. Any row returned is a genuine data conflict — resolve it before
-- continuing. Do not proceed on a non-empty result.
--
-- If you apply this file unattended rather than reading the result, step 3 re-measures the same
-- condition and refuses on it, so the conflict cannot reach the index rebuild either way. This
-- query is still the better place to find out: it names the aggregates before anything is written.

-- A refusal below must be visible to an unattended runner. Without this directive SQL*Plus exits 0
-- even when a block raises, so a pipeline records a declined migration as applied and runs the next
-- step against a database that was never changed. SQLcl and SQL Developer honour it too; drivers
-- that execute statements directly ignore client directives.
-- An operator running this inside an interactive session is ended by that non-zero exit;
-- to keep the session, issue WHENEVER SQLERROR CONTINUE before @-ing the file.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

SELECT AGGREGATEID,
       AGGREGATETYPE,
       COUNT(*) AS COLLIDING_ROWS
  FROM EVENTSTORESNAPSHOTS
 WHERE TENANTID IS NULL
    OR TENANTID = '__untenanted__'
 GROUP BY AGGREGATEID, AGGREGATETYPE
HAVING COUNT(*) > 1;

-- ---------------------------------------------------------------------------------------
-- STEP 1 — Rewrite the NULL tenant onto the sentinel.
-- ---------------------------------------------------------------------------------------
-- Only rows that are genuinely untenanted are touched. A row carrying a real tenant is not
-- matched by this predicate and is left exactly as it is.

UPDATE EVENTSTORESNAPSHOTS
   SET TENANTID = '__untenanted__'
 WHERE TENANTID IS NULL;

COMMIT;

-- ---------------------------------------------------------------------------------------
-- STEP 2 — Close the column.
-- ---------------------------------------------------------------------------------------
-- This ALTER fails if step 1 did not run or did not commit. That is the intended behaviour:
-- it is the database refusing to let the schema claim a guarantee the data does not meet.
--
-- No DEFAULT is added. The tenant is part of the unique key, and defaulting a key column
-- would make "I forgot to supply the tenant" indistinguishable from "this row is
-- deliberately untenanted." The store writes the sentinel explicitly on every save.
--
-- Skipped only when the column is ALREADY closed, which is the one case where re-stating it
-- would raise ORA-01442 without the guarantee being any different. That is not a relaxation of
-- the paragraph above: a column that is already NOT NULL has nothing left to refuse.

DECLARE
    v_nullable  USER_TAB_COLUMNS.NULLABLE%TYPE;
BEGIN
    SELECT NULLABLE INTO v_nullable
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'EVENTSTORESNAPSHOTS' AND COLUMN_NAME = 'TENANTID';

    IF v_nullable = 'Y' THEN
        EXECUTE IMMEDIATE 'ALTER TABLE EVENTSTORESNAPSHOTS MODIFY (TENANTID VARCHAR2(64) NOT NULL)';
    ELSE
        DBMS_OUTPUT.PUT_LINE('002: EVENTSTORESNAPSHOTS.TENANTID is already NOT NULL; nothing to close.');
    END IF;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        RAISE_APPLICATION_ERROR(-20002,
            '002 REFUSED: EVENTSTORESNAPSHOTS has no TENANTID column, so this table did not come from any version of 001_CreateSnapshotSchema.sql. Nothing has been changed. Reconcile the table against 001 before upgrading it -- this script converges an existing tenant column onto the reserved sentinel, it does not add one.');
END;
/

-- ---------------------------------------------------------------------------------------
-- STEP 3 — Replace the function-based index with a plain one.
-- ---------------------------------------------------------------------------------------
-- NVL(TENANTID, CHR(1)) existed only because the column was nullable: Oracle treats NULLs
-- as DISTINCT in a unique index, so a plain UNIQUE over the triple would not have
-- constrained untenanted rows at all. With TENANTID NOT NULL there are no NULLs to
-- collapse, that problem cannot arise, and the workaround is dead code.
--
-- It is dropped rather than kept alongside: two unique indexes over the same logical key
-- would both have to be satisfied, and the surviving NVL form would silently re-admit the
-- assumption this migration exists to remove.
--
-- THE COLLISION CHECK BELOW IS THE STEP 0 QUERY, MADE ENFORCING. Step 0 is a SELECT an operator
-- has to read, and the header asks them to; nothing stopped a runner that applied this file
-- unattended from carrying a collision straight into this step. That matters HERE more than
-- anywhere else in the script, because Oracle commits DDL implicitly: the drop and the recreate
-- cannot be one unit of work, so a CREATE that fails with ORA-01452 leaves the snapshot store with
-- NO uniqueness on (AGGREGATEID, AGGREGATETYPE, TENANTID) at all. Two snapshots could then be
-- stored for one aggregate and a load would return whichever the plan reached first -- silently,
-- since a snapshot is a cache the store is entitled to find.
--
-- So the condition is measured again, immediately before the drop, against the post-rewrite data
-- rather than the pre-flight's. A refusal here leaves the existing unique index in place.
--
-- The guard reads INDEX_TYPE rather than the index's existence, because the replacement reuses the
-- name: FUNCTION-BASED NORMAL is the NVL form this step replaces, NORMAL is the plain one it
-- creates. A second run therefore drops nothing.

DECLARE
    v_index_type  USER_INDEXES.INDEX_TYPE%TYPE;
    v_collisions  NUMBER;
BEGIN
    BEGIN
        SELECT INDEX_TYPE INTO v_index_type
          FROM USER_INDEXES
         WHERE INDEX_NAME = 'UQ_EVENTSTORESNAPSHOTS_AGG';
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RAISE_APPLICATION_ERROR(-20002,
                '002 REFUSED: EVENTSTORESNAPSHOTS has no UQ_EVENTSTORESNAPSHOTS_AGG index, so the snapshot key this step replaces is not there to replace. Steps 1 and 2 have already run and are correct on their own; only this step has been declined. Either the table did not come from any version of 001_CreateSnapshotSchema.sql, or the index was dropped by hand. Reconcile the schema against 001 before continuing -- creating a unique index here without knowing why the old one is absent could fail on data the missing index was never enforcing.');
    END;

    IF v_index_type = 'NORMAL' THEN
        DBMS_OUTPUT.PUT_LINE('002: UQ_EVENTSTORESNAPSHOTS_AGG is already the plain triple index; nothing to replace.');
        RETURN;
    END IF;

    -- Dynamic rather than static: PL/SQL validates static SQL against the data dictionary when the
    -- block is COMPILED, not when it runs, so a missing table or column would fail this block
    -- before either guard above could report what is actually wrong.
    EXECUTE IMMEDIATE
        'SELECT COUNT(*) FROM (SELECT 1 FROM EVENTSTORESNAPSHOTS
                                GROUP BY AGGREGATEID, AGGREGATETYPE, TENANTID
                               HAVING COUNT(*) > 1)'
        INTO v_collisions;

    IF v_collisions > 0 THEN
        RAISE_APPLICATION_ERROR(-20002,
            '002 REFUSED: ' || v_collisions ||
            ' group(s) of rows in EVENTSTORESNAPSHOTS share (AGGREGATEID, AGGREGATETYPE, TENANTID), so the plain unique index cannot be created. This is the conflict step 0 asks you to look for: a table holding BOTH a literal ''__untenanted__'' snapshot AND a NULL one for the same aggregate had two distinct entries under the old NVL index, and step 1 has now collapsed them onto one key. The existing index has NOT been dropped -- had it been, the failed CREATE would have left the snapshot store with no uniqueness at all, and a load would return whichever of the duplicate snapshots the plan reached first. Find them with: SELECT AGGREGATEID, AGGREGATETYPE, TENANTID, COUNT(*) FROM EVENTSTORESNAPSHOTS GROUP BY AGGREGATEID, AGGREGATETYPE, TENANTID HAVING COUNT(*) > 1; only an operator can decide which snapshot survives. Delete the losers, then re-run.');
    END IF;

    EXECUTE IMMEDIATE 'DROP INDEX UQ_EVENTSTORESNAPSHOTS_AGG';
    EXECUTE IMMEDIATE
        'CREATE UNIQUE INDEX UQ_EVENTSTORESNAPSHOTS_AGG
             ON EVENTSTORESNAPSHOTS (AGGREGATEID, AGGREGATETYPE, TENANTID)';

    DBMS_OUTPUT.PUT_LINE('002: UQ_EVENTSTORESNAPSHOTS_AGG replaced with the plain triple index.');
END;
/

-- ---------------------------------------------------------------------------------------
-- STEP 4 — Verify. Expected result: NO ROWS.
-- ---------------------------------------------------------------------------------------

SELECT COUNT(*) AS REMAINING_NULL_TENANTS
  FROM EVENTSTORESNAPSHOTS
 WHERE TENANTID IS NULL
HAVING COUNT(*) > 0;
