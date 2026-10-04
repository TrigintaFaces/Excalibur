-- Fresh disposable PostgreSQL with wal_level=logical. Follow with writer, crash, verify.
\set ON_ERROR_STOP on
SELECT version();
SHOW fsync;
SHOW synchronous_commit;
CREATE TABLE crash_ledger(id integer PRIMARY KEY, payload text NOT NULL);
SELECT * FROM pg_create_logical_replication_slot('crash_spike', 'test_decoding');
CHECKPOINT;
INSERT INTO crash_ledger VALUES(1,'committed-before-crash');
CREATE TEMP TABLE consumed AS
    SELECT * FROM pg_logical_slot_get_changes('crash_spike',NULL,NULL) WITH ORDINALITY;
TABLE consumed;
DO $$BEGIN
    IF (SELECT count(*) FROM consumed WHERE data LIKE 'table public.crash_ledger: INSERT:%') <> 1
       OR NOT EXISTS(SELECT 1 FROM consumed WHERE data LIKE '%id[integer]:1 %') THEN
        RAISE EXCEPTION 'Pre-crash event was not consumed';
    END IF;
END$$;
SELECT slot_name,restart_lsn,confirmed_flush_lsn FROM pg_replication_slots WHERE slot_name='crash_spike';
\echo PASS durable event 1 consumed before crash; slot progress may replay after crash
