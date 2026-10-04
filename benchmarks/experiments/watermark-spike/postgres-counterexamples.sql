-- Research only. Run in a fresh disposable PostgreSQL database, never a deployed store.
-- Each assertion proves a counterexample is reachable, not that a replacement is safe.
\set ON_ERROR_STOP on
SELECT version();
SHOW synchronous_commit;
SHOW fsync;
CREATE EXTENSION dblink;
CREATE SCHEMA watermark_spike;
CREATE SEQUENCE watermark_spike.position CACHE 1;
CREATE TABLE watermark_spike.events (position bigint PRIMARY KEY, event_name text NOT NULL);
SELECT dblink_connect('writer_a', 'dbname=postgres user=spike');
SELECT dblink_connect('writer_b', 'dbname=postgres user=spike');
SELECT dblink_connect('reader', 'dbname=postgres user=spike');

-- A reserves/inserts 1 but does not commit. B commits 2.
SELECT dblink_exec('writer_a', 'BEGIN');
SELECT dblink_exec('writer_a', $$INSERT INTO watermark_spike.events VALUES
    (nextval('watermark_spike.position'), 'delayed-1')$$);
SELECT dblink_exec('writer_b', $$INSERT INTO watermark_spike.events VALUES
    (nextval('watermark_spike.position'), 'early-2')$$);
DO $$BEGIN
    IF (SELECT array_agg(position ORDER BY position) FROM watermark_spike.events)
       IS DISTINCT FROM ARRAY[2::bigint] THEN RAISE EXCEPTION 'Expected only committed position 2'; END IF;
END$$;
\echo TRACE committed-head=2 while position=1 remains uncommitted

-- Fix the reader snapshot before A commits; it sees only 2.
SELECT dblink_exec('reader', 'BEGIN ISOLATION LEVEL REPEATABLE READ');
CREATE TEMP TABLE old_view AS SELECT * FROM dblink('reader',
    'SELECT position FROM watermark_spike.events ORDER BY position') AS t(position bigint);
SELECT dblink_exec('writer_a', 'COMMIT');
-- All through 2 are now committed: W=2 is valid. External publication is newer than reader view.
DO $$BEGIN
    IF (SELECT array_agg(position ORDER BY position) FROM watermark_spike.events)
       IS DISTINCT FROM ARRAY[1::bigint,2::bigint] THEN RAISE EXCEPTION 'Resolved prefix not committed'; END IF;
    IF (SELECT array_agg(position ORDER BY position) FROM dblink('reader',
        'SELECT position FROM watermark_spike.events WHERE position <= 2 ORDER BY position') AS t(position bigint))
       IS DISTINCT FROM ARRAY[2::bigint] THEN RAISE EXCEPTION 'Stale snapshot counterexample not reproduced'; END IF;
END$$;
\echo PASS stale-snapshot: valid W=2 plus older read view omits committed 1
SELECT dblink_exec('reader', 'COMMIT');
DO $$BEGIN
    IF (SELECT count(*) FROM dblink('reader',
        'SELECT position FROM watermark_spike.events WHERE position <= 2') AS t(position bigint)) <> 2
    THEN RAISE EXCEPTION 'Fresh-view control failed'; END IF;
END$$;
\echo PASS fresh-view control sees both resolved events

-- A rollback burns position 3; next successful append uses 4.
SELECT dblink_exec('writer_a', 'BEGIN');
SELECT dblink_exec('writer_a', $$INSERT INTO watermark_spike.events VALUES
    (nextval('watermark_spike.position'), 'aborted-3')$$);
SELECT dblink_exec('writer_a', 'ROLLBACK');
SELECT dblink_exec('writer_b', $$INSERT INTO watermark_spike.events VALUES
    (nextval('watermark_spike.position'), 'committed-4')$$);
DO $$BEGIN
    IF (SELECT array_agg(position ORDER BY position) FROM watermark_spike.events)
       IS DISTINCT FROM ARRAY[1::bigint,2::bigint,4::bigint] THEN RAISE EXCEPTION 'Rollback gap not reproduced'; END IF;
END$$;
\echo PASS rollback: committed positions are 1,2,4 with permanent gap 3

-- Cached issuance violates positional causality even if nextval is called after the causal read.
CREATE SEQUENCE watermark_spike.cached_position CACHE 5;
CREATE TABLE watermark_spike.cached_events (position bigint PRIMARY KEY, event_name text NOT NULL);
SELECT dblink_exec('writer_b', $$INSERT INTO watermark_spike.cached_events VALUES
    (nextval('watermark_spike.cached_position'), 'B-seed')$$);
SELECT dblink_exec('writer_a', $$INSERT INTO watermark_spike.cached_events VALUES
    (nextval('watermark_spike.cached_position'), 'A-cause')$$);
-- In B's own session, make the causal observation part of the insertion statement.
SELECT dblink_exec('writer_b', $$INSERT INTO watermark_spike.cached_events
    SELECT nextval('watermark_spike.cached_position'), 'B-effect'
    FROM watermark_spike.cached_events WHERE event_name = 'A-cause'$$);
DO $$DECLARE cause bigint; effect bigint; BEGIN
    SELECT position INTO STRICT cause FROM watermark_spike.cached_events WHERE event_name='A-cause';
    SELECT position INTO STRICT effect FROM watermark_spike.cached_events WHERE event_name='B-effect';
    IF cause <> 6 OR effect <> 2 OR effect >= cause THEN
        RAISE EXCEPTION 'Cached causal inversion not reproduced: cause %, effect %', cause, effect;
    END IF;
END$$;
TABLE watermark_spike.cached_events;
\echo PASS cached-causality: B observes cause at 6 then inserts dependent effect at 2
SELECT dblink_disconnect('writer_a');
SELECT dblink_disconnect('writer_b');
SELECT dblink_disconnect('reader');
\echo ALL FOUR ASSERTED CASES PASSED: three counterexamples and fresh-view control; no provider qualification
