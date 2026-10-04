\set ON_ERROR_STOP on
SELECT version();
DO $$BEGIN
    IF (SELECT array_agg(id ORDER BY id) FROM crash_ledger) IS DISTINCT FROM ARRAY[1] THEN
        RAISE EXCEPTION 'Committed data lost or in-flight event survived recovery';
    END IF;
    IF (SELECT count(*) FROM pg_replication_slots WHERE slot_name='crash_spike') <> 1 THEN
        RAISE EXCEPTION 'Logical slot missing after crash recovery';
    END IF;
END$$;
\echo PASS committed event survived and in-flight event rolled back
INSERT INTO crash_ledger VALUES(3,'committed-after-recovery');
CREATE TEMP TABLE recovered_feed AS
    SELECT * FROM pg_logical_slot_get_changes('crash_spike',NULL,NULL) WITH ORDINALITY;
TABLE recovered_feed;
DO $$BEGIN
    IF (SELECT count(*) FROM recovered_feed WHERE data LIKE 'table public.crash_ledger: INSERT:%id[integer]:3 %') <> 1 THEN
        RAISE EXCEPTION 'New committed event missing or duplicated within recovered read';
    END IF;
    IF EXISTS(SELECT 1 FROM recovered_feed WHERE data LIKE 'table public.crash_ledger: INSERT:%'
              AND data NOT LIKE '%id[integer]:1 %' AND data NOT LIKE '%id[integer]:3 %') THEN
        RAISE EXCEPTION 'Unexpected or rolled-back event decoded after recovery';
    END IF;
END$$;
SELECT count(*) AS replayed_precrash_event_1 FROM recovered_feed
    WHERE data LIKE 'table public.crash_ledger: INSERT:%id[integer]:1 %';
\echo PASS post-recovery event decoded; any pre-crash replay is reported, not suppressed
SELECT pg_drop_replication_slot('crash_spike');
