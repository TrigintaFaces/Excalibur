-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
-- SQL Server 2017+, default dbo names. Read README.md. Run in CHECKPOINT database.
DECLARE @ExpectedDatabase sysname = N'REPLACE_CHECKPOINT_DATABASE';
DECLARE @EventDatabase sysname = N'REPLACE_EVENT_DATABASE';
DECLARE @AllHostsStopped bit = 0;
DECLARE @FreshProjectionReplayPlanned bit = 0;
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> @ExpectedDatabase OR @ExpectedDatabase = N'REPLACE_CHECKPOINT_DATABASE'
    THROW 51000, 'Select and confirm the checkpoint database.', 1;
IF DB_ID(@EventDatabase) IS NULL OR @EventDatabase = N'REPLACE_EVENT_DATABASE'
    THROW 51001, 'Select the actual event source database on this SQL Server instance.', 1;
IF @AllHostsStopped <> 1 OR @FreshProjectionReplayPlanned <> 1
    THROW 51002, 'Stop all hosts and arrange fresh destination replay before acknowledging this migration.', 1;
IF @@TRANCOUNT <> 0 THROW 51003, 'Run outside an existing transaction.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'Excalibur.alpha19-alpha20.checkpoints',
        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
    IF @LockResult < 0 THROW 51004, 'Migration lock unavailable.', 1;

    -- Same lock order as append: allocator FIRST, then events. Hold through migration.
    -- Use the source database, even if checkpoints are stored in another database.
    DECLARE @Source nvarchar(258)=QUOTENAME(@EventDatabase), @Preflight nvarchar(max);
    SET @Preflight=N'
        DECLARE @AllocatorRows bigint, @Counter bigint, @Rows bigint, @Unique bigint, @First bigint, @Head bigint;
        SELECT @AllocatorRows=COUNT_BIG(*), @Counter=MAX(CASE WHEN Id=1 THEN Value END)
            FROM '+@Source+N'.dbo.EventStoreEventsPosition WITH (TABLOCKX,HOLDLOCK);
        SELECT @Rows=COUNT_BIG(*), @Unique=COUNT_BIG(DISTINCT Position), @First=MIN(Position), @Head=COALESCE(MAX(Position),0)
            FROM '+@Source+N'.dbo.EventStoreEvents WITH (TABLOCKX,HOLDLOCK);
        IF @AllocatorRows<>1 OR @Counter IS NULL OR @Counter<>@Head OR @Rows<>@Unique
           OR (@Rows>0 AND (@First<>1 OR @Rows<>@Head)) OR (@Rows=0 AND @Counter<>0)
        BEGIN
            SELECT @AllocatorRows AllocatorRows,@Counter AllocatorValue,@Rows EventCount,@Unique UniquePositions,@First FirstPosition,@Head LastPosition;
            THROW 51005, ''Event stream is not a complete 1..head sequence with matching allocator. Migration refused; never skip gaps automatically.'',1;
        END;';
    EXEC sys.sp_executesql @Preflight;

    DECLARE @OldId int=OBJECT_ID(N'dbo.SubscriptionCheckpoints',N'U');
    IF @OldId IS NULL THROW 51006, 'Missing default checkpoint table.',1;
    IF COL_LENGTH(N'dbo.SubscriptionCheckpoints',N'ProjectionKey') IS NULL
    BEGIN
        IF OBJECT_ID(N'dbo.SubscriptionCheckpoints_Alpha19') IS NOT NULL OR OBJECT_ID(N'dbo.SubscriptionCheckpointsReaders') IS NOT NULL
            THROW 51007, 'Archive/readers collision. Nothing changed.',1;
        IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=@OldId)<>3
            OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=@OldId AND name='SubscriptionName' AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=510 AND is_nullable=0 AND collation_name='Latin1_General_BIN2')
            OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=@OldId AND name='Position' AND TYPE_NAME(user_type_id)='bigint' AND is_nullable=0)
            OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=@OldId AND name='UpdatedAt' AND TYPE_NAME(user_type_id)='datetimeoffset' AND scale=7 AND is_nullable=0)
            THROW 51008,'Unexpected alpha19 shape; use a reviewed custom migration.',1;
        IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=@OldId OR referenced_object_id=@OldId)
            OR EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=@OldId)
            OR EXISTS(SELECT 1 FROM sys.sql_expression_dependencies WHERE referenced_id=@OldId)
            THROW 51009,'Custom dependencies/triggers require review.',1;
        IF EXISTS(SELECT 1 FROM sys.database_permissions WHERE class=1 AND major_id=@OldId AND minor_id<>0)
            THROW 51010,'Column permissions require a custom migration.',1;
        EXEC sys.sp_rename N'dbo.SubscriptionCheckpoints',N'SubscriptionCheckpoints_Alpha19',N'OBJECT';
        EXEC(N'-- One row per member lifetime. Binding creates a NULL position; advances only update the matching lifetime.
-- Greenfield provisioning: existing databases must use this schema. No legacy data migration.
IF OBJECT_ID(N''dbo.SubscriptionCheckpoints'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.SubscriptionCheckpoints (
        SubscriptionName NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL,
        ProjectionKey NVARCHAR(512) COLLATE Latin1_General_BIN2 NOT NULL,
        DefinitionHash VARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CHECK (LEN(DefinitionHash) = 64 AND DefinitionHash NOT LIKE ''%[^0-9A-F]%''),
        Destination NVARCHAR(512) COLLATE Latin1_General_BIN2 NOT NULL,
        DestinationScope NVARCHAR(512) COLLATE Latin1_General_BIN2 NULL,
        BindingId VARCHAR(36) COLLATE Latin1_General_BIN2 NOT NULL UNIQUE,
        Position BIGINT NULL CHECK (Position IS NULL OR Position >= 0),
        PRIMARY KEY NONCLUSTERED (SubscriptionName, ProjectionKey)
    );
END;

IF OBJECT_ID(N''dbo.SubscriptionCheckpointsReaders'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.SubscriptionCheckpointsReaders (
        SubscriptionName NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL,
        ReaderName NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL,
        Revision VARCHAR(36) COLLATE Latin1_General_BIN2 NOT NULL,
        BindingId VARCHAR(36) COLLATE Latin1_General_BIN2 NOT NULL REFERENCES dbo.SubscriptionCheckpoints(BindingId),
        PRIMARY KEY NONCLUSTERED (SubscriptionName, ReaderName)
    );
END;
');
        DECLARE @Permissions nvarchar(max);
        SELECT @Permissions=STRING_AGG(CAST(CASE p.state WHEN 'D' THEN N'DENY ' ELSE N'GRANT ' END+p.permission_name+
            N' ON OBJECT::dbo.'+QUOTENAME(t.TableName)+N' TO '+QUOTENAME(USER_NAME(p.grantee_principal_id))+
            CASE p.state WHEN 'W' THEN N' WITH GRANT OPTION' ELSE N'' END+N';' AS nvarchar(max)),N'')
        FROM sys.database_permissions p CROSS JOIN(VALUES(N'SubscriptionCheckpoints'),(N'SubscriptionCheckpointsReaders'))t(TableName)
        WHERE p.class=1 AND p.major_id=@OldId AND p.minor_id=0 AND p.state IN('D','G','W');
        IF LEN(@Permissions)>0 EXEC sys.sp_executesql @Permissions;
    END
    ELSE IF OBJECT_ID(N'dbo.SubscriptionCheckpoints_Alpha19',N'U') IS NULL
        THROW 51011,'Already-current schema without this migration archive; inspect manually.',1;

    -- Validate the same columns/keys/checks on initial and repeated execution.
    DECLARE @Expected TABLE(TableName sysname,ColumnName sysname,TypeName sysname,Width smallint,Nullable bit,Collation sysname NULL);
    INSERT @Expected VALUES
    ('SubscriptionCheckpoints','SubscriptionName','nvarchar',510,0,'Latin1_General_BIN2'),
    ('SubscriptionCheckpoints','ProjectionKey','nvarchar',1024,0,'Latin1_General_BIN2'),
    ('SubscriptionCheckpoints','DefinitionHash','varchar',64,0,'Latin1_General_BIN2'),
    ('SubscriptionCheckpoints','Destination','nvarchar',1024,0,'Latin1_General_BIN2'),
    ('SubscriptionCheckpoints','DestinationScope','nvarchar',1024,1,'Latin1_General_BIN2'),
    ('SubscriptionCheckpoints','BindingId','varchar',36,0,'Latin1_General_BIN2'),
    ('SubscriptionCheckpoints','Position','bigint',8,1,NULL),
    ('SubscriptionCheckpointsReaders','SubscriptionName','nvarchar',510,0,'Latin1_General_BIN2'),
    ('SubscriptionCheckpointsReaders','ReaderName','nvarchar',510,0,'Latin1_General_BIN2'),
    ('SubscriptionCheckpointsReaders','Revision','varchar',36,0,'Latin1_General_BIN2'),
    ('SubscriptionCheckpointsReaders','BindingId','varchar',36,0,'Latin1_General_BIN2');
    DECLARE @Actual TABLE(TableName sysname,ColumnName sysname,TypeName sysname,Width smallint,Nullable bit,Collation sysname NULL);
    INSERT @Actual SELECT OBJECT_NAME(object_id),name,TYPE_NAME(user_type_id),max_length,is_nullable,collation_name
        FROM sys.columns WHERE object_id IN(OBJECT_ID(N'dbo.SubscriptionCheckpoints'),OBJECT_ID(N'dbo.SubscriptionCheckpointsReaders'));
    IF EXISTS(SELECT * FROM @Expected EXCEPT SELECT * FROM @Actual) OR EXISTS(SELECT * FROM @Actual EXCEPT SELECT * FROM @Expected)
        THROW 51012,'Unexpected migrated column shape.',1;
    DECLARE @Keys TABLE(TableName sysname,ColumnName sysname,Ordinal int);
    INSERT @Keys VALUES('SubscriptionCheckpoints','SubscriptionName',1),('SubscriptionCheckpoints','ProjectionKey',2),
        ('SubscriptionCheckpointsReaders','SubscriptionName',1),('SubscriptionCheckpointsReaders','ReaderName',2);
    DECLARE @ActualKeys TABLE(TableName sysname,ColumnName sysname,Ordinal int);
    INSERT @ActualKeys SELECT OBJECT_NAME(i.object_id),c.name,ic.key_ordinal
        FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
        JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE i.object_id IN(OBJECT_ID(N'dbo.SubscriptionCheckpoints'),OBJECT_ID(N'dbo.SubscriptionCheckpointsReaders'))
            AND i.is_primary_key=1 AND i.type=2 AND i.is_disabled=0 AND ic.key_ordinal>0;
    IF EXISTS(SELECT * FROM @Keys EXCEPT SELECT * FROM @ActualKeys) OR EXISTS(SELECT * FROM @ActualKeys EXCEPT SELECT * FROM @Keys)
        THROW 51013,'Unexpected binding primary keys.',1;
    IF NOT EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
        JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE i.object_id=OBJECT_ID(N'dbo.SubscriptionCheckpoints') AND i.is_unique=1 AND i.is_disabled=0 AND i.has_filter=0
            AND c.name='BindingId' AND ic.key_ordinal=1
            AND NOT EXISTS(SELECT 1 FROM sys.index_columns x WHERE x.object_id=i.object_id AND x.index_id=i.index_id AND x.key_ordinal>1))
        THROW 51014,'BindingId must be unique.',1;
    IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns k ON f.object_id=k.constraint_object_id
        WHERE f.parent_object_id=OBJECT_ID(N'dbo.SubscriptionCheckpointsReaders') AND f.referenced_object_id=OBJECT_ID(N'dbo.SubscriptionCheckpoints')
            AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND COL_NAME(k.parent_object_id,k.parent_column_id)='BindingId' AND COL_NAME(k.referenced_object_id,k.referenced_column_id)='BindingId'
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=1)
        THROW 51015,'Unexpected reader binding foreign key.',1;
    CREATE TABLE #ExpectedChecks(DefinitionHash VARCHAR(64) CHECK(LEN(DefinitionHash)=64 AND DefinitionHash NOT LIKE '%[^0-9A-F]%'),
        Position BIGINT CHECK(Position IS NULL OR Position>=0));
    IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.SubscriptionCheckpoints') AND is_disabled=0 AND is_not_trusted=0)<>2
        OR EXISTS(SELECT definition COLLATE Latin1_General_BIN2 FROM tempdb.sys.check_constraints WHERE parent_object_id=OBJECT_ID('tempdb..#ExpectedChecks')
            EXCEPT SELECT definition COLLATE Latin1_General_BIN2 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.SubscriptionCheckpoints'))
        THROW 51016,'Unexpected binding CHECK constraints.',1;
    DROP TABLE #ExpectedChecks;
    COMMIT;
    PRINT 'Schema ready. Legacy rows and definitions retained. Replay fresh destinations and verify before cutover.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
