USE WatermarkCrash;
SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM Ledger WITH(READUNCOMMITTED) WHERE Id=1)
    THROW 51000,'Experiment writer has not inserted event 1',1;
IF (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE host_name='watermark-crash-writer') <> 1
    THROW 51000,'Expected exactly the experiment writer',1;
IF NOT EXISTS(SELECT 1 FROM sys.dm_exec_sessions s JOIN sys.dm_exec_requests r ON s.session_id=r.session_id
              WHERE s.host_name='watermark-crash-writer' AND r.wait_type='WAITFOR' AND r.open_transaction_count>0)
    THROW 51000,'Experiment writer has not reached its open-transaction barrier',1;
INSERT Ledger(Id,Payload) VALUES(2,20);
DECLARE @pending binary(8)=(SELECT Position FROM Ledger WITH(READUNCOMMITTED) WHERE Id=1);
DECLARE @boundary binary(8)=MIN_ACTIVE_ROWVERSION();
IF @boundary<>@pending THROW 51000,'Stable boundary failed to protect pending event',1;
IF (SELECT COUNT(*) FROM Ledger)<>1 OR NOT EXISTS(SELECT 1 FROM Ledger WHERE Id=2)
    THROW 51000,'Committed control event not visible independently of pending event',1;
SELECT @pending AS PendingPosition,@boundary AS MinimumActive,@@DBTS AS Dbts;
SELECT s.session_id,s.host_name,r.wait_type,r.open_transaction_count
FROM sys.dm_exec_sessions s JOIN sys.dm_exec_requests r ON s.session_id=r.session_id
WHERE s.host_name='watermark-crash-writer';
PRINT 'PASS pre-crash barrier: 1 uncommitted, 2 committed, stable boundary holds at 1';
GO
