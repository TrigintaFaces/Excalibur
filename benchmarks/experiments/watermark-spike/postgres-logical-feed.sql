-- Research only: fresh disposable PostgreSQL database with wal_level=logical.
-- test_decoding verifies the engine primitive; it is not the proposed shipping adapter.
\set ON_ERROR_STOP on
SELECT version();
SHOW wal_level;
SHOW synchronous_commit;
SHOW fsync;
CREATE EXTENSION dblink;
CREATE SEQUENCE feed_position CACHE 1;
CREATE TABLE ledger(id integer PRIMARY KEY, position bigint NOT NULL, event_name text NOT NULL);
SELECT dblink_connect('first', 'dbname=postgres user=spike');
SELECT dblink_connect('second', 'dbname=postgres user=spike');
SELECT * FROM pg_create_logical_replication_slot('watermark_spike', 'test_decoding');

SELECT dblink_exec('first', 'BEGIN');
SELECT dblink_exec('first', $$INSERT INTO ledger VALUES(1,nextval('feed_position'),'delayed-first')$$);
SELECT dblink_exec('second', $$INSERT INTO ledger VALUES(2,nextval('feed_position'),'committed-second')$$);
CREATE TEMP TABLE initial_peek AS
    SELECT * FROM pg_logical_slot_peek_changes('watermark_spike', NULL, NULL) WITH ORDINALITY;
TABLE initial_peek;
DO $$BEGIN
    IF (SELECT count(*) FROM initial_peek WHERE data LIKE 'table public.ledger: INSERT:%') <> 1
       OR NOT EXISTS(SELECT 1 FROM initial_peek WHERE data LIKE '%id[integer]:2 %')
       OR EXISTS(SELECT 1 FROM initial_peek WHERE data LIKE '%id[integer]:1 %') THEN
        RAISE EXCEPTION 'Initial peek must expose only committed transaction 2';
    END IF;
END$$;
\echo PASS uncommitted first allocation is withheld while committed transaction 2 is decoded

SELECT dblink_exec('first', 'COMMIT');
CREATE TEMP TABLE committed_feed AS
    SELECT * FROM pg_logical_slot_get_changes('watermark_spike', NULL, NULL) WITH ORDINALITY;
TABLE committed_feed;
DO $$DECLARE ids integer[]; BEGIN
    SELECT array_agg(CASE WHEN data LIKE '%id[integer]:1 %' THEN 1
                         WHEN data LIKE '%id[integer]:2 %' THEN 2 ELSE -1 END ORDER BY ordinality)
    INTO ids FROM committed_feed WHERE data LIKE 'table public.ledger: INSERT:%';
    IF ids IS DISTINCT FROM ARRAY[2,1] THEN
        RAISE EXCEPTION 'Expected commit order [2,1], received %', ids;
    END IF;
    IF (SELECT array_agg(id ORDER BY position) FROM ledger) IS DISTINCT FROM ARRAY[1,2] THEN
        RAISE EXCEPTION 'Allocation order control did not differ from commit order';
    END IF;
END$$;
\echo PASS decoded commit order is 2,1 while sequence allocation order is 1,2

SELECT dblink_exec('first', 'BEGIN');
SELECT dblink_exec('first', $$INSERT INTO ledger VALUES(3,nextval('feed_position'),'rolled-back')$$);
SELECT dblink_exec('first', 'ROLLBACK');
SELECT dblink_exec('second', $$INSERT INTO ledger VALUES(4,nextval('feed_position'),'after-rollback')$$);
CREATE TEMP TABLE after_rollback AS
    SELECT * FROM pg_logical_slot_get_changes('watermark_spike', NULL, NULL) WITH ORDINALITY;
TABLE after_rollback;
DO $$BEGIN
    IF (SELECT count(*) FROM after_rollback WHERE data LIKE 'table public.ledger: INSERT:%') <> 1
       OR NOT EXISTS(SELECT 1 FROM after_rollback WHERE data LIKE '%id[integer]:4 %')
       OR EXISTS(SELECT 1 FROM after_rollback WHERE data LIKE '%id[integer]:3 %') THEN
        RAISE EXCEPTION 'Rolled-back transaction leaked or later committed event missing';
    END IF;
END$$;
\echo PASS rolled-back event omitted and later committed event delivered despite sequence gap

DO $$BEGIN
    IF EXISTS(SELECT 1 FROM pg_logical_slot_get_changes('watermark_spike', NULL, NULL)
              WHERE data LIKE 'table public.ledger: INSERT:%') THEN
        RAISE EXCEPTION 'Acknowledged event repeated without restart';
    END IF;
END$$;
\echo PASS consumed changes are absent on next healthy-session read; no crash deduplication claim
SELECT dblink_disconnect('first');
SELECT dblink_disconnect('second');
SELECT pg_drop_replication_slot('watermark_spike');
\echo ALL FOUR LOGICAL FEED CASES PASSED; no adapter, failover or performance qualification
