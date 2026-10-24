param([switch]$Diagnostic)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$runId = [guid]::NewGuid().ToString('N')
$database = "InlineNative_$runId"
$run = Join-Path $repo "artifacts/native-inline/$runId"
New-Item -ItemType Directory -Path $run -Force | Out-Null
$project = Join-Path $PSScriptRoot 'SqlElasticInlineAotProbe.csproj'
$publishArgs = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '-p:BuildExamplesAndTests=true',
    "-p:NuGetLockFilePath=obj/native-inline/$runId/packages.lock.json", '-p:RestorePackagesWithLockFile=true')
if ($Diagnostic) { $publishArgs += @('-p:ILLinkTreatWarningsAsErrors=false', '-p:TreatWarningsAsErrors=false') }
& dotnet @publishArgs > (Join-Path $run 'publish.log') 2>&1
if ($LASTEXITCODE -ne 0) { throw "Native publish failed. Evidence: $run" }
$binary = Join-Path $PSScriptRoot 'bin/Release/net10.0/win-x64/publish/SqlElasticInlineAotProbe.exe'
# Use only the existing, isolated AngularApp1.Local development services. Each run owns a new database/index.
$passwordLine = Get-Content 'D:/Dev/AngularNet/AngularApp1.Local/local-harness/.env' | Where-Object { $_ -match '^LOCAL_SQL_PASSWORD=' } | Select-Object -First 1
if (!$passwordLine) { throw 'Missing local SQL credential.' }
$password = $passwordLine.Substring('LOCAL_SQL_PASSWORD='.Length).Trim().Trim('"').Trim("'")
$env:INLINE_SQL = "Server=127.0.0.1,11433;User Id=sa;Password=$password;TrustServerCertificate=True;Database=$database"
$env:INLINE_INDEX = "inline-native-$runId"
$env:INLINE_ELASTIC_URI = 'http://127.0.0.1:19200'
function Invoke-Sql([string]$Db, [string]$Sql) {
    $Sql | docker exec -i angularapp1-local-sqlserver-1 sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d "$1"' -- $Db
    if ($LASTEXITCODE -ne 0) { throw 'SQL provisioning failed.' }
}
Invoke-Sql master "CREATE DATABASE [$database];"
foreach ($file in @('001_CreateEventStoreSchema.sql', '002_CreateSnapshotSchema.sql', '004_CreateSubscriptionCheckpointSchema.sql')) {
    Invoke-Sql $database (Get-Content -Raw (Join-Path $repo "src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/$file"))
}
function Run-Native([string]$Mode, [guid]$Id) {
    $log = Join-Path $run "$Mode-$Id.log"
    & $binary $Mode $Id > $log 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Native $Mode failed. See $log" }
    Get-Content $log | Select-String 'PASS|PENDING'
}
$first = [guid]::NewGuid()
Run-Native api $first
Run-Native api $first # Existing aggregate reload and positioned update.
Run-Native jobs ([guid]::NewGuid())
$worker = Start-Process -FilePath $binary -ArgumentList 'serve' -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run 'worker.log') -RedirectStandardError (Join-Path $run 'worker.err')
$writers = @()
try {
    foreach ($i in 1..8) {
        $id = [guid]::NewGuid()
        $process = Start-Process -FilePath $binary -ArgumentList @('api', $id) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run "concurrent-$id.log") -RedirectStandardError (Join-Path $run "concurrent-$id.err")
        $writers += @{ id=$id; process=$process }
    }
    foreach ($writer in $writers) {
        $writer.process.WaitForExit()
        if ($writer.process.ExitCode -ne 0) { throw "Concurrent native writer failed: $($writer.id)" }
        $document = Invoke-RestMethod "$env:INLINE_ELASTIC_URI/projections-$env:INLINE_INDEX/_doc/$($writer.id)"
        if ($document._version -ne 1 -or $document._source.count -ne 20) { throw 'Duplicate write or wrong concurrent fold.' }
    }
} finally {
    if (!$worker.HasExited) { Stop-Process -Id $worker.Id }
}
$pending = [guid]::NewGuid()
$recoveryLog = Join-Path $run 'recovery-worker.log'
$recovery = Start-Process -FilePath $binary -ArgumentList 'serve' -WindowStyle Hidden -PassThru -RedirectStandardOutput $recoveryLog -RedirectStandardError (Join-Path $run 'recovery-worker.err')
try {
    $workerReady = $false
    foreach ($i in 1..30) {
        if (Select-String -Path $recoveryLog -Pattern 'READY native recovery host' -Quiet) { $workerReady = $true; break }
        if ($recovery.HasExited) { throw 'Recovery worker failed at startup.' }
        Start-Sleep -Seconds 1
    }
    if (!$workerReady) { throw 'Recovery worker did not start.' }
    try {
        docker stop angularapp1-local-elasticsearch-1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not stop isolated Elasticsearch.' }
        Run-Native pending $pending
        $failedAttempts = 0
        foreach ($i in 1..60) {
            $failedAttempts = @(Select-String -Path $recoveryLog -Pattern 'Error dispatching events to async projection').Count
            if ($failedAttempts -ge 3) { break }
            if ($recovery.HasExited) { throw 'Recovery worker exited during outage.' }
            Start-Sleep -Seconds 1
        }
        if ($failedAttempts -lt 3) { throw 'Did not observe three recovery failures during outage.' }
    } finally {
        docker start angularapp1-local-elasticsearch-1 | Out-Null
    }
    $ready = $false
    foreach ($i in 1..60) {
        try { $null = Invoke-RestMethod "$env:INLINE_ELASTIC_URI/_cluster/health?wait_for_status=yellow&timeout=1s"; $ready=$true; break } catch { Start-Sleep -Seconds 1 }
    }
    if (!$ready) { throw 'Elasticsearch did not recover.' }
    if ($recovery.HasExited) { throw 'Recovery worker restarted or exited.' }
    Run-Native verify $pending # Read only: the original worker must repair the projection.
    $search = Invoke-RestMethod "$env:INLINE_ELASTIC_URI/projections-$env:INLINE_INDEX/_search?q=_id:$pending"
    if ($search.hits.total.value -ne 1) { throw 'Recovered document is not searchable.' }
    Write-Host 'PASS same-process recovery after three failed attempts.'
} finally {
    if (!$recovery.HasExited) { Stop-Process -Id $recovery.Id }
}
Run-Native recover $pending
Run-Native contracts ([guid]::NewGuid())
$verdict = @{ runtime='passed'; strictPublication= $(if ($Diagnostic) { 'not-certified' } else { 'passed' }); database=$database; index=$env:INLINE_INDEX; evidence=$run }
$verdict | ConvertTo-Json | Set-Content (Join-Path $run 'verdict.json')
Write-Host "PASS native runtime scenarios. Strict publication: $($verdict.strictPublication). Evidence: $run"
