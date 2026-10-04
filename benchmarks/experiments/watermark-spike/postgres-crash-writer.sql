\set ON_ERROR_STOP on
BEGIN;
INSERT INTO crash_ledger VALUES(2,'must-rollback-on-crash');
-- Controller verifies this session is active in PgSleep, proving INSERT has returned.
SELECT pg_sleep(600);
COMMIT;
