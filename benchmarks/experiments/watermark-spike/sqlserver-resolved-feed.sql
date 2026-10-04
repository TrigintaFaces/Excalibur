-- Research-only single-source, single-tenant resolved feed. Run in a fresh disposable engine.
-- This is not the shipped IGlobalStreamQuery contract or a performance result.
CREATE DATABASE WatermarkFeedSpike;
ALTER DATABASE WatermarkFeedSpike SET READ_COMMITTED_SNAPSHOT ON;
GO
USE WatermarkFeedSpike;
SET NOCOUNT ON;
CREATE TABLE SourceIdentity(Id int PRIMARY KEY CHECK(Id=1), Epoch uniqueidentifier NOT NULL);
INSERT SourceIdentity VALUES(1, NEWID());
CREATE TABLE EventPayload(EventId int PRIMARY KEY, Payload int NULL, Erased bit NOT NULL DEFAULT 0);
CREATE TABLE Ledger(EventId int PRIMARY KEY REFERENCES EventPayload(EventId), Position rowversion NOT NULL);
CREATE UNIQUE INDEX IX_Ledger_Position ON Ledger(Position);
CREATE TABLE Outbox(EventId int PRIMARY KEY REFERENCES EventPayload(EventId));
CREATE TABLE Publication(Id int PRIMARY KEY CHECK(Id=1), Epoch uniqueidentifier NOT NULL, ExclusiveBound binary(8) NOT NULL);
INSERT Publication SELECT 1, Epoch, 0x0000000000000000 FROM SourceIdentity;
CREATE TABLE Subscription(Id int PRIMARY KEY, Epoch uniqueidentifier NOT NULL, StartHead binary(8) NOT NULL,
    CursorPosition binary(8) NOT NULL, IncludeCursor bit NOT NULL, ProjectionValue bigint NOT NULL);
CREATE TABLE Applied(ApplyOrder bigint IDENTITY PRIMARY KEY, SubscriptionId int NOT NULL,
    EventId int NOT NULL, Position binary(8) NOT NULL, IsReplay bit NOT NULL, WasErased bit NOT NULL,
    UNIQUE(SubscriptionId, EventId));
CREATE TABLE Gate(Id int PRIMARY KEY, Released bit NOT NULL, ObservedBound binary(8) NULL);
INSERT Gate(Id,Released) VALUES(1,0),(2,0),(3,0),(4,0),(104,0);
GO
CREATE TRIGGER ImmutableIncarnation ON SourceIdentity INSTEAD OF UPDATE, DELETE AS
BEGIN
    THROW 51000, 'Incarnation transitions require a separate fenced restore protocol', 1;
END;
GO
CREATE TRIGGER ImmutableLedger ON Ledger INSTEAD OF UPDATE, DELETE AS
BEGIN
    THROW 51000, 'Ordering ledger is immutable', 1;
END;
GO
CREATE PROCEDURE AppendEvent @EventId int, @Payload int AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @@TRANCOUNT<>0 THROW 51000, 'Append owns its transaction', 1;
    BEGIN TRAN;
    BEGIN TRY
        INSERT EventPayload(EventId,Payload) VALUES(@EventId,@Payload);
        -- Allocation follows all causal inputs. No preallocated/cached application positions.
        INSERT Ledger(EventId) VALUES(@EventId);
        INSERT Outbox VALUES(@EventId);
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
CREATE PROCEDURE Publish @Epoch uniqueidentifier, @PauseGate int=NULL AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT<>0 THROW 51000, 'Publication requires a fresh local operation', 1;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    IF NOT EXISTS(SELECT 1 FROM SourceIdentity WHERE Epoch=@Epoch)
        THROW 51000, 'Source incarnation mismatch', 1;
    DECLARE @bound binary(8)=MIN_ACTIVE_ROWVERSION();
    IF @PauseGate IS NOT NULL
    BEGIN
        UPDATE Gate SET ObservedBound=@bound WHERE Id=@PauseGate;
        DECLARE @deadline datetime2=DATEADD(second,45,SYSUTCDATETIME());
        WHILE (SELECT Released FROM Gate WHERE Id=@PauseGate)=0
        BEGIN
            IF SYSUTCDATETIME()>@deadline THROW 51000, 'Publisher release deadline exceeded', 1;
            WAITFOR DELAY '00:00:00.100';
        END;
    END;
    -- A delayed publisher cannot regress a newer publication. No writer takes this row lock.
    UPDATE Publication SET ExclusiveBound=@bound
    WHERE Id=1 AND Epoch=@Epoch AND ExclusiveBound<@bound;
END;
GO
CREATE PROCEDURE StartSubscription @Id int, @Epoch uniqueidentifier AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT<>0 THROW 51000, 'Subscription start requires a fresh operation', 1;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    IF NOT EXISTS(SELECT 1 FROM SourceIdentity WHERE Epoch=@Epoch)
        THROW 51000, 'Source incarnation mismatch', 1;
    -- This committed RCSI statement defines the start linearization point, independently of B.
    DECLARE @head binary(8)=COALESCE((SELECT MAX(Position) FROM Ledger),0x0000000000000000);
    INSERT Subscription VALUES(@Id,@Epoch,@head,0x0000000000000000,1,0);
END;
GO
CREATE PROCEDURE ApplyPage @Id int, @Epoch uniqueidentifier, @PageSize int, @FailBeforeCheckpoint bit=0, @PauseGate int=NULL AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @@TRANCOUNT<>0 THROW 51000, 'Reader requires a fresh local transaction', 1;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    IF @PageSize<1 THROW 51000, 'Positive page size required', 1;
    IF NOT EXISTS(SELECT 1 FROM SourceIdentity WHERE Epoch=@Epoch)
        THROW 51000, 'Source incarnation mismatch', 1;
    BEGIN TRAN;
    BEGIN TRY
        DECLARE @cursor binary(8), @include bit, @head binary(8), @bound binary(8);
        SELECT @cursor=CursorPosition,@include=IncludeCursor,@head=StartHead
        FROM Subscription WITH(UPDLOCK,HOLDLOCK) WHERE Id=@Id AND Epoch=@Epoch;
        IF @cursor IS NULL THROW 51000, 'Subscription scope mismatch', 1;
        SELECT @bound=ExclusiveBound FROM Publication WHERE Id=1 AND Epoch=@Epoch;
        IF @bound IS NULL OR @bound<@cursor THROW 51000, 'Invalid publication boundary', 1;
        IF @PauseGate IS NOT NULL
        BEGIN
            UPDATE Gate SET ObservedBound=@bound WHERE Id=@PauseGate;
            DECLARE @deadline datetime2=DATEADD(second,45,SYSUTCDATETIME());
            WHILE (SELECT Released FROM Gate WHERE Id=@PauseGate+100)=0
            BEGIN
                IF SYSUTCDATETIME()>@deadline THROW 51000, 'Reader release deadline exceeded', 1;
                WAITFOR DELAY '00:00:00.100';
            END;
        END;
        DECLARE @page TABLE(EventId int NOT NULL, Position binary(8) NOT NULL PRIMARY KEY, Payload int NULL, Erased bit NULL);
        -- A fresh RCSI statement AFTER observing publication; never an older snapshot or replica.
        INSERT @page SELECT TOP(@PageSize) l.EventId,l.Position,p.Payload,p.Erased FROM Ledger l
        LEFT JOIN EventPayload p ON p.EventId=l.EventId
        WHERE (l.Position>@cursor OR (l.Position=@cursor AND @include=1)) AND l.Position<@bound
        ORDER BY l.Position;
        IF EXISTS(SELECT 1 FROM @page WHERE Erased IS NULL OR (Erased=0 AND Payload IS NULL))
            THROW 51000, 'Unresolved payload cannot be skipped', 1;
        DECLARE @event int, @position binary(8), @payload int, @erased bit;
        DECLARE ordered_page CURSOR LOCAL FAST_FORWARD FOR SELECT EventId,Position,Payload,Erased FROM @page ORDER BY Position;
        OPEN ordered_page;
        FETCH NEXT FROM ordered_page INTO @event,@position,@payload,@erased;
        WHILE @@FETCH_STATUS=0
        BEGIN
            IF @erased=0 UPDATE Subscription SET ProjectionValue=ProjectionValue*10+@payload WHERE Id=@Id;
            INSERT Applied(SubscriptionId,EventId,Position,IsReplay,WasErased)
            VALUES(@Id,@event,@position,CASE WHEN @position<=@head THEN 1 ELSE 0 END,@erased);
            FETCH NEXT FROM ordered_page INTO @event,@position,@payload,@erased;
        END;
        CLOSE ordered_page;
        DEALLOCATE ordered_page;
        IF @FailBeforeCheckpoint=1 THROW 51001, 'Injected failure after effects before checkpoint', 1;
        IF (SELECT COUNT(*) FROM @page)=@PageSize
            UPDATE Subscription SET CursorPosition=(SELECT MAX(Position) FROM @page),IncludeCursor=0 WHERE Id=@Id;
        ELSE
            -- The first unresolved position may equal B. Include it when a later B exposes it.
            UPDATE Subscription SET CursorPosition=@bound,IncludeCursor=1 WHERE Id=@Id;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
SET NOCOUNT ON;
SELECT @@VERSION AS EngineVersion;
SELECT name,is_read_committed_snapshot_on,delayed_durability_desc FROM sys.databases WHERE name=DB_NAME();
IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND is_read_committed_snapshot_on=1 AND delayed_durability_desc='DISABLED')
    THROW 51000, 'Required visibility or durability settings are absent', 1;
DECLARE @epoch uniqueidentifier=(SELECT Epoch FROM SourceIdentity);
EXEC AppendEvent 1,1;
BEGIN TRAN;
INSERT EventPayload(EventId,Payload) VALUES(2,20);
INSERT Ledger(EventId) VALUES(2);
ROLLBACK;
EXEC AppendEvent 3,3;
-- Publication intentionally lags at zero when subscription captures its fresh committed head.
EXEC StartSubscription 1,@epoch;
EXEC AppendEvent 4,4;
EXEC Publish @epoch;
BEGIN TRY
    EXEC ApplyPage 1,@epoch,2,1;
    THROW 51000, 'Expected injected failure', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51001 THROW;
END CATCH;
IF EXISTS(SELECT 1 FROM Applied) THROW 51000, 'Effects escaped failed checkpoint transaction', 1;
IF EXISTS(SELECT 1 FROM Subscription WHERE ProjectionValue<>0) THROW 51000, 'Projection state escaped rollback', 1;
IF EXISTS(SELECT 1 FROM Subscription WHERE CursorPosition<>0x0000000000000000 OR IncludeCursor<>1)
    THROW 51000, 'Failed page advanced checkpoint', 1;
PRINT 'PASS: effects and checkpoint roll back together';
EXEC ApplyPage 1,@epoch,1;
IF (SELECT COUNT(*) FROM Applied)<>1 OR NOT EXISTS(SELECT 1 FROM Applied WHERE EventId=1)
    THROW 51000, 'First bounded page was incomplete or skipped an event', 1;
IF (SELECT CursorPosition FROM Subscription WHERE Id=1)<>(SELECT Position FROM Ledger WHERE EventId=1)
    THROW 51000, 'Full page jumped to publication boundary', 1;
EXEC ApplyPage 1,@epoch,10;
IF (SELECT COUNT(*) FROM Applied)<>3 OR EXISTS(SELECT 1 FROM Applied WHERE EventId=2)
    THROW 51000, 'Rollback gap blocked progress or delivered an aborted event', 1;
IF EXISTS(SELECT 1 FROM Applied WHERE (EventId IN(1,3) AND IsReplay<>1) OR (EventId=4 AND IsReplay<>0))
    THROW 51000, 'Mixed replay/live page violated subscription boundary', 1;
IF EXISTS(SELECT 1 FROM (SELECT Position,LAG(Position) OVER(ORDER BY ApplyOrder) AS Previous FROM Applied) p WHERE Previous>=Position)
    THROW 51000, 'Projection effects were applied out of order', 1;
PRINT 'PASS: paged ordered application crosses rollback gaps and classifies replay per event';
IF (SELECT ProjectionValue FROM Subscription WHERE Id=1)<>134 THROW 51000, 'Noncommutative projection applied events in the wrong order', 1;
DECLARE @immutable binary(8)=(SELECT Position FROM Ledger WHERE EventId=3);
UPDATE EventPayload SET Payload=NULL,Erased=1 WHERE EventId=3;
IF (SELECT Position FROM Ledger WHERE EventId=3)<>@immutable THROW 51000, 'Erasure moved ordering position', 1;
BEGIN TRY
    UPDATE Ledger SET EventId=EventId WHERE EventId=3;
    THROW 51000, 'Expected immutable ledger refusal', 1;
END TRY
BEGIN CATCH
    IF ERROR_MESSAGE()<>'Ordering ledger is immutable' THROW;
END CATCH;
PRINT 'PASS: mutable payload disposition is separate from immutable ordering';
BEGIN TRAN;
INSERT EventPayload(EventId,Payload) VALUES(6,60);
INSERT Ledger(EventId) VALUES(6);
ROLLBACK;
EXEC Publish @epoch;
EXEC ApplyPage 1,@epoch,10;
DECLARE @emptyBoundary binary(8)=(SELECT CursorPosition FROM Subscription WHERE Id=1);
IF (SELECT IncludeCursor FROM Subscription WHERE Id=1)<>1 THROW 51000, 'Exhausted boundary must remain inclusive', 1;
EXEC AppendEvent 5,5;
SELECT @emptyBoundary AS ExhaustedBoundary,(SELECT Position FROM Ledger WHERE EventId=5) AS NextPosition,@@DBTS AS Dbts;
IF (SELECT Position FROM Ledger WHERE EventId=5)<@emptyBoundary
    THROW 51000, 'New allocation fell below exhausted boundary', 1;
EXEC Publish @epoch;
EXEC ApplyPage 1,@epoch,10;
EXEC ApplyPage 1,@epoch,10;
IF (SELECT COUNT(*) FROM Applied)<>4 OR NOT EXISTS(SELECT 1 FROM Applied WHERE EventId=5 AND IsReplay=0)
    THROW 51000, 'Empty interval skipped a later event or re-applied effects', 1;
IF (SELECT COUNT(*) FROM Outbox)<>4 THROW 51000, 'Outbox diverged from committed appends', 1;
IF (SELECT ProjectionValue FROM Subscription WHERE Id=1)<>1345 THROW 51000, 'Projection lost or repeated an event', 1;
PRINT 'PASS: empty resolved intervals advance without skipping subsequent events';
EXEC StartSubscription 2,@epoch;
UPDATE EventPayload SET Payload=NULL WHERE EventId=5;
BEGIN TRY
    EXEC ApplyPage 2,@epoch,10;
    THROW 51000, 'Expected unresolved payload refusal', 1;
END TRY
BEGIN CATCH
    IF ERROR_MESSAGE()<>'Unresolved payload cannot be skipped' THROW;
END CATCH;
IF EXISTS(SELECT 1 FROM Applied WHERE SubscriptionId=2) OR EXISTS(SELECT 1 FROM Subscription WHERE Id=2 AND ProjectionValue<>0)
    THROW 51000, 'Unresolved payload allowed partial effects', 1;
UPDATE EventPayload SET Payload=5 WHERE EventId=5;
EXEC ApplyPage 2,@epoch,10;
IF (SELECT ProjectionValue FROM Subscription WHERE Id=2)<>145
    OR NOT EXISTS(SELECT 1 FROM Applied WHERE SubscriptionId=2 AND EventId=3 AND WasErased=1)
    THROW 51000, 'Positive erasure and missing payload were conflated', 1;
PRINT 'PASS: missing payload refuses progress; positive erasure is explicitly handled';
DECLARE @wrongEpoch uniqueidentifier=NEWID();
BEGIN TRY
    EXEC ApplyPage 1,@wrongEpoch,10;
    THROW 51000, 'Expected source incarnation refusal', 1;
END TRY
BEGIN CATCH
    IF ERROR_MESSAGE()<>'Source incarnation mismatch' THROW;
END CATCH;
PRINT 'PASS: mismatched source incarnation rejected';
SELECT * FROM Publication;
SELECT * FROM Subscription;
SELECT * FROM Applied ORDER BY ApplyOrder;
PRINT 'Resolved-feed prototype assertions passed; no performance or production qualification.';
