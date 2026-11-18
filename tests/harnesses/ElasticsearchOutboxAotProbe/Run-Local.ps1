# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param(
    [ValidateSet('', 'linux-x64', 'win-x64')][string]$Runtime = '',
    [string]$ArtifactsParent = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$Runtime) { $Runtime = if ($IsWindows) { 'win-x64' } elseif ($IsLinux) { 'linux-x64' } else { throw 'This runner supports Linux and Windows x64 only.' } }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$label = [guid]::NewGuid().ToString('N')
$parent = if ($ArtifactsParent) { [IO.Path]::GetFullPath($ArtifactsParent) } else { Join-Path $repo 'artifacts/elasticsearch-outbox-aot' }
$repoPrefix = $repo.TrimEnd([char[]]@('/','\')) + [IO.Path]::DirectorySeparatorChar
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd([char[]]@('/','\'))
if (($parent -eq $repo -or $parent.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) -and
    $parent -ne $artifactRoot -and !$parent.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'An in-repository artifact parent must be under artifacts; source/lock fence exclusions are not allowed.'
}
$null = New-Item -ItemType Directory -Path $parent -Force
$evidence = Join-Path $parent $label
$ownObj = (Join-Path $evidence 'obj') + [IO.Path]::DirectorySeparatorChar
$ownBin = (Join-Path $evidence 'bin') + [IO.Path]::DirectorySeparatorChar
$null = New-Item -ItemType Directory -Path $evidence
$project = Join-Path $PSScriptRoot 'ElasticsearchOutboxAotProbe.csproj'
$container = "es-outbox-native-$label"
$containerAttempted = $false
$result = [ordered]@{ artifactParent = $parent; ownIntermediate = $ownObj; ownOutput = $ownBin; runtime = $Runtime; publish = 'not-run'; native = 'not-run'; cleanup = 'not-run'; sourceStable = $false }
$environmentNames = @('BuildExamplesAndTests','NuGetLockFilePath','ElasticsearchOutboxProbePublishDir','ElasticsearchOutboxProbeIntermediateDir','ElasticsearchOutboxProbeOutputDir','DOTNET_CLI_UI_LANGUAGE','SolutionDir','SolutionName','UseSharedCompilation','MSBUILDDISABLENODEREUSE','DOTNET_CLI_USE_MSBUILD_SERVER','PROBE_RUN_ID','PROBE_CONNECTION')
$previous = @{}
foreach ($name in $environmentNames) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
$pythonName = if ($IsWindows) { 'python' } else { 'python3' }
$python = Get-Command $pythonName -CommandType Application -ErrorAction Stop | Select-Object -First 1
$owner = Join-Path $PSScriptRoot 'owned_process.py'
function Invoke-Bounded([string]$File, [string[]]$Arguments, [int]$Seconds, [string]$Stem, [switch]$AllowFailure) {
    $log = Join-Path $evidence "$Stem.log"
    $receipt = Join-Path $evidence "$Stem.process.json"
    $PSNativeCommandUseErrorActionPreference = $false
    & $python.Source $owner --repo $repo --cwd $repo --timeout $Seconds --log $log --receipt $receipt -- $File @Arguments
    $wrapperExit = $LASTEXITCODE
    if (!(Test-Path -LiteralPath $receipt -PathType Leaf)) { throw "Owner did not produce $Stem receipt (wrapper exit $wrapperExit)." }
    $record = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
    $text = if (Test-Path -LiteralPath $log) { [IO.File]::ReadAllText($log) } else { '' }
    if (!$record.cleanup_verified -or $record.timed_out -or $record.PSObject.Properties['execution_error']) {
        throw "$Stem ownership/timeout refusal; partial merged stdout/stderr and receipt retained at $evidence."
    }
    if (($record.exit_code -ne 0 -or $wrapperExit -ne 0) -and !$AllowFailure) { throw "$Stem failed (child $($record.exit_code), wrapper $wrapperExit). Evidence: $evidence" }
    return [pscustomobject]@{ stdout=$text; stderr=''; exitCode=$record.exit_code }
}
function Get-SourceFence {
    $tracked = & git -C $repo ls-files -- src eng global.json Directory.Build.props Directory.Build.targets Directory.Packages.props .github/workflows/aot-validation.yml tests/harnesses/ElasticsearchOutboxAotProbe '**/packages.lock.json'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source/lock fence.' }
    $untracked = & git -C $repo ls-files --others --exclude-standard -- src eng tests/harnesses/ElasticsearchOutboxAotProbe
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate untracked source fence.' }
    $paths = @(@($tracked) + @($untracked) | ForEach-Object { Join-Path $repo $_ }) + @(Get-ChildItem -LiteralPath $PSScriptRoot -File | ForEach-Object FullName)
    $fence = [ordered]@{}
    foreach ($path in $paths | Sort-Object -Unique) { $fence[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    return $fence
}
$before = Get-SourceFence
$before | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $evidence 'source-before.json')
try {
    $env:BuildExamplesAndTests = 'true'
    $env:DOTNET_CLI_UI_LANGUAGE = 'en-US'
    $env:ElasticsearchOutboxProbeIntermediateDir = $ownObj
    $env:ElasticsearchOutboxProbeOutputDir = $ownBin
    $env:NuGetLockFilePath = "obj/elasticsearch-outbox-native/$label/packages.lock.json"
    $env:ElasticsearchOutboxProbePublishDir = (Join-Path $evidence 'native') + [IO.Path]::DirectorySeparatorChar
    $env:SolutionDir = $repo + [IO.Path]::DirectorySeparatorChar; $env:SolutionName = 'Excalibur'
    $env:UseSharedCompilation = 'false'; $env:MSBUILDDISABLENODEREUSE = '1'; $env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
    $properties = Invoke-Bounded 'dotnet' @('msbuild', $project, '-nologo', '-p:Configuration=Release', "-p:RuntimeIdentifier=$Runtime", '-getProperty:NETCoreSdkVersion,PublishAot,IsTestProject,IsPackable,BuildExamplesAndTests,SkipProjectBuild,JsonSerializerIsReflectionEnabledByDefault,TreatWarningsAsErrors,ILLinkTreatWarningsAsErrors,SuppressTrimAnalysisWarnings,TrimmerSingleWarn,NoWarn,DirectoryBuildPropsPath,BaseIntermediateOutputPath,MSBuildProjectExtensionsPath,BaseOutputPath,OutputPath') 60 'properties'
    $p = ($properties.stdout | ConvertFrom-Json).Properties
    $pinnedSdk = (Get-Content -LiteralPath (Join-Path $repo 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if ($p.NETCoreSdkVersion -ne $pinnedSdk) { throw 'Effective SDK differs from global.json; the default diagnostic allowance must be reviewed.' }
    foreach ($name in @('PublishAot','BuildExamplesAndTests','TreatWarningsAsErrors','ILLinkTreatWarningsAsErrors')) { if ($p.$name -ne 'true') { throw "Required property $name is not true." } }
    foreach ($name in @('IsTestProject','IsPackable','JsonSerializerIsReflectionEnabledByDefault','SuppressTrimAnalysisWarnings','TrimmerSingleWarn')) { if ($p.$name -ne 'false') { throw "Required property $name is not false." } }
    $ilWaivers = @($p.NoWarn -split '[;,\s]+' | Where-Object { $_ -match '^IL' })
    if ($p.SkipProjectBuild -eq 'true' -or @($ilWaivers | Where-Object { $_ -cne 'IL2121' }).Count -ne 0) { throw 'Probe is skipped or suppresses an IL diagnostic other than the pinned SDK defaultIL2121.' }
    foreach ($name in @('BaseIntermediateOutputPath','MSBuildProjectExtensionsPath')) {
        if ([IO.Path]::GetFullPath($p.$name).TrimEnd([char[]]@('/','\')) -ne [IO.Path]::GetFullPath($ownObj).TrimEnd([char[]]@('/','\'))) { throw "Harness-only intermediate property $name did not evaluate to the fresh evidence obj path." }
    }
    $expectedBin = [IO.Path]::GetFullPath($ownBin).TrimEnd([char[]]@('/','\'))
    if ([string]::IsNullOrWhiteSpace($p.BaseOutputPath) -or [IO.Path]::GetFullPath($p.BaseOutputPath).TrimEnd([char[]]@('/','\')) -ne $expectedBin) {
        throw 'Harness-only BaseOutputPath did not evaluate to the fresh evidence bin path.'
    }
    if ([string]::IsNullOrWhiteSpace($p.OutputPath) -or ![IO.Path]::GetFullPath($p.OutputPath).StartsWith($expectedBin + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Harness-only OutputPath did not evaluate inside the fresh evidence bin path.'
    }
    $result.effectiveBaseOutputPath = $p.BaseOutputPath
    $result.effectiveOutputPath = $p.OutputPath
    if ([IO.Path]::GetFullPath($p.DirectoryBuildPropsPath) -ne [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Directory.Build.props'))) { throw 'Probe did not isolate its props from test SDK dependencies.' }
    $controls = Invoke-Bounded $python.Source @((Join-Path $PSScriptRoot 'owned_process.test.py'),'--repo',$repo) 60 'owner-controls'
    if (([regex]::Matches($controls.stdout, '(?m)^OWNED-CONTROLS-COMPLETE checks=4\r?$')).Count -ne 1) { throw 'Ownership controls did not execute exactly four successful cases.' }
    if (Test-Path -LiteralPath $env:ElasticsearchOutboxProbePublishDir) { throw 'Native publish directory must be fresh.' }
    $publishStarted = [DateTime]::UtcNow
    $result.publish = 'running'
    Invoke-Bounded 'pwsh' @('-NoLogo','-NoProfile','-File',(Join-Path $repo 'eng/ci/Invoke-AotPublishValidation.ps1'),'-ProjectPath',$project,'-Configuration','Release','-Runtime',$Runtime,'-OutputPath',(Join-Path $evidence 'validation')) 1500 'publish' | Out-Null
    $publishFinished = [DateTime]::UtcNow
    $publishLog = Join-Path $evidence 'validation/aot-publish.log'
    if (!(Test-Path -LiteralPath $publishLog) -or [IO.File]::ReadAllText($publishLog) -notmatch 'Generating native code') { throw 'Official raw publish log did not prove actual native generation.' }
    $objectName = 'ElasticsearchOutboxAotProbe' + $(if ($Runtime -eq 'win-x64') { '.obj' } else { '.o' })
    $objects = @(Get-ChildItem -LiteralPath $ownObj -Recurse -File | Where-Object { $_.Name -eq $objectName -and $_.Directory.Name -eq 'native' })
    if ($objects.Count -eq 0) { throw 'Actual harness native object is missing in fresh own intermediates.' }
    foreach ($object in $objects) {
        if ($object.CreationTimeUtc -lt $publishStarted -or $object.CreationTimeUtc -gt $publishFinished -or $object.LastWriteTimeUtc -lt $publishStarted -or $object.LastWriteTimeUtc -gt $publishFinished) { throw 'Native object was not created/written inside this publish window.' }
    }
    $result.nativeObjectHashes = @($objects | ForEach-Object { @{ path=$_.FullName; sha256=(Get-FileHash -LiteralPath $_.FullName).Hash; creationUtc=$_.CreationTimeUtc.ToString('O'); writeUtc=$_.LastWriteTimeUtc.ToString('O') } })
    $result.publishStartedUtc = $publishStarted.ToString('O'); $result.publishFinishedUtc = $publishFinished.ToString('O')
    $result.publish = 'passed'
    $binary = Join-Path $env:ElasticsearchOutboxProbePublishDir ('ElasticsearchOutboxAotProbe' + $(if ($Runtime -eq 'win-x64') { '.exe' } else { '' }))
    if (!(Test-Path -LiteralPath $binary -PathType Leaf)) { throw 'Strict publish produced no expected native executable.' }
    $nativeFile = Get-Item -LiteralPath $binary
    if ($nativeFile.CreationTimeUtc -lt $publishStarted -or $nativeFile.CreationTimeUtc -gt $publishFinished -or $nativeFile.LastWriteTimeUtc -lt $publishStarted -or $nativeFile.LastWriteTimeUtc -gt $publishFinished) { throw 'Native executable was not created/written inside this publish window.' }
    $result.binaryCreatedUtc = $nativeFile.CreationTimeUtc.ToString('O'); $result.binaryWrittenUtc = $nativeFile.LastWriteTimeUtc.ToString('O')
    $result.binarySha256 = (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash
    Invoke-Bounded 'docker' @('version','--format','{{.Server.Version}}') 30 'docker-version' | Out-Null
    Invoke-Bounded 'docker' @('pull','docker.elastic.co/elasticsearch/elasticsearch:9.0.0') 300 'docker-pull' | Out-Null
    $containerAttempted = $true
    Invoke-Bounded 'docker' @('run','--detach','--name',$container,'--label',"excalibur.nativeprobe=$label",'-p','127.0.0.1::9200','-e','discovery.type=single-node','-e','xpack.security.enabled=false','-e','xpack.security.http.ssl.enabled=false','-e','ES_JAVA_OPTS=-Xms512m -Xmx512m','docker.elastic.co/elasticsearch/elasticsearch:9.0.0') 60 'docker-start' | Out-Null
    $port = Invoke-Bounded 'docker' @('port',$container,'9200/tcp') 30 'docker-port'
    if ($port.stdout.Trim() -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'Owned fixture did not expose exactly one loopback port.' }
    $uri = "http://127.0.0.1:$($Matches[1])"
    $ready = $false; $startup = [Diagnostics.Stopwatch]::StartNew()
    while ($startup.Elapsed.TotalSeconds -lt 240) {
        try {
            $version = Invoke-RestMethod -Uri $uri -TimeoutSec 3
            if ($version.version.number -ne '9.0.0') { throw 'Unexpected Elasticsearch server version.' }
            $version | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence 'server-version.json')
            $ready = $true; break
        } catch { $_.Exception.Message | Add-Content (Join-Path $evidence 'startup.log'); Start-Sleep -Seconds 1 }
    }
    if (!$ready) { throw 'Elasticsearch startup failed within240 seconds.' }
    $env:PROBE_CONNECTION = $uri; $env:PROBE_RUN_ID = "outbox-native-$label"
    $result.native = 'running'
    $run = Invoke-Bounded $binary @() 90 'native'
    if ((Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash -ne $result.binarySha256) { throw 'Native binary changed during execution.' }
    foreach ($object in $result.nativeObjectHashes) { if ((Get-FileHash -LiteralPath $object.path).Hash -ne $object.sha256) { throw 'Native object changed during execution.' } }
    $markers = @('PROBE-NATIVE dynamic-code=false json-reflection=false','PROBE-PASS primitive-stage-read','PROBE-PASS generated-header-stage-read','PROBE-PASS source-missing-metadata','PROBE-PASS stage-missing-metadata','PROBE-PASS no-accepted-missing-header-row','PROBE-COMPLETE ElasticsearchOutboxAotProbe checks=5')
    foreach ($marker in $markers) { if (([regex]::Matches($run.stdout, '(?m)^' + [regex]::Escape($marker) + '\r?$')).Count -ne 1) { throw "Missing/duplicate native completeness marker: $marker" } }
    $result.native = 'passed'
} catch {
    if ($result.publish -eq 'running') { $result.publish = 'failed' }
    if ($result.native -eq 'running') { $result.native = 'failed' }
    $result.failure = $_.Exception.ToString()
} finally {
    try {
        $inspection = Invoke-Bounded 'docker' @('inspect',$container) 30 'docker-inspect' -AllowFailure
        if ($inspection.exitCode -eq 0) {
            $details = @($inspection.stdout | ConvertFrom-Json)[0]
            if ($details.Config.Labels.'excalibur.nativeprobe' -ne $label) { throw 'Refusing to remove a container without this run ownership label.' }
            Invoke-Bounded 'docker' @('logs','--timestamps',$container) 30 'container' -AllowFailure | Out-Null
            Invoke-Bounded 'docker' @('rm','--force',$container) 30 'docker-remove' | Out-Null
            $remaining = Invoke-Bounded 'docker' @('ps','--all','--quiet','--filter',"name=^/$container$") 30 'docker-remaining'
            if ($remaining.stdout.Trim()) { throw 'Owned container remains after cleanup.' }
        } elseif ($containerAttempted) { throw 'Cannot verify cleanup of an attempted owned container; missing inspect is not proof of absence.' }
        $result.cleanup = 'passed'
    } catch { $result.cleanup = 'failed'; $result.cleanupFailure = $_.Exception.ToString() }
    try {
        $after = Get-SourceFence
        $after | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $evidence 'source-after.json')
        $changed = @(@($before.Keys) + @($after.Keys) | Sort-Object -Unique | Where-Object { $before[$_] -ne $after[$_] })
        $result.sourceStable = $changed.Count -eq 0
        if ($changed) { $result.changedFiles = $changed }
    } catch { $result.sourceStable = $false; $result.fenceFailure = $_.Exception.ToString() }
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
    $result | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $evidence 'result.json')
    Write-Host "Evidence: $evidence"
}
if ($result.publish -ne 'passed' -or $result.native -ne 'passed' -or $result.cleanup -ne 'passed' -or !$result.sourceStable) { exit 1 }
Write-Host 'PASS strict native public Elasticsearch outbox regression; isolated fixture and source/lock fence verified.'
exit 0
