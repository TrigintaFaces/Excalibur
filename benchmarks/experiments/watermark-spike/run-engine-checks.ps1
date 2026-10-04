[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [ValidateSet('pg-counterexamples', 'pg-feed', 'pg-crash', 'sql-commit', 'sql-kill', 'sql-crash', 'sql-feed')]
    [string[]]$Cases = @('pg-counterexamples', 'pg-feed', 'pg-crash', 'sql-commit', 'sql-kill', 'sql-crash'),
    [ValidateRange(1, 30)][int]$Repetitions = 1
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Cases.Count -eq 0 -or @($Cases | Select-Object -Unique).Count -ne $Cases.Count) { throw 'Specify at least one case, without duplicates.' }
$evidenceRoot = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $evidenceRoot) { throw 'Use a new evidence directory; prior evidence is never overwritten.' }
$null = New-Item -ItemType Directory -Path $evidenceRoot
$dockerPath = (Get-Command docker -ErrorAction Stop).Source
$runToken = [Guid]::NewGuid().ToString('N')
$images = @{
    pg = 'sha256:e17e86066e5ef83e0952a9347f5c792b7ece00972e2aa787a6986f471b3dd3d5'
    sql = 'sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090'
}
$stepNumber = 0
$caseDirectory = $evidenceRoot
$results = [Collections.Generic.List[object]]::new()

function Invoke-Docker {
    param([string[]]$DockerArgs, [string]$Label, [switch]$AllowFailure)
    $script:stepNumber++
    $start = [Diagnostics.ProcessStartInfo]::new($dockerPath)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($item in $DockerArgs) { $start.ArgumentList.Add($item) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $began = [DateTime]::UtcNow
    $null = $process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $timedOut = -not $process.WaitForExit(90000)
    if ($timedOut) {
        $process.Kill($true)
        if (-not $process.WaitForExit(5000)) { throw "Docker client for $Label did not terminate; step failed." }
    }
    if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 5000)) {
        $process.Dispose()
        throw "Docker output for $Label did not reach EOF; step failed."
    }
    $record = [ordered]@{
        label = $Label; startedUtc = $began.ToString('o'); endedUtc = [DateTime]::UtcNow.ToString('o')
        exitCode = $process.ExitCode; timedOut = $timedOut
        stdout = $stdout.GetAwaiter().GetResult(); stderr = $stderr.GetAwaiter().GetResult()
    }
    $process.Dispose()
    # Arguments can contain the disposable password. Retain outputs, never command arguments.
    $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $caseDirectory ('{0:D3}-{1}.json' -f $script:stepNumber, $Label))
    if ($timedOut -or ($record.exitCode -ne 0 -and -not $AllowFailure)) {
        throw "Docker step $Label failed (exit $($record.exitCode), timeout $timedOut); see retained evidence."
    }
    return [pscustomobject]$record
}

function Invoke-Sql {
    param([string[]]$SqlArgs, [string]$Label, [switch]$AllowFailure)
    $shell = 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U sa -C -b -l 5 -t 60 "$@"'
    Invoke-Docker -DockerArgs (@('exec', $containerName, 'bash', '-c', $shell, 'sqlcmd') + $SqlArgs) -Label $Label -AllowFailure:$AllowFailure
}

function Wait-Ready {
    param([string]$Database = 'master')
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    do {
        $probe = if ($engine -eq 'pg') {
            Invoke-Docker @('exec', $containerName, 'pg_isready', '-h', '127.0.0.1', '-U', 'spike', '-d', 'postgres') 'ready' -AllowFailure
        } else { Invoke-Sql @('-d', $Database, '-Q', 'SELECT 1') 'ready' -AllowFailure }
        if ($probe.exitCode -eq 0) { return }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Database readiness deadline exceeded.'
}

$inputDirectory = Join-Path $evidenceRoot 'inputs'
$null = New-Item -ItemType Directory -Path $inputDirectory
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object Extension -In '.sql', '.sh')
foreach ($source in $sources) {
    $content = [IO.File]::ReadAllText($source.FullName).Replace("`r`n", "`n")
    [IO.File]::WriteAllText((Join-Path $inputDirectory $source.Name), $content, [Text.UTF8Encoding]::new($false))
}
Get-FileHash -LiteralPath @($sources.FullName) | Select-Object Path, Hash |
    ConvertTo-Json | Set-Content (Join-Path $evidenceRoot 'source-hashes.json')
Get-ChildItem -LiteralPath $inputDirectory -File | Get-FileHash | Select-Object Path, Hash |
    ConvertTo-Json | Set-Content (Join-Path $evidenceRoot 'executed-input-hashes.json')
Get-FileHash -LiteralPath $PSCommandPath | Select-Object Path, Hash |
    ConvertTo-Json | Set-Content (Join-Path $evidenceRoot 'runner-hash.json')
git -C $PSScriptRoot rev-parse HEAD | Set-Content (Join-Path $evidenceRoot 'revision.txt')
if ($LASTEXITCODE -ne 0) { throw 'Cannot record repository revision.' }
@{ cases=$Cases; repetitions=$Repetitions; images=$images; runToken=$runToken } |
    ConvertTo-Json -Depth 6 | Set-Content (Join-Path $evidenceRoot 'parameters.json')

try {
    foreach ($iteration in 1..$Repetitions) {
        foreach ($case in $Cases) {
            $caseDirectory = Join-Path $evidenceRoot ('{0:D2}-{1}' -f $iteration, $case)
            $null = New-Item -ItemType Directory -Path $caseDirectory
            $engine = if ($case.StartsWith('pg-')) { 'pg' } else { 'sql' }
            $containerName = 'codex-wm-' + [Guid]::NewGuid().ToString('N').Substring(0, 16)
            $containerId = $null
            $passed = $false
            Write-Host "Starting $case repetition $iteration/$Repetitions"
            try {
                $runArgs = @('run', '-d', '--name', $containerName, '--label', "codex.watermark.run=$runToken",
                    '--network', 'none', '--cpus', '2', '--mount', "type=bind,source=$inputDirectory,target=/experiment,readonly")
                if ($engine -eq 'pg') {
                    $runArgs += @('--memory','512m','-e','POSTGRES_USER=spike','-e','POSTGRES_HOST_AUTH_METHOD=trust',
                        $images.pg,'-c','wal_level=logical','-c','checkpoint_timeout=1h')
                } else {
                    $password = 'Spike-' + [Guid]::NewGuid().ToString('N') + '!aZ9'
                    $runArgs += @('--memory','3g','-e','ACCEPT_EULA=Y','-e','MSSQL_PID=Developer','-e',"MSSQL_SA_PASSWORD=$password",$images.sql)
                }
                $created = Invoke-Docker $runArgs 'create'
                $containerId = $created.stdout.Trim()
                if ($containerId -notmatch '^[a-f0-9]{64}$') { throw 'Unexpected container identity.' }
                $containerName = $containerId
                $runArgs = $null
                Wait-Ready
                if ($case -eq 'pg-counterexamples' -or $case -eq 'pg-feed') {
                    $file = if ($case -eq 'pg-feed') { 'postgres-logical-feed.sql' } else { 'postgres-counterexamples.sql' }
                    $null = Invoke-Docker @('exec',$containerName,'psql','-X','-U','spike','-d','postgres','-f',"/experiment/$file") 'assertions'
                } elseif ($case -eq 'sql-feed') {
                    $null = Invoke-Docker @('exec',$containerName,'bash','/experiment/sqlserver-resolved-feed.sh') 'assertions'
                    $null = Invoke-Docker @('exec','-d',$containerName,'bash','-c','export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U sa -C -b -H feed-crash-writer -i /experiment/sqlserver-feed-crash-writer.sql > /tmp/feed-crash-writer.log 2>&1') 'writer'
                    $writerReadyQuery = "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.dm_exec_sessions s JOIN sys.dm_exec_requests r ON s.session_id=r.session_id WHERE s.host_name='feed-crash-writer' AND r.wait_type='WAITFOR' AND r.open_transaction_count>0"
                    $writerReady = $false
                    foreach ($attempt in 1..30) {
                        $barrier = Invoke-Sql @('-h','-1','-W','-Q',$writerReadyQuery) 'writer-ready'
                        if ($barrier.stdout.Trim() -eq '1') { $writerReady=$true; break }
                        Start-Sleep -Milliseconds 200
                    }
                    if (-not $writerReady) { throw 'Actual feed crash-writer barrier not established.' }
                    $null = Invoke-Sql @('-d','WatermarkFeedSpike','-Q',"IF NOT EXISTS(SELECT 1 FROM Ledger WITH(READUNCOMMITTED) WHERE EventId=11) OR NOT EXISTS(SELECT 1 FROM Outbox WITH(READUNCOMMITTED) WHERE EventId=11) THROW 51000, 'Incomplete crash barrier', 1;") 'feed-crash-barrier'
                    $null = Invoke-Docker @('exec',$containerName,'cat','/tmp/feed-crash-writer.log') 'writer-evidence'
                    $null = Invoke-Docker @('kill','--signal','KILL',$containerName) 'crash'
                    $crashState = Invoke-Docker @('inspect',$containerName,'--format','{{.State.Status}}|{{.State.ExitCode}}|{{.State.OOMKilled}}') 'crash-state'
                    if ($crashState.stdout.Trim() -ne 'exited|137|false') { throw 'Expected SIGKILL state without OOM was not observed.' }
                    $null = Invoke-Docker @('start',$containerName) 'restart'
                    Wait-Ready -Database 'WatermarkFeedSpike'
                    $null = Invoke-Sql @('-i','/experiment/sqlserver-feed-recovery.sql') 'feed-recovery'
                } elseif ($case -eq 'sql-commit' -or $case -eq 'sql-kill') {
                    $null = Invoke-Docker @('exec',$containerName,'bash','/experiment/sqlserver-rowversion.sh',$case.Substring(4)) 'assertions'
                } else {
                    if ($engine -eq 'pg') {
                        $null = Invoke-Docker @('exec',$containerName,'psql','-X','-U','spike','-d','postgres','-f','/experiment/postgres-crash-setup.sql') 'setup'
                        $null = Invoke-Docker @('exec','-d',$containerName,'bash','-c','PGAPPNAME=watermark-crash-writer psql -X -U spike -d postgres -f /experiment/postgres-crash-writer.sql > /tmp/crash-writer.log 2>&1') 'writer'
                        $barrierQuery = "SELECT count(*) FROM pg_stat_activity WHERE application_name='watermark-crash-writer' AND state='active' AND wait_event='PgSleep' AND xact_start IS NOT NULL"
                        $ready = $false
                        foreach ($attempt in 1..30) {
                            $barrier = Invoke-Docker @('exec',$containerName,'psql','-X','-At','-U','spike','-d','postgres','-c',$barrierQuery) 'barrier'
                            if ($barrier.stdout.Trim() -eq '1') { $ready=$true; break }
                            Start-Sleep -Milliseconds 200
                        }
                        if (-not $ready) { throw 'Uncommitted writer barrier not established.' }
                    } else {
                        $null = Invoke-Sql @('-i','/experiment/sqlserver-crash-setup.sql') 'setup'
                        $null = Invoke-Docker @('exec','-d',$containerName,'bash','-c','export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U sa -C -b -H watermark-crash-writer -i /experiment/sqlserver-crash-writer.sql > /tmp/crash-writer.log 2>&1') 'writer'
                        # Poll only observation; execute the mutating barrier exactly once.
                        $writerReadyQuery = "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.dm_exec_sessions s JOIN sys.dm_exec_requests r ON s.session_id=r.session_id WHERE s.host_name='watermark-crash-writer' AND r.wait_type='WAITFOR' AND r.open_transaction_count>0"
                        $ready = $false
                        foreach ($attempt in 1..30) {
                            $barrier = Invoke-Sql @('-h','-1','-W','-Q',$writerReadyQuery) 'writer-ready'
                            if ($barrier.stdout.Trim() -eq '1') { $ready=$true; break }
                            Start-Sleep -Milliseconds 200
                        }
                        if (-not $ready) { throw 'Uncommitted writer barrier not established.' }
                        $null = Invoke-Sql @('-i','/experiment/sqlserver-crash-barrier.sql') 'barrier'
                    }
                    $null = Invoke-Docker @('exec',$containerName,'cat','/tmp/crash-writer.log') 'writer-evidence'
                    $null = Invoke-Docker @('kill','--signal','KILL',$containerName) 'crash'
                    $crashState = Invoke-Docker @('inspect',$containerName,'--format','{{.State.Status}}|{{.State.ExitCode}}|{{.State.OOMKilled}}') 'crash-state'
                    if ($crashState.stdout.Trim() -ne 'exited|137|false') { throw 'Expected SIGKILL exit state without OOM was not observed.' }
                    $null = Invoke-Docker @('start',$containerName) 'restart'
                    Wait-Ready -Database 'WatermarkCrash'
                    if ($engine -eq 'pg') {
                        $null = Invoke-Docker @('exec',$containerName,'psql','-X','-U','spike','-d','postgres','-f','/experiment/postgres-crash-verify.sql') 'recovery'
                    } else { $null = Invoke-Sql @('-i','/experiment/sqlserver-crash-verify.sql') 'recovery' }
                }
                $passed = $true
            } finally {
                $cleanupPassed = $false
                try {
                if (-not $containerId) {
                    # A timed-out create may still have created our uniquely named container.
                    $lookup = Invoke-Docker @('ps','-aq','--no-trunc','--filter',"name=^/$containerName$",'--filter',"label=codex.watermark.run=$runToken") 'cleanup-lookup'
                    $found = $lookup.stdout.Trim()
                    if ($found) {
                        if ($found -notmatch '^[a-f0-9]{64}$') { throw 'Ambiguous cleanup identity; resources preserved.' }
                        $containerId = $found
                    } else {
                        throw 'No owned container observed after ambiguous creation; cleanup completion cannot be certified.'
                    }
                }
                if ($containerId) {
                    try {
                        $null = Invoke-Docker @('logs','--tail','80',$containerId) 'server-log'
                    } catch {
                        $passed = $false
                        $_.ToString() | Set-Content (Join-Path $caseDirectory 'diagnostic-failure.txt')
                    }
                    $identity = Invoke-Docker @('inspect',$containerId,'--format','{{.Id}}|{{index .Config.Labels "codex.watermark.run"}}|{{.Config.Image}}|{{json .Mounts}}') 'identity'
                    if (-not $identity.stdout.StartsWith("$containerId|$runToken|")) { throw 'Cleanup ownership check failed; container preserved.' }
                    $null = Invoke-Docker @('rm','-f','-v',$containerId) 'cleanup'
                }
                $cleanupPassed = $true
                } finally {
                    $results.Add([pscustomobject]@{ case=$case; repetition=$iteration; assertionsPassed=$passed; cleanupPassed=$cleanupPassed; passed=($passed -and $cleanupPassed); containerId=$containerId })
                }
            }
            if (-not $passed) { throw 'Case failed evidence collection; see retained diagnostics.' }
            Write-Host "Passed $case repetition $iteration/$Repetitions"
        }
    }
} finally {
    @{ expected=$Cases.Count*$Repetitions; completed=$results.Count; success=($results.Count -eq $Cases.Count*$Repetitions -and @($results | Where-Object { -not $_.passed }).Count -eq 0); results=@($results.ToArray()) } |
        ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidenceRoot 'results.json')
}
