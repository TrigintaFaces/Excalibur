-- Oracle MIGRATION for Excalibur.EventSourcing.Oracle — EVENT STORE TENANT TOTALITY
-- Version: 1.0
--
-- Converges EVENTSTOREEVENTS.TENANTID onto a TOTAL representation: NOT NULL, defaulting to the
-- reserved '__untenanted__' sentinel. After this script there is exactly ONE way to say "this
-- event has no tenant", and it is a value rather than the absence of one.
--
-- This is the event-store counterpart of 002, which did the same thing for the snapshot store.
--
-- BEFORE: TENANTID is nullable. An event appended before tenancy holds NULL, an event appended
--         since holds '__untenanted__', and the read path folds the two together with
--         COALESCE(TENANTID, '__untenanted__'). Two spellings, one meaning.
-- AFTER:  TENANTID is NOT NULL and every untenanted row holds the sentinel. The COALESCE still
--         runs and is now a no-op, so no query changes and nothing breaks.
--
-- Run 003_CreateEventStoreSchema.sql for NEW deployments; it already produces this shape and
-- this script is then a no-op. This script is only for a schema created by an earlier revision
-- that still holds NULL tenants.
--
--
-- THIS MIGRATION ALSO CLOSES A LIVE CONCURRENCY HOLE. READ THIS PART.
-- -------------------------------------------------------------------
-- UQ_EVENTSTOREEVENTS_STREAM is a plain UNIQUE over
-- (AGGREGATEID, AGGREGATETYPE, VERSION, TENANTID), and it is what makes optimistic concurrency
-- per-tenant rather than global.
--
-- Oracle treats NULLs as DISTINCT in a unique index. While TENANTID is nullable, that constraint
-- therefore does not constrain UNTENANTED rows at all: two appends at the same version of the
-- same untenanted stream BOTH succeed, and the conflict the event store exists to detect is not
-- detected. Tenanted streams were never affected -- they carry a non-null term, so the
-- constraint applies to them normally.
--
-- This is the identical defect the snapshot store hit, which is why 002 describes a
-- function-based index over NVL(TENANTID, CHR(1)) as its workaround. The event store never had
-- that workaround. Making the column total removes the need for one: with no NULLs there is
-- nothing for Oracle to treat as distinct, and the plain UNIQUE constrains every row by one rule.
--
-- CONSEQUENCE FOR STEP 0, and it is not hypothetical: because duplicates were ADMITTED while the
-- column was nullable, an existing database may already hold them. Step 0 is therefore a
-- correctness gate, not a formality -- unlike the snapshot case, where the NVL index had been
-- preventing duplicates all along.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run during a maintenance window with the store stopped. Take a backup you have restored at
-- least once — this touches the system of record. Step 1 rewrites every row whose TENANTID is
-- NULL and is the dominant cost; it is a single set-based UPDATE and is not resumable.

-- ---------------------------------------------------------------------------------------
-- STEP 0 — PRE-FLIGHT. RUN THIS FIRST AND READ THE RESULT. DO NOT SKIP.
-- ---------------------------------------------------------------------------------------
--
-- Two DIFFERENT collisions can exist, and they need one query each.
--
-- 0a — a stream version holding BOTH a NULL row and a literal sentinel row. Those are two
--      distinct index entries today and become one after the rewrite.
--
-- 0b — a stream version holding MORE THAN ONE NULL row. Oracle admitted these because it treats
--      NULLs as distinct (see the header). They are pre-existing duplicate appends, and the
--      unique constraint will reject them the moment the column is total.
--
-- Query 0 below covers both at once: it groups every untenanted row, by either spelling, and
-- reports any version with more than one.
--
-- Expected result: NO ROWS. Any row returned is a genuine data conflict — two rows each claiming
-- to be the same version of the untenanted stream. Only an operator can decide which append
-- survives; that is a data question and this script has no authority over it. Resolve them, then
-- re-run. Do not proceed on a non-empty result: step 3 would fail partway.

SELECT AGGREGATEID,
       AGGREGATETYPE,
       VERSION,
       COUNT(*) AS COLLIDING_ROWS
  FROM EVENTSTOREEVENTS
 WHERE TENANTID IS NULL
    OR TENANTID = '__untenanted__'
 GROUP BY AGGREGATEID, AGGREGATETYPE, VERSION
HAVING COUNT(*) > 1;

-- ---------------------------------------------------------------------------------------
-- STEP 1 — Rewrite the NULL tenant onto the sentinel.
-- ---------------------------------------------------------------------------------------
-- Only rows that are genuinely untenanted are touched. A row carrying a real tenant is not
-- matched by this predicate and is left exactly as it is.

UPDATE EVENTSTOREEVENTS
   SET TENANTID = '__untenanted__'
 WHERE TENANTID IS NULL;

COMMIT;

-- ---------------------------------------------------------------------------------------
-- STEP 2 — Apply the default.
-- ---------------------------------------------------------------------------------------
-- Applied BEFORE the NOT NULL so the two ALTERs are independent and this script stays
-- re-runnable: MODIFY DEFAULT is idempotent, and a database that already reached NOT NULL by
-- another route still picks the default up here.
--
-- In Oracle a DEFAULT applies only when the column is OMITTED from the INSERT. It is a backstop
-- for hand-written INSERTs; the store itself always binds the term explicitly, so the store never
-- relies on it.

ALTER TABLE EVENTSTOREEVENTS MODIFY (TENANTID DEFAULT '__untenanted__');

-- ---------------------------------------------------------------------------------------
-- STEP 3 — Close the column.
-- ---------------------------------------------------------------------------------------
-- This ALTER fails if step 1 did not run or did not commit. That is the intended behaviour: it
-- is the database refusing to let the schema claim a guarantee the data does not meet.
--
-- Re-running it against an already-NOT NULL column is a no-op in Oracle rather than an error,
-- which is what keeps this script safe to run twice.

ALTER TABLE EVENTSTOREEVENTS MODIFY (TENANTID VARCHAR2(64) NOT NULL);

-- ---------------------------------------------------------------------------------------
-- STEP 4 — Verify. Expected result for BOTH queries: NO ROWS.
-- ---------------------------------------------------------------------------------------
-- The first proves the data is total. The second re-runs the step 0 collision check now that
-- the constraint is in force; it can only return rows if step 3 somehow did not apply, and it is
-- cheap insurance against reading a half-applied migration as a successful one.

SELECT COUNT(*) AS REMAINING_NULL_TENANTS
  FROM EVENTSTOREEVENTS
 WHERE TENANTID IS NULL
HAVING COUNT(*) > 0;

SELECT AGGREGATEID,
       AGGREGATETYPE,
       VERSION,
       COUNT(*) AS COLLIDING_ROWS
  FROM EVENTSTOREEVENTS
 WHERE TENANTID = '__untenanted__'
 GROUP BY AGGREGATEID, AGGREGATETYPE, VERSION
HAVING COUNT(*) > 1;
