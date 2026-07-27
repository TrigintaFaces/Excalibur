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

ALTER TABLE EVENTSTORESNAPSHOTS MODIFY (TENANTID VARCHAR2(255) NOT NULL);

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

DROP INDEX UQ_EVENTSTORESNAPSHOTS_AGG;

CREATE UNIQUE INDEX UQ_EVENTSTORESNAPSHOTS_AGG
    ON EVENTSTORESNAPSHOTS (AGGREGATEID, AGGREGATETYPE, TENANTID);

-- ---------------------------------------------------------------------------------------
-- STEP 4 — Verify. Expected result: NO ROWS.
-- ---------------------------------------------------------------------------------------

SELECT COUNT(*) AS REMAINING_NULL_TENANTS
  FROM EVENTSTORESNAPSHOTS
 WHERE TENANTID IS NULL
HAVING COUNT(*) > 0;
