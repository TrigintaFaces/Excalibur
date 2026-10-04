#!/usr/bin/env bash
# Research only: fresh disposable SQL Server; no production credentials or data.
set -euo pipefail
mode=${1:-commit}
case "$mode" in commit|kill) ;; *) echo 'Expected commit or kill mode' >&2; exit 2;; esac
echo "Resolution mode: $mode"
export SQLCMDPASSWORD="${MSSQL_SA_PASSWORD:?Disposable container password required}"
sqlcmd=/opt/mssql-tools18/bin/sqlcmd
sql() { "$sqlcmd" -S 127.0.0.1 -U sa -C -b -r1 -t 60 "$@"; }
sql -d master -Q "CREATE DATABASE WatermarkSpike; ALTER DATABASE WatermarkSpike SET READ_COMMITTED_SNAPSHOT ON;"
sql -d WatermarkSpike <<'SQL'
SET NOCOUNT ON;
SELECT @@VERSION AS EngineVersion;
SELECT name, is_read_committed_snapshot_on, delayed_durability_desc FROM sys.databases WHERE name=DB_NAME();
CREATE TABLE Ledger(Id int PRIMARY KEY, Payload int NOT NULL, Position rowversion NOT NULL);
CREATE TABLE Gate(Id int PRIMARY KEY, Released bit NOT NULL);
INSERT Gate VALUES(1,0);
SQL

# Writer waits for an explicit committed release signal, not a guessed timing window.
sql -H watermark-spike-held-writer -d WatermarkSpike > /tmp/watermark-writer.log 2>&1 <<'SQL' &
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
INSERT Ledger(Id,Payload) VALUES(1,10);
DECLARE @deadline datetime2 = DATEADD(second,45,SYSUTCDATETIME());
WHILE (SELECT Released FROM Gate WHERE Id=1)=0
BEGIN
    IF SYSUTCDATETIME() > @deadline THROW 51000, 'Writer release deadline exceeded', 1;
    WAITFOR DELAY '00:00:00.100';
END;
COMMIT;
PRINT 'Writer 1 committed after release';
SQL
writer_pid=$!

# READUNCOMMITTED is used only to establish that the injected writer reached its barrier.
# The candidate feed reads below use ordinary RCSI visibility, never dirty reads.
ready=false
for attempt in $(seq 1 30); do
    if sql -d WatermarkSpike -Q "IF NOT EXISTS(SELECT 1 FROM Ledger WITH(READUNCOMMITTED) WHERE Id=1) THROW 51000, 'Not at writer barrier yet', 1;" > /tmp/watermark-ready.log 2>&1; then
        ready=true
        break
    fi
    sleep 0.2
done
if [ "$ready" != true ]; then
    cat /tmp/watermark-ready.log
    wait "$writer_pid" || cat /tmp/watermark-writer.log
    exit 1
fi

sql -d WatermarkSpike <<'SQL'
SET NOCOUNT ON;
INSERT Ledger(Id,Payload) VALUES(2,20);
DECLARE @pending binary(8)=(SELECT Position FROM Ledger WITH(READUNCOMMITTED) WHERE Id=1);
DECLARE @bound binary(8)=MIN_ACTIVE_ROWVERSION();
DECLARE @head binary(8)=@@DBTS;
SELECT @pending AS PendingPosition, @bound AS MinimumActive, @head AS Dbts;
IF @bound <> @pending THROW 51000, 'Minimum active did not protect pending writer', 1;
IF (SELECT COUNT(*) FROM Ledger WHERE Position < @bound) <> 0
    THROW 51000, 'Stable-bound control unexpectedly passed the unresolved writer', 1;
IF (SELECT COUNT(*) FROM Ledger WHERE Position <= @head) <> 1
    THROW 51000, 'Expected DBTS-bounded read to return only later committed event', 1;
IF NOT EXISTS(SELECT 1 FROM Ledger WHERE Id=2 AND Position <= @head)
    THROW 51000, 'Later event not visible in counterexample', 1;
PRINT 'PASS: DBTS exposes later event while MIN_ACTIVE_ROWVERSION withholds the unresolved prefix';
SQL
expected_rows=2
if [ "$mode" = kill ]; then
    sql -d WatermarkSpike <<'SQL'
DECLARE @session int;
IF (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE host_name='watermark-spike-held-writer') <> 1
    THROW 51000, 'Expected exactly the experiment writer session', 1;
SELECT @session=session_id FROM sys.dm_exec_sessions WHERE host_name='watermark-spike-held-writer';
DECLARE @command nvarchar(100)=N'KILL '+CONVERT(nvarchar(12),@session);
EXEC(@command);
PRINT 'Experiment writer terminated by SQL Server';
SQL
    if wait "$writer_pid"; then
        echo 'Killed writer unexpectedly reported success' >&2
        exit 1
    fi
    expected_rows=1
else
    sql -d WatermarkSpike -Q 'UPDATE Gate SET Released=1 WHERE Id=1;'
    wait "$writer_pid"
fi
cat /tmp/watermark-writer.log

sql -d WatermarkSpike -v ExpectedRows="$expected_rows" <<'SQL'
SET NOCOUNT ON;
DECLARE @bound binary(8)=MIN_ACTIVE_ROWVERSION();
IF (SELECT COUNT(*) FROM Ledger WHERE Position < @bound) <> $(ExpectedRows)
    THROW 51000, 'Resolved-prefix control did not expose exactly the committed events', 1;
IF $(ExpectedRows)=1 AND EXISTS(SELECT 1 FROM Ledger WHERE Id=1)
    THROW 51000, 'Terminated writer event survived rollback', 1;
IF NOT EXISTS(SELECT 1 FROM Ledger WHERE Id=2 AND Position < @bound)
    THROW 51000, 'Boundary failed to advance past resolved gap', 1;
PRINT 'PASS: after writer resolution the exclusive stable boundary exposes exactly the committed events';
DECLARE @old binary(8)=(SELECT Position FROM Ledger WHERE Id=2);
UPDATE Ledger SET Payload=Payload WHERE Id=2;
IF EXISTS(SELECT 1 FROM Ledger WHERE Position=@old)
    THROW 51000, 'Rowversion unexpectedly remained immutable', 1;
IF (SELECT Position FROM Ledger WHERE Id=2) <= @old
    THROW 51000, 'Rowversion did not move forwards on update', 1;
PRINT 'PASS: even a no-op update moves rowversion; mutable event rows cannot directly supply immutable positions';
SELECT Id, Payload, Position FROM Ledger ORDER BY Position;
SQL
echo 'All three asserted SQL Server cases passed; no complete watermark protocol or performance qualification.'
