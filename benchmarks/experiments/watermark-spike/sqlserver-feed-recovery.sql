USE WatermarkFeedSpike;
SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND is_read_committed_snapshot_on=1 AND delayed_durability_desc='DISABLED')
    THROW 51000, 'Recovered database changed required settings', 1;
IF EXISTS(SELECT 1 FROM Ledger WHERE EventId=11) OR EXISTS(SELECT 1 FROM EventPayload WHERE EventId=11) OR EXISTS(SELECT 1 FROM Outbox WHERE EventId=11)
    THROW 51000, 'Uncommitted append survived crash', 1;
IF NOT EXISTS(SELECT 1 FROM Subscription s CROSS JOIN Publication p JOIN RecoveryExpected e ON p.Epoch=e.Epoch
    WHERE s.Id=4 AND s.Epoch=e.Epoch AND s.StartHead=e.StartHead AND s.CursorPosition=e.CursorPosition
    AND s.IncludeCursor=e.IncludeCursor AND s.ProjectionValue=e.ProjectionValue AND p.ExclusiveBound=e.ExclusiveBound)
    THROW 51000, 'Durable publication or subscription state changed across crash', 1;
DECLARE @epoch uniqueidentifier=(SELECT Epoch FROM SourceIdentity);
EXEC Publish @epoch;
EXEC ApplyPage 4,@epoch,100;
IF (SELECT ProjectionValue FROM Subscription WHERE Id=4)<>1457890
    OR (SELECT COUNT(*) FROM Applied WHERE SubscriptionId=4)<>8
    THROW 51000, 'Recovery repeated or lost prior effects', 1;
EXEC AppendEvent 12,1;
EXEC Publish @epoch;
EXEC ApplyPage 4,@epoch,100;
IF (SELECT ProjectionValue FROM Subscription WHERE Id=4)<>14578901
    OR (SELECT COUNT(*) FROM Applied WHERE SubscriptionId=4)<>9
    OR (SELECT COUNT(*) FROM Outbox)<>9
    THROW 51000, 'Post-recovery publication or application failed', 1;
PRINT 'PASS: actual publication, replay head, cursor and effects survive process crash; incomplete append rolls back; new append progresses';
