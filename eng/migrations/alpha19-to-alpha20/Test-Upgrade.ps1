[CmdletBinding()]
param([Parameter(Mandatory)][string]$Container)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$name='Excalibur_Alpha20MigrationTest_'+[Guid]::NewGuid().ToString('N')
$source=$name+'_Source'; $fresh=$name+'_Fresh'
function Invoke-Sql([string]$Database,[string]$Sql,[string]$ExpectedError='') {
    $output=$Sql | docker exec -i $Container sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d "$1"' -- $Database 2>&1
    $code=$LASTEXITCODE
    if($ExpectedError){if($code -eq 0 -or ($output -join "`n") -notmatch $ExpectedError){throw "Expected $ExpectedError; got exit=$code : $output"}}
    elseif($code){throw ($output -join "`n")}
}
$legacy=(git -C $root show 'v10.0.0-alpha.19:src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/004_CreateSubscriptionCheckpointSchema.sql') -join "`n"
if($LASTEXITCODE){throw 'alpha19 baseline tag required'}
$current=Get-Content "$root/src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/004_CreateSubscriptionCheckpointSchema.sql" -Raw
$sql=Get-Content "$PSScriptRoot/Upgrade.sql" -Raw
$sql=$sql.Replace("@ExpectedDatabase sysname = N'REPLACE_CHECKPOINT_DATABASE'","@ExpectedDatabase sysname = N'$name'").Replace("@EventDatabase sysname = N'REPLACE_EVENT_DATABASE'","@EventDatabase sysname = N'$source'").Replace('@AllHostsStopped bit = 0','@AllHostsStopped bit = 1').Replace('@FreshProjectionReplayPlanned bit = 0','@FreshProjectionReplayPlanned bit = 1')
try {
    Invoke-Sql master "CREATE DATABASE [$name]; CREATE DATABASE [$source]; CREATE DATABASE [$fresh];"
    Invoke-Sql $name $legacy
    Invoke-Sql $fresh $current
    Invoke-Sql $source 'CREATE TABLE dbo.EventStoreEvents(Position bigint NOT NULL,Payload varbinary(max)); CREATE TABLE dbo.EventStoreEventsPosition(Id tinyint,Value bigint);'
    Invoke-Sql $name @'
INSERT dbo.SubscriptionCheckpoints VALUES ('Inline:app:Customer',2,'2026-01-01T00:00:00+00:00');
INSERT dbo.SubscriptionCheckpointsDefinitions VALUES ('Inline:app',REPLICATE('A',64));
CREATE USER TestReader WITHOUT LOGIN; CREATE USER TestWriter WITHOUT LOGIN;
GRANT SELECT ON dbo.SubscriptionCheckpoints TO TestReader;
DENY DELETE ON dbo.SubscriptionCheckpoints TO TestReader;
GRANT INSERT ON dbo.SubscriptionCheckpoints TO TestWriter WITH GRANT OPTION;
'@
    Invoke-Sql $name ($sql.Replace('@AllHostsStopped bit = 1','@AllHostsStopped bit = 0')) '51002'
    Invoke-Sql $name ($sql.Replace("@ExpectedDatabase sysname = N'$name'","@ExpectedDatabase sysname = N'WrongDatabase'")) '51000'
    $cases=@{
        leading='INSERT dbo.EventStoreEvents VALUES (2,0x01); INSERT dbo.EventStoreEventsPosition VALUES (1,2);'
        interior='INSERT dbo.EventStoreEvents VALUES (1,0x01),(3,0x03); INSERT dbo.EventStoreEventsPosition VALUES (1,3);'
        trailing='INSERT dbo.EventStoreEvents VALUES (1,0x01),(2,0x02); INSERT dbo.EventStoreEventsPosition VALUES (1,3);'
        counterLag='INSERT dbo.EventStoreEvents VALUES (1,0x01),(2,0x02); INSERT dbo.EventStoreEventsPosition VALUES (1,1);'
        missingCounter='INSERT dbo.EventStoreEvents VALUES (1,0x01);'
        duplicatePosition='INSERT dbo.EventStoreEvents VALUES (1,0x01),(1,0x02); INSERT dbo.EventStoreEventsPosition VALUES (1,2);'
        nonpositive='INSERT dbo.EventStoreEvents VALUES (0,0x01),(2,0x02); INSERT dbo.EventStoreEventsPosition VALUES (1,2);'
        emptyTrailing='INSERT dbo.EventStoreEventsPosition VALUES (1,1);'
        duplicateCounter='INSERT dbo.EventStoreEventsPosition VALUES (1,0),(1,0);'
    }
    foreach($case in $cases.GetEnumerator()){
        Invoke-Sql $source ('DELETE dbo.EventStoreEvents; DELETE dbo.EventStoreEventsPosition; '+$case.Value)
        Invoke-Sql $name $sql '51005'
        Invoke-Sql $name "IF OBJECT_ID('dbo.SubscriptionCheckpoints_Alpha19') IS NOT NULL OR (SELECT COUNT(*) FROM dbo.SubscriptionCheckpoints)<>1 THROW 52000,'Gap rejection mutated checkpoints.',1;"
        Write-Output "PASS rejected $($case.Key) before migration"
    }
    Invoke-Sql $source 'DELETE dbo.EventStoreEvents; DELETE dbo.EventStoreEventsPosition; INSERT dbo.EventStoreEventsPosition VALUES (1,0);'
    Invoke-Sql $name ($sql.Replace('    COMMIT;',"    THROW 52001,'Injected late failure',1;`n    COMMIT;")) '52001'
    Invoke-Sql $name "IF OBJECT_ID('dbo.SubscriptionCheckpoints_Alpha19') IS NOT NULL OR COL_LENGTH('dbo.SubscriptionCheckpoints','ProjectionKey') IS NOT NULL THROW 52002,'Rename was not rolled back.',1;"
    Invoke-Sql $source 'INSERT dbo.EventStoreEvents VALUES (1,0x0001FF),(2,0x0002FF); UPDATE dbo.EventStoreEventsPosition SET Value=2;'
    # An in-flight append must block the preflight, not be mistaken for a permanent gap.
    Invoke-Sql $name 'CREATE TABLE dbo.MigrationTestSignal(Released bit NOT NULL); INSERT dbo.MigrationTestSignal VALUES(0);'
    $writer = Start-Job -ArgumentList $Container,$source,$name -ScriptBlock {
        param($Container,$Database,$ControlDatabase)
        $statement = "SET XACT_ABORT ON; BEGIN TRAN; UPDATE dbo.EventStoreEventsPosition SET Value=3; INSERT dbo.EventStoreEvents VALUES(3,0x03); EXEC sys.sp_getapplock @Resource='Alpha20TestAppend',@LockMode='Exclusive',@LockOwner='Transaction'; DECLARE @Deadline datetime2=DATEADD(second,30,SYSUTCDATETIME()); WHILE NOT EXISTS(SELECT 1 FROM [$ControlDatabase].dbo.MigrationTestSignal WITH(READUNCOMMITTED) WHERE Released=1) BEGIN IF SYSUTCDATETIME()>@Deadline THROW 52022,'Writer release timed out',1; WAITFOR DELAY '00:00:00.05'; END; COMMIT;"
        $statement | docker exec -i $Container sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d "$1"' -- $Database
        if($LASTEXITCODE){throw 'Concurrent append failed'}
    }
    try {
        $ready=$false
        for($attempt=0;$attempt -lt 30;$attempt++) {
            try { Invoke-Sql $source "IF APPLOCK_TEST('public','Alpha20TestAppend','Exclusive','Session')<>0 THROW 52020,'Writer not ready',1;"; $ready=$true; break }
            catch { Start-Sleep -Milliseconds 100 }
        }
        if(!$ready){throw 'Concurrent append never reached its held transaction'}
        Invoke-Sql $name ($sql.Replace('SET LOCK_TIMEOUT 15000','SET LOCK_TIMEOUT 1000')) '1222'
        Invoke-Sql $name "IF OBJECT_ID('dbo.SubscriptionCheckpoints_Alpha19') IS NOT NULL THROW 52021,'Blocked preflight mutated checkpoints',1;"
        Invoke-Sql $name 'UPDATE dbo.MigrationTestSignal SET Released=1;'
        $null=Wait-Job $writer -Timeout 20
        if($writer.State -ne 'Completed'){throw 'Concurrent append did not complete'}
        $null=Receive-Job $writer -ErrorAction Stop
    } finally {
        Invoke-Sql $name 'UPDATE dbo.MigrationTestSignal SET Released=1;'
        $null=Wait-Job $writer -Timeout 20
        Remove-Job $writer -Force
    }
    Invoke-Sql $name $sql
    Invoke-Sql $name @'
IF NOT EXISTS(SELECT 1 FROM dbo.SubscriptionCheckpoints_Alpha19 WHERE Position=2 AND UpdatedAt='2026-01-01T00:00:00+00:00') THROW 52003,'Legacy data changed',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SubscriptionCheckpointsDefinitions WHERE DefinitionHash=REPLICATE('A',64)) THROW 52004,'Legacy definition changed',1;
IF EXISTS(SELECT 1 FROM dbo.SubscriptionCheckpoints) THROW 52005,'Fabricated binding',1;
IF EXISTS(SELECT p.grantee_principal_id,p.permission_name,p.state,t.object_id FROM sys.database_permissions p
    CROSS JOIN(VALUES(OBJECT_ID('dbo.SubscriptionCheckpoints')),(OBJECT_ID('dbo.SubscriptionCheckpointsReaders')))t(object_id)
    WHERE p.class=1 AND p.major_id=OBJECT_ID('dbo.SubscriptionCheckpoints_Alpha19') AND p.minor_id=0
    EXCEPT SELECT grantee_principal_id,permission_name,state,major_id FROM sys.database_permissions WHERE class=1 AND minor_id=0)
    THROW 52006,'Permission lost',1;
INSERT dbo.SubscriptionCheckpoints VALUES('Inline:app','Customer',REPLICATE('B',64),'fresh-generation',NULL,'11111111-1111-1111-1111-111111111111',NULL);
INSERT dbo.SubscriptionCheckpointsReaders VALUES('Inline:app','api','22222222-2222-2222-2222-222222222222','11111111-1111-1111-1111-111111111111');
UPDATE dbo.SubscriptionCheckpoints SET Position=2 WHERE BindingId='11111111-1111-1111-1111-111111111111' AND Position IS NULL;
IF @@ROWCOUNT<>1 THROW 52007,'Binding cannot advance',1;
UPDATE dbo.SubscriptionCheckpoints SET Position=3 WHERE BindingId='33333333-3333-3333-3333-333333333333' AND Position=2;
IF @@ROWCOUNT<>0 THROW 52008,'Stale binding advanced',1;
'@
    Invoke-Sql $name 'DELETE dbo.SubscriptionCheckpoints;' '547'
    Invoke-Sql $name 'UPDATE dbo.SubscriptionCheckpoints SET Position=-1;' '547'
    Invoke-Sql $name "UPDATE dbo.SubscriptionCheckpoints SET DefinitionHash=REPLICATE('g',64);" '547'
    Invoke-Sql $name $sql
    Invoke-Sql $name @"
IF (SELECT Position FROM dbo.SubscriptionCheckpoints)<>2 OR (SELECT COUNT(*) FROM dbo.SubscriptionCheckpointsReaders)<>1 THROW 52009,'Repeat changed bindings',1;
IF (SELECT COUNT(*) FROM [$source].dbo.EventStoreEvents WHERE Payload IN(0x0001FF,0x0002FF))<>2 THROW 52010,'Events changed',1;
SELECT t.name TableName,c.name ColumnName,ty.name TypeName,c.max_length,c.is_nullable,c.collation_name INTO #Actual
FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE t.name IN('SubscriptionCheckpoints','SubscriptionCheckpointsReaders');
SELECT t.name TableName,c.name ColumnName,ty.name TypeName,c.max_length,c.is_nullable,c.collation_name INTO #Expected
FROM [$fresh].sys.tables t JOIN [$fresh].sys.columns c ON c.object_id=t.object_id JOIN [$fresh].sys.types ty ON ty.user_type_id=c.user_type_id
WHERE t.name IN('SubscriptionCheckpoints','SubscriptionCheckpointsReaders');
IF EXISTS(SELECT * FROM #Expected EXCEPT SELECT * FROM #Actual) OR EXISTS(SELECT * FROM #Actual EXCEPT SELECT * FROM #Expected) THROW 52011,'Shipped schema mismatch',1;
"@
    Invoke-Sql $name 'ALTER TABLE dbo.SubscriptionCheckpoints ADD Unexpected int;'
    Invoke-Sql $name $sql '51012'
    Invoke-Sql $name @'
ALTER TABLE dbo.SubscriptionCheckpoints DROP COLUMN Unexpected;
DECLARE @Drop nvarchar(max)=(SELECT N'ALTER TABLE dbo.SubscriptionCheckpoints DROP CONSTRAINT '+QUOTENAME(name) FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID('dbo.SubscriptionCheckpoints') AND type='PK'); EXEC sys.sp_executesql @Drop;
'@
    Invoke-Sql $name $sql '51013'
    Write-Output 'PASS populated alpha19 migration, empty source, gap/allocator refusals, rollback, grants, binding lifetime, foreign key, checks, schema parity, rerun and drift rejection.'
} finally {
    if($name -notmatch '^Excalibur_Alpha20MigrationTest_[a-f0-9]{32}$'){throw 'Invalid cleanup target'}
    foreach($db in @($name,$source,$fresh)){Invoke-Sql master "IF DB_ID(N'$db') IS NOT NULL DROP DATABASE [$db];"}
}
