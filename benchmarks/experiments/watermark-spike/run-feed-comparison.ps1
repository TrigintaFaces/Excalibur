[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [Parameter(Mandatory)][string]$PlanFile,
    [Parameter(Mandatory)][string]$ApplicationDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath($EvidenceDirectory)
$app = [IO.Path]::GetFullPath($ApplicationDirectory)
$plan = Get-Content -LiteralPath $PlanFile -Raw | ConvertFrom-Json
if (Test-Path -LiteralPath $root) { throw 'Use a new evidence directory.' }
if (-not (Test-Path -LiteralPath (Join-Path $app 'Excalibur.Benchmarks.dll'))) { throw 'Compiled comparison application missing.' }
if ($plan.cells.Count -eq 0) { throw 'Empty experiment plan.' }
$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($cell in $plan.cells) {
    if ($cell.id -notmatch '^[a-zA-Z0-9_-]+$' -or -not $ids.Add($cell.id) -or $cell.arm -notin @('gapless','watermark') -or
        $cell.writers -lt 1 -or $cell.writers -gt 64 -or $cell.batch -lt 1 -or $cell.batch -gt 100 -or
        $cell.rate -lt 1 -or $cell.rate -gt 100000 -or $cell.requests -lt 1 -or $cell.requests -gt 2000000) { throw 'Invalid experiment cell.' }
}
$null = New-Item -ItemType Directory -Path $root
Copy-Item -LiteralPath $PlanFile -Destination (Join-Path $root 'plan.json')
Get-FileHash -LiteralPath $PSCommandPath | Select-Object Path,Hash | ConvertTo-Json | Set-Content (Join-Path $root 'runner-hash.json')
Get-ChildItem -LiteralPath $app -File -Recurse | Get-FileHash | Select-Object Path,Hash | ConvertTo-Json | Set-Content (Join-Path $root 'application-hashes.json')
git -C $PSScriptRoot rev-parse HEAD | Set-Content (Join-Path $root 'revision.txt')
if ($LASTEXITCODE -ne 0) { throw 'Cannot record revision.' }
$token = [Guid]::NewGuid().ToString('N')
$network = 'wm-perf-' + $token
$serverName = 'wm-sql-' + $token
$clientName = 'wm-client-' + $token
$sqlImage = 'sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090'
$sdkImage = 'mcr.microsoft.com/dotnet/sdk:10.0.400'
$docker = (Get-Command docker).Source
$resourceProbe = 'set -e; if [ -f /sys/fs/cgroup/cpu.stat ]; then echo cgroup-v2; cat /sys/fs/cgroup/cpu.stat /sys/fs/cgroup/memory.events; else echo cgroup-v1; echo cpu_usage_nanoseconds; cat /sys/fs/cgroup/cpuacct/cpuacct.usage; cat /sys/fs/cgroup/cpu/cpu.stat; echo memory_failcnt; cat /sys/fs/cgroup/memory/memory.failcnt; fi'
$step = 0
$serverId = $null
$clientId = $null
$networkId = $null
$networkAttempted = $false
$serverAttempted = $false
$clientAttempted = $false
$results = [Collections.Generic.List[object]]::new()
$cleanupErrors = [Collections.Generic.List[string]]::new()
$failure = $null

function Invoke-Docker {
    param([string[]]$Arguments,[string]$Label,[int]$TimeoutSeconds=90,[switch]$AllowFailure)
    $script:step++
    $psi = [Diagnostics.ProcessStartInfo]::new($docker)
    $psi.UseShellExecute=$false; $psi.CreateNoWindow=$true
    $psi.RedirectStandardOutput=$true; $psi.RedirectStandardError=$true
    foreach ($argument in $Arguments) { $psi.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo=$psi
    $began=[DateTime]::UtcNow
    $null=$process.Start()
    $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
    $timeout=-not $process.WaitForExit($TimeoutSeconds*1000)
    if ($timeout) { $process.Kill($true); if (-not $process.WaitForExit(5000)) { throw 'Docker client did not terminate.' } }
    if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr),5000)) { throw 'Docker output did not reach EOF.' }
    $record=[ordered]@{label=$Label;startedUtc=$began.ToString('o');endedUtc=[DateTime]::UtcNow.ToString('o');exitCode=$process.ExitCode;timedOut=$timeout;stdout=$stdout.Result;stderr=$stderr.Result}
    $process.Dispose()
    # Never retain arguments: creation includes disposable credentials.
    $record | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root ('{0:D4}-{1}.json' -f $script:step,$Label))
    if ($timeout -or ($record.exitCode -ne 0 -and -not $AllowFailure)) { throw "Docker step $Label failed; see evidence." }
    return [pscustomobject]$record
}

try {
    $null=Invoke-Docker @('info','--format','{{json .}}') 'docker-host'
    $null=Invoke-Docker @('ps','--format','{{.ID}} {{.Image}} {{.Names}}') 'other-containers'
    $networkAttempted=$true
    $networkId=(Invoke-Docker @('network','create','--internal','--label',"codex.watermark.run=$token",$network) 'network-create').stdout.Trim()
    $password='Spike-'+[Guid]::NewGuid().ToString('N')+'!aZ9'
    $serverAttempted=$true
    $serverId=(Invoke-Docker @('run','-d','--name',$serverName,'--label',"codex.watermark.run=$token",'--network',$networkId,'--network-alias','feed-sql','--cpus','2','--memory','3g','-e','ACCEPT_EULA=Y','-e','MSSQL_PID=Developer','-e',"MSSQL_SA_PASSWORD=$password",$sqlImage) 'server-create').stdout.Trim()
    if ($serverId -notmatch '^[a-f0-9]{64}$') { throw 'Invalid server identity.' }
    $ready=$false
    foreach ($attempt in 1..90) {
        $probe=Invoke-Docker @('exec',$serverId,'bash','-c','export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U sa -C -b -l 2 -Q "SELECT 1"') 'ready' -AllowFailure
        if ($probe.exitCode -eq 0) { $ready=$true; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $ready) { throw 'SQL readiness deadline exceeded.' }
    $connection="Server=feed-sql;User ID=sa;Password=$password;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10;Application Name=ResolvedFeedSpike"
    $clientAttempted=$true
    $clientId=(Invoke-Docker @('run','-d','--name',$clientName,'--label',"codex.watermark.run=$token",'--network',$networkId,'--cpus','2','--memory','2g','--mount',"type=bind,source=$app,target=/app,readonly",'--mount',"type=bind,source=$root,target=/evidence",'-e','WATERMARK_SPIKE_DISPOSABLE=1','-e',"BENCHMARK_SQL_CONNECTIONSTRING=$connection",$sdkImage,'sleep','infinity') 'client-create').stdout.Trim()
    if ($clientId -notmatch '^[a-f0-9]{64}$') { throw 'Invalid client identity.' }
    foreach ($id in @($serverId,$clientId)) {
        $null=Invoke-Docker @('inspect',$id,'--format','{{.Id}}|{{.Image}}|cpus={{.HostConfig.NanoCpus}}|memory={{.HostConfig.Memory}}|{{json .Mounts}}') 'environment'
    }
    foreach ($cell in $plan.cells) {
        Write-Host "Running $($cell.id): $($cell.arm), $($cell.writers) writers, batch $($cell.batch), $($cell.rate) requests/sec"
        $null=Invoke-Docker @('stats','--no-stream','--format','{{json .}}',$serverId,$clientId) ($cell.id+'-resources-before')
        $null=Invoke-Docker @('exec',$serverId,'sh','-c',$resourceProbe) ($cell.id+'-server-cost-before')
        $null=Invoke-Docker @('exec',$clientId,'sh','-c',$resourceProbe) ($cell.id+'-client-cost-before')
        $seconds=[int][Math]::Ceiling($cell.requests/[double]$cell.rate)+180
        $fault=if ($cell.PSObject.Properties.Name -contains 'fault') { [string]$cell.fault } else { '' }
        $run=Invoke-Docker @('exec','-e',"WATERMARK_SPIKE_TEST_FAULT=$fault",$clientId,'dotnet','/app/Excalibur.Benchmarks.dll','feed-spike',$cell.arm,[string]$cell.writers,[string]$cell.batch,[string]$cell.rate,[string]$cell.requests,"/evidence/$($cell.id).json") ($cell.id+'-run') -TimeoutSeconds $seconds -AllowFailure
        $null=Invoke-Docker @('stats','--no-stream','--format','{{json .}}',$serverId,$clientId) ($cell.id+'-resources-after')
        $null=Invoke-Docker @('exec',$serverId,'sh','-c',$resourceProbe) ($cell.id+'-server-cost-after')
        $null=Invoke-Docker @('exec',$clientId,'sh','-c',$resourceProbe) ($cell.id+'-client-cost-after')
        $path=Join-Path $root ($cell.id+'.json')
        $data=if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } else { $null }
        $results.Add([ordered]@{id=$cell.id;exitCode=$run.exitCode;recordPresent=($null -ne $data);complete=($null -ne $data -and $data.complete)})
        if ($null -eq $data -or -not $data.cleanupPassed) { throw 'Missing evidence or failed database cleanup; stopping experiment.' }
        # Overload is evidence, not a pass. Preserve failed cells and continue the frozen plan.
    }
} catch { $failure=$_.Exception.Message }
finally {
    if ($serverId) { try { $null=Invoke-Docker @('logs',$serverId) 'server-log' -AllowFailure } catch { $cleanupErrors.Add($_.Exception.Message) } }
    $cleanupClient=if ($clientId -match '^[a-f0-9]{64}$') {$clientId} elseif ($clientAttempted) {$clientName} else {$null}
    $cleanupServer=if ($serverId -match '^[a-f0-9]{64}$') {$serverId} elseif ($serverAttempted) {$serverName} else {$null}
    foreach ($id in @($cleanupClient,$cleanupServer)) {
        if (-not $id) { continue }
        try {
            $owner=(Invoke-Docker @('inspect',$id,'--format','{{index .Config.Labels "codex.watermark.run"}}') 'cleanup-owner').stdout.Trim()
            if ($owner -ne $token) { throw 'Container ownership mismatch.' }
            $null=Invoke-Docker @('rm','-f','-v',$id) 'cleanup-container'
        } catch { $cleanupErrors.Add($_.Exception.Message) }
    }
    if ($networkAttempted) {
        if ($networkId -notmatch '^[a-f0-9]{64}$') { $networkId=$network }
        try {
            $owner=(Invoke-Docker @('network','inspect',$networkId,'--format','{{index .Labels "codex.watermark.run"}}') 'network-owner').stdout.Trim()
            if ($owner -ne $token) { throw 'Network ownership mismatch.' }
            $null=Invoke-Docker @('network','rm',$networkId) 'cleanup-network'
        } catch { $cleanupErrors.Add($_.Exception.Message) }
    }
    @{runnerCompleted=($null -eq $failure -and $cleanupErrors.Count -eq 0 -and $results.Count -eq $plan.cells.Count);failure=$failure;cleanupErrors=$cleanupErrors.ToArray();cells=$results.ToArray();qualification='Local research kernel experiment, shared host; not production capacity'} |
        ConvertTo-Json -Depth 10 | Set-Content (Join-Path $root 'results.json')
}
if ($failure -or $cleanupErrors.Count) { throw 'Experiment runner failed; retained evidence is not a pass.' }
