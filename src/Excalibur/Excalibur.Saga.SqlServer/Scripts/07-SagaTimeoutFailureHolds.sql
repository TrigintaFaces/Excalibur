-- Apply explicitly before hosted preserving timeout delivery. No runtime migration.
-- Set this selected DIRECT schema/table to SqlServerSagaTimeoutStoreOptions. Default/case aliases
-- resolve to the same object; distinct custom direct tables each require this migration.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @Target NVARCHAR(776)=N'dbo.SagaTimeouts';
IF PARSENAME(@Target,3) IS NOT NULL OR PARSENAME(@Target,1) IS NULL
 OR OBJECT_ID(@Target,N'U') IS NULL
 THROW 51004,'Select the direct timeout table, not a view, synonym or cross-database object.',1;
DECLARE @Object INT=OBJECT_ID(@Target,N'U');
DECLARE @Qualified NVARCHAR(776)=QUOTENAME(OBJECT_SCHEMA_NAME(@Object))+N'.'+QUOTENAME(OBJECT_NAME(@Object));
BEGIN TRANSACTION;
BEGIN TRY
 DECLARE @Count INT=(SELECT COUNT(*) FROM sys.columns WHERE object_id=@Object AND name IN
 (N'FailureSourceEpoch',N'FailureDeliveryEpoch',N'FailureHeld',N'FailureVersion',N'FailureToken',N'FailureOperation',
 N'FailureOriginalClaim',N'FailureCurrentClaim',N'FailureObservedDeliveryEpoch',N'FailureKind',N'FailureReason',N'FailureResumeCommand'));
 IF @Count<>0 AND @Count<>12 THROW 51004,'Resolve the partial timeout failure-hold schema before migration.',1;
 IF @Count=0
 BEGIN
  -- ALTER obtains schema authority; persisted legacy winners initialize ONCE under that boundary.
  DECLARE @Sql NVARCHAR(MAX)=N'ALTER TABLE '+@Qualified+N' ADD FailureSourceEpoch UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    FailureDeliveryEpoch UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    FailureHeld BIT NOT NULL DEFAULT 0,
    FailureVersion BIGINT NOT NULL DEFAULT 0,
    FailureToken UNIQUEIDENTIFIER NULL,
    FailureOperation UNIQUEIDENTIFIER NULL,
    FailureOriginalClaim NVARCHAR(200) NULL,
    FailureCurrentClaim NVARCHAR(200) NULL,
    FailureObservedDeliveryEpoch UNIQUEIDENTIFIER NULL,
    FailureKind INT NULL,
    FailureReason NVARCHAR(1024) NULL,
    FailureResumeCommand UNIQUEIDENTIFIER NULL, CHECK (FailureSourceEpoch<>''00000000-0000-0000-0000-000000000000'' AND FailureDeliveryEpoch<>''00000000-0000-0000-0000-000000000000''),
    CHECK (FailureVersion>=0),
    CHECK (FailureHeld=0 OR (FailureVersion>0 AND FailureToken IS NOT NULL AND FailureOperation IS NOT NULL
      AND FailureOriginalClaim IS NOT NULL AND FailureCurrentClaim IS NOT NULL AND FailureObservedDeliveryEpoch IS NOT NULL
      AND FailureKind IS NOT NULL AND FailureKind IN(1,2,4) AND FailureReason IS NOT NULL));';
  EXEC sys.sp_executesql @Sql;
  SET @Count=12;
 END;
 IF @Count=12 AND (SELECT COUNT(*) FROM sys.columns c
 JOIN sys.default_constraints d ON d.object_id=c.default_object_id
 WHERE c.object_id=@Object AND
 ((c.name IN(N'FailureSourceEpoch',N'FailureDeliveryEpoch') AND UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(d.definition,N'(',N''),N')',N''),N' ',N''),NCHAR(9),N''),NCHAR(10),N''),NCHAR(13),N''))=N'NEWID')
  OR (c.name IN(N'FailureHeld',N'FailureVersion') AND UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(d.definition,N'(',N''),N')',N''),N' ',N''),NCHAR(9),N''),NCHAR(10),N''),NCHAR(13),N''))=N'0')))<>4
 THROW 51004,'Timeout failure-hold default definitions must use NEWID epochs and zero state.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=@Object AND minor_id=0 AND name=N'Excalibur.TimeoutFailureTarget')
 BEGIN
  DECLARE @Epoch UNIQUEIDENTIFIER=NEWID(),@Schema SYSNAME=OBJECT_SCHEMA_NAME(@Object),@Table SYSNAME=OBJECT_NAME(@Object);
  EXEC sys.sp_addextendedproperty @name=N'Excalibur.TimeoutFailureTarget',@value=@Epoch,
   @level0type=N'SCHEMA',@level0name=@Schema,@level1type=N'TABLE',@level1name=@Table;
 END;
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=@Object AND name=N'IX_TimeoutFailureHeld')
 BEGIN
  DECLARE @IndexSql NVARCHAR(MAX)=N'CREATE INDEX IX_TimeoutFailureHeld ON '+@Qualified+N'(TenantId,FailureHeld,TimeoutId);';
  EXEC sys.sp_executesql @IndexSql;
 END;
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
