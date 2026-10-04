USE WatermarkCrash;
SET NOCOUNT ON;
IF (SELECT COUNT(*) FROM Ledger)<>1 OR NOT EXISTS(SELECT 1 FROM Ledger WHERE Id=2)
    THROW 51000,'Committed event lost or pending event survived recovery',1;
DECLARE @recovered binary(8)=(SELECT Position FROM Ledger WHERE Id=2);
DECLARE @boundary binary(8)=MIN_ACTIVE_ROWVERSION();
IF @boundary<=@recovered THROW 51000,'Boundary failed to pass recovered event',1;
IF (SELECT COUNT(*) FROM Ledger WHERE Position<@boundary)<>1
    THROW 51000,'Stable read failed after recovery',1;
PRINT 'PASS recovery: committed event survives, in-flight event rolls back, boundary passes gap';
INSERT Ledger(Id,Payload) VALUES(3,30);
DECLARE @new binary(8)=(SELECT Position FROM Ledger WHERE Id=3);
IF @new<=@recovered THROW 51000,'Rowversion did not advance after restart',1;
SET @boundary=MIN_ACTIVE_ROWVERSION();
IF (SELECT COUNT(*) FROM Ledger WHERE Position<@boundary)<>2
    THROW 51000,'Post-recovery stable read failed',1;
SELECT Id,Payload,Position FROM Ledger ORDER BY Position;
SELECT @boundary AS MinimumActiveAfterRecovery;
PRINT 'PASS new append: position advanced and both committed events are readable';
GO
