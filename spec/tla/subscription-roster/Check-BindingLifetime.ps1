param(
    [string]$Java = "$env:USERPROFILE/tools/tla/jdk-21.0.12.1+1-jre/bin/java.exe",
    [string]$Jar = "$env:USERPROFILE/tools/tla/tla2tools.jar",
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Java) -or !(Test-Path -LiteralPath $Jar)) { throw 'TLC toolchain unavailable: nothing checked.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot ('../../../artifacts/binding-lifetime/' + [guid]::NewGuid().ToString('N')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$model = Join-Path $OutputDirectory 'BindingLifetime.tla'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BindingLifetime.tla') -Destination $model
$cases = @(
    @{name='current'; fence='TRUE'; snapshot='epochs'; absent='FALSE'; target=1; expect='hold'},
    @{name='current-zero-target'; fence='TRUE'; snapshot='epochs'; absent='FALSE'; target=0; expect='hold'},
    @{name='no-epoch-fence'; fence='FALSE'; snapshot='epochs'; absent='FALSE'; target=1; expect='InvEpoch'},
    @{name='no-roster-validation'; fence='TRUE'; snapshot='none'; absent='FALSE'; target=1; expect='InvRead'},
    @{name='membership-only-aba'; fence='TRUE'; snapshot='keys'; absent='FALSE'; target=1; expect='InvRead'},
    @{name='absence-as-zero'; fence='TRUE'; snapshot='epochs'; absent='TRUE'; target=0; expect='InvReceipt'},
    @{name='stale-writer-rebind'; fence='FALSE'; snapshot='epochs'; absent='FALSE'; target=1; expect='InvRebindFence'},
    @{name='no-release-fence'; fence='TRUE'; snapshot='epochs'; absent='FALSE'; target=1; expect='InvRelease'},
    @{name='reach-zero'; fence='TRUE'; snapshot='epochs'; absent='FALSE'; target=0; expect='NeverZero'},
    @{name='reach-stale-rebound'; fence='TRUE'; snapshot='epochs'; absent='FALSE'; target=1; expect='NeverStaleRebound'},
    @{name='reach-accepted'; fence='TRUE'; snapshot='epochs'; absent='FALSE'; target=1; expect='NeverAccepted'},
    @{name='reach-issued'; fence='TRUE'; snapshot='epochs'; absent='FALSE'; target=1; expect='NeverIssued'},
    @{name='reach-rebound'; fence='TRUE'; snapshot='epochs'; absent='FALSE'; target=1; expect='NeverRebound'},
    @{name='reach-stale-writer'; fence='TRUE'; snapshot='epochs'; absent='FALSE'; target=1; expect='NeverStaleAttempt'}
)
$results = [Collections.Generic.List[object]]::new()
$failed = $false
foreach ($case in $cases) {
    $directory = Join-Path $OutputDirectory $case.name
    [void][IO.Directory]::CreateDirectory($directory)
    $config = Join-Path $directory 'model.cfg'
    $invariants = if ($case.expect -eq 'hold') { 'InvEpoch InvRelease InvRebindFence InvCheckpoint InvRead InvReceipt' } else { $case.expect }
    @"
SPECIFICATION Spec
CONSTANTS
    FenceAdvance = $($case.fence)
    FenceRelease = $(if ($case.name -eq 'no-release-fence') {'FALSE'} else {'TRUE'})
    SnapshotMode = "$($case.snapshot)"
    AbsenceAsZero = $($case.absent)
    Target = $($case.target)
INVARIANTS $invariants
"@ | Set-Content -LiteralPath $config
    $log = Join-Path $directory 'tlc.log'
    $arguments = @('-Xmx2g','-XX:+UseParallelGC','-cp',$Jar,'tlc2.TLC','-deadlock','-workers','2',
        '-config',$config,'-metadir',(Join-Path $directory 'states'),$model)
    & $Java @arguments *> $log
    $nativeExit = $LASTEXITCODE
    $text = Get-Content -LiteralPath $log -Raw
    $passed = if ($case.expect -eq 'hold') { $nativeExit -eq 0 -and $text -match 'Model checking completed. No error has been found.' }
        else { $nativeExit -eq 12 -and $text -match ('Invariant ' + [regex]::Escape($case.expect) + ' is violated') }
    $counts = [regex]::Match($text, '([\d,]+) states generated, ([\d,]+) distinct states found')
    $results.Add(@{name=$case.name; expected=$case.expect; passed=$passed; exitCode=$nativeExit;
        modelSha256=(Get-FileHash $model).Hash; configSha256=(Get-FileHash $config).Hash;
        generated=$counts.Groups[1].Value; distinct=$counts.Groups[2].Value; command=@($Java)+$arguments; log=$log})
    Write-Output "$($case.name): passed=$passed; expected=$($case.expect); exit=$nativeExit; states=$($counts.Groups[2].Value)"
    if (!$passed) { $failed = $true; Get-Content -LiteralPath $log -Tail 18 }
    @{status=$(if ($failed) {'failed'} else {'incomplete'}); cases=@($results.ToArray())} |
        ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory 'verdict.json')
}
@{status=$(if ($failed) {'failed'} else {'passed'}); cases=@($results.ToArray());
    scope='Two keys, two epochs per key, paused writer, one reader, finite producer observation; no SQL engine, destination-write or generation-pointer proof.'} |
    ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory 'verdict.json')
Write-Output "Evidence: $OutputDirectory"
if ($failed) { exit 1 }
