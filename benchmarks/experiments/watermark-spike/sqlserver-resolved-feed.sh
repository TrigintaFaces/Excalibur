#!/usr/bin/env bash
set -euo pipefail
export SQLCMDPASSWORD="${MSSQL_SA_PASSWORD:?Disposable password required}"
sql() { /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U sa -C -b -r1 -t 60 "$@"; }
sql -i /experiment/sqlserver-resolved-feed.sql
sql -d WatermarkFeedSpike -Q 'CREATE TABLE EqualityControl(Position binary(8) NOT NULL);'
sql -H watermark-feed-held-writer -d WatermarkFeedSpike > /tmp/feed-writer.log 2>&1 <<'SQL' &
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
INSERT EventPayload(EventId,Payload) VALUES(7,7);
INSERT Ledger(EventId) VALUES(7);
INSERT Outbox VALUES(7);
DECLARE @deadline datetime2=DATEADD(second,45,SYSUTCDATETIME());
WHILE (SELECT Released FROM Gate WHERE Id=1)=0
BEGIN
    IF SYSUTCDATETIME()>@deadline THROW 51000, 'Held writer release deadline exceeded', 1;
    WAITFOR DELAY '00:00:00.100';
END;
COMMIT;
PRINT 'Held writer committed';
SQL
writer_pid=$!
ready=false
for attempt in $(seq 1 30); do
    if sql -d WatermarkFeedSpike -Q "IF NOT EXISTS(SELECT 1 FROM Ledger WITH(READUNCOMMITTED) WHERE EventId=7) THROW 51000, 'Writer not at barrier', 1;" > /tmp/feed-ready.log 2>&1; then
        ready=true
        break
    fi
    sleep 0.2
done
if [ "$ready" != true ]; then
    cat /tmp/feed-ready.log
    wait "$writer_pid" || cat /tmp/feed-writer.log
    exit 1
fi
sql -d WatermarkFeedSpike <<'SQL'
SET NOCOUNT ON;
DECLARE @epoch uniqueidentifier=(SELECT Epoch FROM SourceIdentity);
DECLARE @pending binary(8)=(SELECT Position FROM Ledger WITH(READUNCOMMITTED) WHERE EventId=7);
EXEC Publish @epoch;
IF (SELECT ExclusiveBound FROM Publication)<>@pending THROW 51000, 'Held writer did not establish exact boundary', 1;
EXEC ApplyPage 1,@epoch,10;
IF EXISTS(SELECT 1 FROM Applied WHERE EventId=7) THROW 51000, 'Uncommitted event was applied', 1;
IF NOT EXISTS(SELECT 1 FROM Subscription WHERE Id=1 AND CursorPosition=@pending AND IncludeCursor=1)
    THROW 51000, 'Exhaustion lost inclusive boundary', 1;
INSERT EqualityControl VALUES(@pending);
UPDATE Gate SET Released=1 WHERE Id=1;
PRINT 'PASS: reader exhausted exactly to the held writer position without applying it';
SQL
wait "$writer_pid"
cat /tmp/feed-writer.log
sql -d WatermarkFeedSpike <<'SQL'
SET NOCOUNT ON;
DECLARE @epoch uniqueidentifier=(SELECT Epoch FROM SourceIdentity);
IF (SELECT Position FROM Ledger WHERE EventId=7)<>(SELECT Position FROM EqualityControl)
    THROW 51000, 'Writer position changed after boundary capture', 1;
EXEC Publish @epoch;
EXEC ApplyPage 1,@epoch,10;
IF NOT EXISTS(SELECT 1 FROM Applied WHERE SubscriptionId=1 AND EventId=7 AND IsReplay=0)
    THROW 51000, 'Exact-boundary event silently skipped', 1;
IF (SELECT ProjectionValue FROM Subscription WHERE Id=1)<>13457
    THROW 51000, 'Exact-boundary event applied incorrectly', 1;
PRINT 'PASS: exact-boundary event is applied after commit and publication';
SQL
wait_for() {
    local query="$1"
    for attempt in $(seq 1 40); do
        if sql -d WatermarkFeedSpike -Q "$query" > /tmp/feed-barrier.log 2>&1; then return; fi
        sleep 0.2
    done
    cat /tmp/feed-barrier.log
    return 1
}

# Force both consumers to contend before either can read the checkpoint.
sql -d WatermarkFeedSpike -Q 'DECLARE @e uniqueidentifier=(SELECT Epoch FROM SourceIdentity); EXEC StartSubscription 3,@e;'
sql -H feed-checkpoint-holder -d WatermarkFeedSpike > /tmp/feed-holder.log 2>&1 <<'SQL' &
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
SELECT CursorPosition FROM Subscription WITH(UPDLOCK,HOLDLOCK) WHERE Id=3;
DECLARE @deadline datetime2=DATEADD(second,45,SYSUTCDATETIME());
WHILE (SELECT Released FROM Gate WHERE Id=2)=0
BEGIN
    IF SYSUTCDATETIME()>@deadline THROW 51000, 'Checkpoint holder deadline exceeded', 1;
    WAITFOR DELAY '00:00:00.100';
END;
COMMIT;
SQL
holder_pid=$!
wait_for "IF NOT EXISTS(SELECT 1 FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id WHERE s.host_name='feed-checkpoint-holder' AND r.wait_type='WAITFOR' AND r.open_transaction_count>0) THROW 51000, 'Checkpoint holder not ready', 1;"
sql -H feed-reader-a -d WatermarkFeedSpike -Q 'DECLARE @e uniqueidentifier=(SELECT Epoch FROM SourceIdentity); EXEC ApplyPage 3,@e,100;' > /tmp/feed-a.log 2>&1 &
reader_a=$!
sql -H feed-reader-b -d WatermarkFeedSpike -Q 'DECLARE @e uniqueidentifier=(SELECT Epoch FROM SourceIdentity); EXEC ApplyPage 3,@e,100;' > /tmp/feed-b.log 2>&1 &
reader_b=$!
wait_for "IF (SELECT COUNT(*) FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id WHERE s.host_name IN('feed-reader-a','feed-reader-b') AND r.blocking_session_id>0 AND r.wait_type LIKE 'LCK_M_%')<>2 THROW 51000, 'Both readers must contend on checkpoint', 1;"
sql -d WatermarkFeedSpike -Q "SELECT s.host_name,r.blocking_session_id,r.wait_type,r.wait_resource FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id WHERE s.host_name IN('feed-reader-a','feed-reader-b','feed-checkpoint-holder'); UPDATE Gate SET Released=1 WHERE Id=2;"
wait "$holder_pid"
wait "$reader_a"
wait "$reader_b"
cat /tmp/feed-holder.log /tmp/feed-a.log /tmp/feed-b.log
sql -d WatermarkFeedSpike -Q "IF (SELECT COUNT(*) FROM Applied WHERE SubscriptionId=3)<>5 OR (SELECT ProjectionValue FROM Subscription WHERE Id=3)<>1457 THROW 51000, 'Concurrent readers duplicated or skipped effects', 1; PRINT 'PASS: both contending consumers succeed with exact effects';"

# Pause the actual publisher between capturing its boundary and the guarded publication update.
sql -H feed-old-publisher -d WatermarkFeedSpike -Q 'DECLARE @e uniqueidentifier=(SELECT Epoch FROM SourceIdentity); EXEC Publish @e,3;' > /tmp/feed-publisher.log 2>&1 &
publisher_pid=$!
wait_for "IF NOT EXISTS(SELECT 1 FROM Gate WHERE Id=3 AND ObservedBound IS NOT NULL) THROW 51000, 'Publisher capture not reached', 1;"
sql -d WatermarkFeedSpike <<'SQL'
DECLARE @epoch uniqueidentifier=(SELECT Epoch FROM SourceIdentity);
-- An observable dependency: this append is constructed only after reading committed event 7.
DECLARE @derived int=(SELECT Payload+1 FROM EventPayload WHERE EventId=7);
IF @derived<>8 THROW 51000, 'Causal predecessor not observed', 1;
EXEC AppendEvent 8,@derived;
EXEC Publish @epoch;
IF (SELECT ExclusiveBound FROM Publication)<=(SELECT ObservedBound FROM Gate WHERE Id=3)
    THROW 51000, 'New publisher did not establish a higher boundary', 1;
UPDATE Gate SET Released=1 WHERE Id=3;
SQL
wait "$publisher_pid"
cat /tmp/feed-publisher.log
sql -d WatermarkFeedSpike -Q "IF (SELECT ExclusiveBound FROM Publication)<=(SELECT ObservedBound FROM Gate WHERE Id=3) THROW 51000, 'Delayed publisher regressed boundary', 1; PRINT 'PASS: delayed publisher cannot rewind publication';"

# The reader must finish only its captured interval even when publication advances before its scan.
sql -d WatermarkFeedSpike -Q 'DECLARE @e uniqueidentifier=(SELECT Epoch FROM SourceIdentity); EXEC StartSubscription 4,@e;'
sql -H feed-held-reader -d WatermarkFeedSpike -Q 'DECLARE @e uniqueidentifier=(SELECT Epoch FROM SourceIdentity); EXEC ApplyPage 4,@e,100,0,4;' > /tmp/feed-held-reader.log 2>&1 &
held_reader=$!
# Dirty read is a test readiness barrier only, never the candidate feed's visibility mechanism.
wait_for "IF NOT EXISTS(SELECT 1 FROM Gate WITH(READUNCOMMITTED) WHERE Id=4 AND ObservedBound IS NOT NULL) THROW 51000, 'Reader capture not reached', 1;"
sql -d WatermarkFeedSpike -Q 'DECLARE @e uniqueidentifier=(SELECT Epoch FROM SourceIdentity); EXEC AppendEvent 9,9; EXEC Publish @e; UPDATE Gate SET Released=1 WHERE Id=104;'
wait "$held_reader"
cat /tmp/feed-held-reader.log
sql -d WatermarkFeedSpike <<'SQL'
IF EXISTS(SELECT 1 FROM Applied WHERE SubscriptionId=4 AND EventId=9)
    THROW 51000, 'Reader exceeded captured boundary', 1;
IF NOT EXISTS(SELECT 1 FROM Subscription s JOIN Gate g ON g.Id=4 WHERE s.Id=4 AND s.CursorPosition=g.ObservedBound AND s.IncludeCursor=1)
    THROW 51000, 'Reader checkpoint exceeded captured boundary', 1;
IF (SELECT ProjectionValue FROM Subscription WHERE Id=4)<>14578
    THROW 51000, 'Causal application order incorrect', 1;
DECLARE @epoch uniqueidentifier=(SELECT Epoch FROM SourceIdentity);
EXEC ApplyPage 4,@epoch,100;
IF (SELECT ProjectionValue FROM Subscription WHERE Id=4)<>145789
    THROW 51000, 'Next interval lost the newly committed event', 1;
PRINT 'PASS: reader captures one stable interval and later applies its successor';
SQL
sql -d WatermarkFeedSpike -Q 'CREATE TABLE Unrelated(Id int PRIMARY KEY, Version rowversion); INSERT Gate(Id,Released) VALUES(5,0);'
sql -H feed-unrelated-holder -d WatermarkFeedSpike > /tmp/feed-unrelated.log 2>&1 <<'SQL' &
SET NOCOUNT ON;
BEGIN TRAN;
INSERT Unrelated(Id) VALUES(1);
DECLARE @deadline datetime2=DATEADD(second,45,SYSUTCDATETIME());
WHILE (SELECT Released FROM Gate WHERE Id=5)=0
BEGIN
    IF SYSUTCDATETIME()>@deadline THROW 51000, 'Unrelated writer deadline exceeded', 1;
    WAITFOR DELAY '00:00:00.100';
END;
ROLLBACK;
SQL
unrelated_pid=$!
wait_for "IF NOT EXISTS(SELECT 1 FROM Unrelated WITH(READUNCOMMITTED) WHERE Id=1) THROW 51000, 'Unrelated writer not ready', 1;"
sql -d WatermarkFeedSpike <<'SQL'
DECLARE @epoch uniqueidentifier=(SELECT Epoch FROM SourceIdentity);
EXEC AppendEvent 10,0;
EXEC Publish @epoch;
IF (SELECT ExclusiveBound FROM Publication)<>(SELECT Version FROM Unrelated WITH(READUNCOMMITTED) WHERE Id=1)
    THROW 51000, 'Unrelated rowversion did not hold database-wide boundary', 1;
EXEC ApplyPage 4,@epoch,100;
IF EXISTS(SELECT 1 FROM Applied WHERE SubscriptionId=4 AND EventId=10)
    THROW 51000, 'Reader crossed unrelated active rowversion', 1;
UPDATE Gate SET Released=1 WHERE Id=5;
PRINT 'PASS: unrelated rowversion transaction demonstrably delays committed feed visibility';
SQL
wait "$unrelated_pid"
cat /tmp/feed-unrelated.log
sql -d WatermarkFeedSpike <<'SQL'
DECLARE @epoch uniqueidentifier=(SELECT Epoch FROM SourceIdentity);
EXEC Publish @epoch;
EXEC ApplyPage 4,@epoch,100;
IF (SELECT ProjectionValue FROM Subscription WHERE Id=4)<>1457890
    THROW 51000, 'Feed failed to recover after unrelated transaction resolved', 1;
SELECT s.Epoch,s.StartHead,s.CursorPosition,s.IncludeCursor,s.ProjectionValue,p.ExclusiveBound
INTO RecoveryExpected FROM Subscription s CROSS JOIN Publication p WHERE s.Id=4;
PRINT 'PASS: feed catches up after unrelated transaction resolution';
SQL
echo 'Resolved-feed assertions passed; no complete framework or performance qualification.'
