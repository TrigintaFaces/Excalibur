#!/usr/bin/env pwsh
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param([string]$CandidateFeed = '', [string]$CandidateManifest = '', [string]$Version = '0.0.0-local')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/package-composition.functions.ps1"
if ($CandidateFeed -and -not $CandidateManifest) { throw 'Candidate package controls require the independently collected source manifest.' }
if ($CandidateFeed) { $CandidateFeed = (Resolve-Path -LiteralPath $CandidateFeed).Path }
if ($CandidateManifest) { $CandidateManifest = (Resolve-Path -LiteralPath $CandidateManifest).Path }
$root = Join-Path (Split-Path $PSScriptRoot) ('artifacts/package-composition-controls/' + [guid]::NewGuid().ToString('N'))
$shell = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
New-Item -ItemType Directory -Path $root -Force | Out-Null
$script:passed = 0
function Expect-Failure([string]$Name,[scriptblock]$Action,[string]$Message) {
    $caught = $null
    try { & $Action } catch { $caught = $_.Exception.Message }
    if (-not $caught -or $caught -notlike "*$Message*") { throw "$Name did not reject for the expected reason: $caught" }
    $script:passed++
    Write-Host "PASS $Name"
}
function Expect-Success([string]$Name,[scriptblock]$Action) {
    & $Action
    $script:passed++
    Write-Host "PASS $Name"
}
function New-Package([string]$Directory,[string]$Id,[string]$Dependency='',[string]$PackageVersion='0.0.0-local') {
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $path = Join-Path $Directory "$Id.$PackageVersion.nupkg"
    if (Test-Path $path) { Remove-Item -LiteralPath $path }
    $zip = [IO.Compression.ZipFile]::Open($path,[IO.Compression.ZipArchiveMode]::Create)
    try {
        $entry = $zip.CreateEntry("$Id.nuspec")
        $writer = [IO.StreamWriter]::new($entry.Open())
        try { $writer.Write("<package><metadata><id>$Id</id><version>$PackageVersion</version><dependencies>$Dependency</dependencies></metadata></package>") }
        finally { $writer.Dispose() }
    }
    finally { $zip.Dispose() }
}
$feed = Join-Path $root 'feed'
New-Package $feed 'Excalibur.Dispatch'
New-Package $feed 'Excalibur.Provider' '<dependency id="Excalibur.Dispatch" version="[0.0.0-local, )" />'
$packages = Get-CompositionPackages $feed '0.0.0-local'
Expect-Success 'valid package closure' { if ($packages.Count -ne 2) { throw 'Wrong count' } }
Expect-Failure 'missing provider dependency' {
    New-Package $feed 'Excalibur.Provider' '<dependency id="Excalibur.Missing" version="0.0.0-local" />'
    Get-CompositionPackages $feed '0.0.0-local'
} 'Missing internal dependency'
Expect-Failure 'stale dependency version' {
    New-Package $feed 'Excalibur.Provider' '<dependency id="Excalibur.Dispatch" version="0.0.0-stale" />'
    Get-CompositionPackages $feed '0.0.0-local'
} 'Wrong internal dependency version'
Expect-Failure 'wrong package version' { Get-CompositionPackages $feed '1.0.0' } 'Wrong package version'
Expect-Failure 'exclusive candidate lower bound refused' {
    New-Package $feed 'Excalibur.Provider' '<dependency id="Excalibur.Dispatch" version="(0.0.0-local, )" />'
    Get-CompositionPackages $feed '0.0.0-local'
} 'Wrong internal dependency version'
$emptyFeed = Join-Path $root 'empty-feed'
New-Item -ItemType Directory $emptyFeed | Out-Null
Expect-Failure 'empty candidate feed' { Get-CompositionPackages $emptyFeed '0.0.0-local' } 'No packages were produced'
New-Package $feed 'Excalibur.Provider' '<dependency id="Excalibur.Dispatch" version="0.0.0-local" />'
$packages = Get-CompositionPackages $feed '0.0.0-local'
$cache = Join-Path $root 'cache'
$cachedFolder = Join-Path $cache 'excalibur.dispatch/0.0.0-local'
New-Item -ItemType Directory $cachedFolder -Force | Out-Null
$cachedFile = Join-Path $cachedFolder 'excalibur.dispatch.0.0.0-local.nupkg'
Copy-Item (Join-Path $feed $packages['Excalibur.Dispatch'].file) $cachedFile
$assetsFile = Join-Path $root 'project.assets.json'
$assets = @{
    libraries = @{
        'Excalibur.Dispatch/0.0.0-local' = @{ type='package'; path='excalibur.dispatch/0.0.0-local'; sha512=$packages['Excalibur.Dispatch'].sha512 }
        'Excalibur.Application/1.0.0' = @{ type='project' }
    }
    targets = @{ net10=@{ 'Excalibur.Dispatch/0.0.0-local'=@{type='package'}; 'Excalibur.Application/1.0.0'=@{type='project'} } }
    packageFolders = @{ $cache=@{} }
}
function Save-Assets { $assets | ConvertTo-Json -Depth 10 | Set-Content $assetsFile }
Save-Assets
Expect-Failure 'wrong restore cache refused' { Assert-CompositionAssets $assetsFile $packages (Join-Path $cache 'wrong') } 'isolated package cache'
$assets.targets.net10.Remove('Excalibur.Dispatch/0.0.0-local')
Save-Assets
Expect-Failure 'global library without target consumption refused' { Assert-CompositionAssets $assetsFile $packages $cache -RequiredPackages 'Excalibur.Dispatch' } 'Required package was not consumed'
$assets.targets.net10['Excalibur.Dispatch/0.0.0-local'] = @{type='package'}
Save-Assets
Expect-Failure 'omitted target refused' { Assert-CompositionAssets $assetsFile $packages $cache -RequiredByTarget @{net10=@('Excalibur.Dispatch');net8=@('Excalibur.Dispatch')} } 'target population changed'
Expect-Failure 'deleted cross-family edge refused' { Assert-CompositionAssets $assetsFile $packages $cache -RequiredByTarget @{net10=@('Excalibur.Provider')} } 'Required package was not consumed'
Expect-Success 'selective Excalibur project references remain legitimate' {
    $seen = Assert-CompositionAssets $assetsFile $packages $cache -RequiredPackages 'Excalibur.Dispatch'
    if ($seen.Count -ne 1) { throw 'Missing package proof' }
}
Expect-Failure 'project-only sample refused' { Assert-CompositionAssets $assetsFile $packages $cache -PackageOnly } 'Project reference bypasses'
Expect-Failure 'omitted required consumer package' { Assert-CompositionAssets $assetsFile $packages $cache -RequiredPackages 'Excalibur.Provider' } 'Required package was not consumed'
$assets.libraries['Excalibur.Dispatch/0.0.0-local'].type = 'project'
Save-Assets
Expect-Failure 'dispatch project reference bypass refused' { Assert-CompositionAssets $assetsFile $packages $cache } 'Project reference bypasses'
$assets.libraries['Excalibur.Dispatch/0.0.0-local'].type = 'package'
$assets.libraries['Excalibur.Dispatch/0.0.0-local'].sha512 = 'stale'
Save-Assets
Expect-Failure 'stale same-version restored hash refused' { Assert-CompositionAssets $assetsFile $packages $cache } 'identity/hash differs'
$assets.libraries['Excalibur.Dispatch/0.0.0-local'].sha512 = $packages['Excalibur.Dispatch'].sha512
Save-Assets
[IO.File]::AppendAllText($cachedFile,'changed')
Expect-Failure 'substituted cache archive refused' { Assert-CompositionAssets $assetsFile $packages $cache } 'Cached package bytes differ'
Copy-Item (Join-Path $feed $packages['Excalibur.Dispatch'].file) $cachedFile -Force
Expect-Success 'restored valid cached package accepted' { $null = Assert-CompositionAssets $assetsFile $packages $cache }

# Exercise real child process exit and timeout handling; no simulated process receipts.
Expect-Success 'successful command retains raw output' {
    $log = Join-Path $root 'command-success'
    Invoke-PackageCommand $shell @('-NoProfile','-Command','[Console]::WriteLine("observed"); exit 0') $log $root
    if ((Get-Content "$log.stdout.log" -Raw).Trim() -ne 'observed') { throw 'Missing raw output' }
}
Expect-Failure 'single required command failure' {
    Invoke-PackageCommand $shell @('-NoProfile','-Command','[Console]::Error.WriteLine("failed"); exit 47') (Join-Path $root 'command-failure') $root
} 'exit 47'
Expect-Failure 'unexpected sample timeout' {
    Invoke-PackageCommand $shell @('-NoProfile','-Command','Start-Sleep -Seconds 15; exit 0') (Join-Path $root 'command-timeout') $root -TimeoutSeconds 1
} 'Unexpected timeout'
Expect-Success 'failure and timeout evidence retained' {
    $failure = Get-Content (Join-Path $root 'command-failure.receipt.json') -Raw | ConvertFrom-Json
    $timeout = Get-Content (Join-Path $root 'command-timeout.receipt.json') -Raw | ConvertFrom-Json
    if ($failure.exitCode -ne 47 -or -not $timeout.timedOut -or $timeout.status -eq 'passed') { throw 'Receipt lost refusal' }
}
Expect-Failure 'required sample cannot be skipped' { & "$PSScriptRoot/validate-package-composition.ps1" -SkipSample } 'scenario is required'

# Run the real pack entry point against three controlled MSBuild projects. One failure among
# otherwise passing commands must fail the entry point. No public feeds or framework packages used.
$fixture = Join-Path $root 'producer'
New-Item -ItemType Directory "$fixture/eng/ci/shards","$fixture/templates" -Force | Out-Null
Copy-Item "$PSScriptRoot/pack-local.ps1","$PSScriptRoot/package-composition.functions.ps1" "$fixture/eng"
# An isolated Git worktree identity without creating any commit or changing the repository index.
& git -C $fixture init --quiet
if ($LASTEXITCODE -ne 0) { throw 'Fixture git init failed.' }
$repository = Split-Path $PSScriptRoot
$objectDirectory = (& git -C $repository rev-parse --path-format=absolute --git-path objects).Trim()
[IO.File]::WriteAllText("$fixture/.git/objects/info/alternates",$objectDirectory + "`n")
$head = (& git -C $repository rev-parse HEAD).Trim()
& git -C $fixture update-ref HEAD $head
if ($LASTEXITCODE -ne 0) { throw 'Fixture git identity failed.' }
$projectPaths = @()
foreach ($name in @('A','B','C')) {
    $path = "src/Excalibur/$name/$name.csproj"
    $projectPaths += $path
    New-Item -ItemType Directory (Split-Path (Join-Path $fixture $path)) -Force | Out-Null
    New-Package "$fixture/templates" "Excalibur.$name"
    @'
<Project>
 <PropertyGroup><IsPackable>false</IsPackable><IsPackable Condition="'$(Configuration)' == 'Release' And '$(UsePackageReferences)' == 'false'">true</IsPackable><PackageId>Excalibur.NAME</PackageId><PackageVersion>0.0.0-local</PackageVersion></PropertyGroup>
 <Target Name="Pack"><Copy SourceFiles="$(MSBuildProjectDirectory)/../../../templates/Excalibur.NAME.0.0.0-local.nupkg" DestinationFolder="$(PackageOutputPath)" /></Target>
</Project>
'@.Replace('NAME',$name) | Set-Content (Join-Path $fixture $path)
}
# template path from src/Excalibur/A is three levels up, inside the fixture.
@{solution=@{path='unused.sln';projects=$projectPaths}} | ConvertTo-Json -Depth 8 | Set-Content "$fixture/eng/ci/shards/ShippingOnly.slnf"
$previousMode = $env:UsePackageReferences
try {
    $env:UsePackageReferences = 'true'
    Expect-Success 'Release source-mode roster overrides inherited package mode' { & "$fixture/eng/pack-local.ps1" -NoBuild -Clean }
    $copiedProducer = "$fixture/eng/pack-local.ps1"
    $producerSource = [IO.File]::ReadAllText($copiedProducer)
    try {
        [IO.File]::WriteAllText($copiedProducer,$producerSource.Replace(", '-p:UsePackageReferences=false'",''))
        Expect-Failure 'removing source-mode pin exposes inherited mode' { & $copiedProducer -NoBuild -Clean } 'evaluated package roster is empty'
    }
    finally { [IO.File]::WriteAllText($copiedProducer,$producerSource) }
}
finally { $env:UsePackageReferences = $previousMode }
Expect-Success 'source fingerprint observes dirty source contents' {
    $before = Get-CompositionSourceIdentity $fixture
    Add-Content (Join-Path $fixture $projectPaths[0]) '<!-- changed -->'
    $after = Get-CompositionSourceIdentity $fixture
    if ($before.sha256 -eq $after.sha256) { throw 'Changed source did not alter fingerprint' }
}
foreach ($relative in @('LICENSE','licenses/LICENSE-FIXTURE.txt','images/Dispatch/png/icon.png','images/Excalibur/png/icon.png','src/A/packages.lock.json',
    'NuGet.config','.globalconfig','.gitattributes','.gitignore','Directory.Solution.targets',
    'samples/Directory.Build.props','samples/Directory.Build.targets','samples/Directory.Packages.props','samples/NuGet.config','samples/global.json','samples/.editorconfig','samples/.globalconfig',
    'samples/01-getting-started/Directory.Build.props','samples/01-getting-started/NuGet.Config','samples/01-getting-started/.editorconfig','samples/01-getting-started/.globalconfig')) {
    Expect-Success "fingerprint observes packaged input $relative" {
        $path = Join-Path $fixture $relative
        New-Item -ItemType Directory (Split-Path $path) -Force | Out-Null
        $beforeCreation = Get-CompositionSourceIdentity $fixture
        Set-Content $path 'original fixture bytes'
        $before = Get-CompositionSourceIdentity $fixture
        if ($before.sha256 -eq $beforeCreation.sha256) { throw "New input was invisible: $relative" }
        Add-Content $path 'changed fixture bytes'
        if ((Get-CompositionSourceIdentity $fixture).sha256 -eq $before.sha256) { throw "Packaged input mutation was invisible: $relative" }
    }
}
Expect-Success 'fingerprint retains case-distinct Git paths' {
    # Git can track both paths even on a case-insensitive filesystem. Neither may disappear.
    $casePaths = @('src/case-sensitive/Type.cs', 'src/case-sensitive/type.cs')
    New-Item -ItemType Directory "$fixture/src/case-sensitive" -Force | Out-Null
    foreach ($relative in $casePaths) {
        Set-Content (Join-Path $fixture $relative) '// case-distinct input'
        $blob = (& git -C $fixture hash-object -w -- $relative).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Could not create fixture Git object' }
        & git -C $fixture update-index --add --cacheinfo "100644,$blob,$relative"
        if ($LASTEXITCODE -ne 0) { throw 'Could not index case-distinct fixture path' }
    }
    $identity = Get-CompositionSourceIdentity $fixture
    foreach ($relative in $casePaths) {
        if (@($identity.inputs | Where-Object { $_.path -ceq $relative }).Count -ne 1) { throw "Case-distinct input was lost: $relative" }
    }
}
$projectB = Join-Path $fixture $projectPaths[1]
$originalB = Get-Content $projectB -Raw
Set-Content $projectB ($originalB.Replace('<Copy SourceFiles=', '<Error Text="Planted one-project failure" /><Copy SourceFiles='))
Expect-Failure 'one of three pack failures blocks feed' { & "$fixture/eng/pack-local.ps1" -NoBuild -Clean } '1 required package command(s) failed'
Set-Content $projectB ($originalB -replace '<Copy [^>]+/>','')
Expect-Failure 'successful pack without expected artifact refused' { & "$fixture/eng/pack-local.ps1" -NoBuild -Clean } 'do not match the evaluated source roster'
Set-Content $projectB $originalB
@{solution=@{path='unused.sln';projects=@($projectPaths[0],$projectPaths[2])}} | ConvertTo-Json -Depth 8 | Set-Content "$fixture/eng/ci/shards/ShippingOnly.slnf"
Expect-Failure 'omitted shipping provider refused' { & "$fixture/eng/pack-local.ps1" -NoBuild -Clean } 'omitted from ShippingOnly'
Expect-Failure 'unsafe clean target refused' { & "$fixture/eng/pack-local.ps1" -NoBuild -Clean -OutputDirectory $fixture } 'must be a child directory'

# Real NuGet restores: source locks belong to the source graph. Package-mode validation must
# preserve them in both the root and recursively restored sibling, including under ambient locked mode.
$lockFixture = Join-Path $root 'locks'
New-Item -ItemType Directory "$lockFixture/feed","$lockFixture/dependency","$lockFixture/sibling","$lockFixture/consumer" -Force | Out-Null
'<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile></PropertyGroup></Project>' | Set-Content "$lockFixture/Directory.Build.props"
'<Project/>' | Set-Content "$lockFixture/Directory.Build.targets"
'<Project/>' | Set-Content "$lockFixture/Directory.Packages.props"
'<configuration><packageSources><clear/><add key="fixture" value="./feed"/></packageSources><packageSourceMapping><clear/></packageSourceMapping><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>' | Set-Content "$lockFixture/NuGet.Config"
'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><AssemblyName>P08.Lock.Dependency</AssemblyName><Version>1.0.0</Version></PropertyGroup></Project>' | Set-Content "$lockFixture/dependency/Dependency.csproj"
@'
<Project Sdk="Microsoft.NET.Sdk"><ItemGroup Condition="'$(UsePackageReferences)' != 'true'"><ProjectReference Include="../dependency/Dependency.csproj"/></ItemGroup><ItemGroup Condition="'$(UsePackageReferences)' == 'true'"><PackageReference Include="P08.Lock.Dependency" Version="1.0.0"/></ItemGroup></Project>
'@ | Set-Content "$lockFixture/sibling/Sibling.csproj"
'<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../sibling/Sibling.csproj"/></ItemGroup></Project>' | Set-Content "$lockFixture/consumer/Consumer.csproj"
$zip = [IO.Compression.ZipFile]::Open("$lockFixture/feed/P08.Lock.Dependency.1.0.0.nupkg",[IO.Compression.ZipArchiveMode]::Create)
try {
    $writer = [IO.StreamWriter]::new($zip.CreateEntry('P08.Lock.Dependency.nuspec').Open())
    try { $writer.Write('<package><metadata><id>P08.Lock.Dependency</id><version>1.0.0</version><authors>Fixture</authors><description>Fixture</description></metadata></package>') } finally { $writer.Dispose() }
    $null = $zip.CreateEntry('lib/net10.0/_._')
} finally { $zip.Dispose() }
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$restoreArguments = @('restore',"$lockFixture/consumer/Consumer.csproj",'--force',"-p:RestorePackagesPath=$lockFixture/cache")
Invoke-PackageCommand $dotnet ($restoreArguments + @('-p:UsePackageReferences=false','-p:RestoreLockedMode=false')) "$lockFixture/source" $lockFixture
$sourceLocks = @{}
foreach ($name in @('consumer','sibling')) {
    $path = "$lockFixture/$name/packages.lock.json"
    $sourceLocks[$name] = @{ bytes=[IO.File]::ReadAllBytes($path); hash=(Get-FileHash $path).Hash }
}
Expect-Success 'default package restore exposes root and sibling source lock mutation' {
    Invoke-PackageCommand $dotnet ($restoreArguments + @('-p:UsePackageReferences=true','-p:RestoreLockedMode=false','-p:RestoreForceEvaluate=true')) "$lockFixture/unisolated" $lockFixture
    foreach ($name in $sourceLocks.Keys) {
        if ((Get-FileHash "$lockFixture/$name/packages.lock.json").Hash -ceq $sourceLocks[$name].hash) { throw "Mutation control did not rewrite $name source lock" }
        [IO.File]::WriteAllBytes("$lockFixture/$name/packages.lock.json",$sourceLocks[$name].bytes)
    }
}
$previousLockedMode = $env:RestoreLockedMode
try {
    $env:RestoreLockedMode = 'true'
    $lockRunId = [guid]::NewGuid().ToString('N')
    $isolatedLockArguments = @(Get-CompositionLockProperties -RunId $lockRunId)
    foreach ($name in $sourceLocks.Keys) {
        $path = "$lockFixture/$name/obj/package-composition/$lockRunId/packages.lock.json"
        New-Item -ItemType Directory (Split-Path $path) -Force | Out-Null
        $incompatible = [Text.Encoding]::UTF8.GetString($sourceLocks[$name].bytes).Replace('"net10.0"','"net8.0"')
        [IO.File]::WriteAllText($path,$incompatible)
    }
    Expect-Failure 'removing the candidate locked-mode override exposes incompatible locks' {
        Invoke-PackageCommand $dotnet ($restoreArguments + @('-p:UsePackageReferences=true') + @($isolatedLockArguments | Where-Object { $_ -ne '-p:RestoreLockedMode=false' })) "$lockFixture/inherited-locked-mode" $lockFixture
    } 'Command failed with exit'
    if (-not (Get-Content "$lockFixture/inherited-locked-mode.stdout.log" -Raw).Contains('NU1004')) { throw 'Locked-mode control failed for an unintended reason' }
    Expect-Success 'isolated package restore preserves root and sibling source locks' {
        Invoke-PackageCommand $dotnet ($restoreArguments + @('-p:UsePackageReferences=true') + $isolatedLockArguments) "$lockFixture/isolated" $lockFixture
        $paths = @()
        foreach ($name in $sourceLocks.Keys) {
            if ((Get-FileHash "$lockFixture/$name/packages.lock.json").Hash -cne $sourceLocks[$name].hash) { throw "Package restore rewrote $name source lock" }
            $path = "$lockFixture/$name/obj/package-composition/$lockRunId/packages.lock.json"
            if (-not (Test-Path $path)) { throw "Missing $name isolated package lock" }
            $paths += (Resolve-Path $path).Path
            $contents = Get-Content $path -Raw | ConvertFrom-Json -AsHashtable
            if ($contents.dependencies['net10.0']['P08.Lock.Dependency'].type -eq 'Project') { throw 'Candidate lock retained source dependency' }
        }
        if (@($paths | Sort-Object -Unique).Count -ne $sourceLocks.Count) { throw 'Projects shared a lock output path' }
    }
    $noLockProject = "$lockFixture/no-lock"
    New-Item -ItemType Directory $noLockProject -Force | Out-Null
    '<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><RestorePackagesWithLockFile>false</RestorePackagesWithLockFile></PropertyGroup></Project>' | Set-Content "$noLockProject/Directory.Build.props"
    '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="P08.Lock.Dependency" Version="1.0.0"/></ItemGroup></Project>' | Set-Content "$noLockProject/NoLock.csproj"
    $newLockId = [guid]::NewGuid().ToString('N')
    $newLockArguments = @(Get-CompositionLockProperties -RunId $newLockId)
    $noLockRestore = @('restore',"$noLockProject/NoLock.csproj",'--force',"-p:RestorePackagesPath=$lockFixture/cache")
    Expect-Success 'a lock path alone does not opt a sample into lock generation' {
        Invoke-PackageCommand $dotnet ($noLockRestore + @($newLockArguments | Where-Object { $_ -ne '-p:RestorePackagesWithLockFile=true' })) "$lockFixture/no-lock-without-opt-in" $lockFixture
        if (Test-Path "$noLockProject/obj/package-composition/$newLockId/packages.lock.json") { throw 'Missing-opt-in control unexpectedly produced a lock' }
    }
    Expect-Success 'package mode opts a sample without source locks into isolated lock generation' {
        Invoke-PackageCommand $dotnet ($noLockRestore + $newLockArguments) "$lockFixture/no-lock-opt-in" $lockFixture
        if (-not (Test-Path "$noLockProject/obj/package-composition/$newLockId/packages.lock.json")) { throw 'Sample candidate lock was not generated' }
        if (Test-Path "$noLockProject/packages.lock.json") { throw 'Sample wrote a source lock' }
    }
} finally { $env:RestoreLockedMode = $previousLockedMode }

if ($CandidateFeed) {
    $sample = Join-Path $root 'sample'
    New-Item -ItemType Directory $sample -Force | Out-Null
    $source = Join-Path (Split-Path $PSScriptRoot) 'samples/01-getting-started/DispatchOnly'
    foreach ($file in Get-ChildItem $source -Filter '*.cs' -Recurse) {
        $relative = [IO.Path]::GetRelativePath($source,$file.FullName)
        if ($relative -match '^(bin|obj)[\\/]') { continue }
        $destination = Join-Path $sample $relative
        New-Item -ItemType Directory (Split-Path $destination) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
    Set-Content "$sample/Directory.Build.props" '<Project />'
    Set-Content "$sample/Directory.Build.targets" '<Project />'
    Set-Content "$sample/Directory.Packages.props" '<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>'
    [xml]$pins = Get-Content (Join-Path (Split-Path $PSScriptRoot) 'Directory.Packages.props') -Raw
    $loggingVersion = $pins.SelectSingleNode('//PackageVersion[@Include="Microsoft.Extensions.Logging.Console"]').Version
    @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><PackageReference Include="Excalibur.Dispatch" Version="$Version"/><PackageReference Include="Microsoft.Extensions.Logging.Console" Version="$loggingVersion"/></ItemGroup></Project>
"@ | Set-Content "$sample/Consumer.csproj"
    @"
<configuration><fallbackPackageFolders><clear/></fallbackPackageFolders><packageSources><clear/><add key="candidate" value="$([Security.SecurityElement]::Escape($CandidateFeed))"/><add key="nuget" value="https://api.nuget.org/v3/index.json"/></packageSources><packageSourceMapping><clear/><packageSource key="candidate"><package pattern="Excalibur*"/></packageSource><packageSource key="nuget"><package pattern="*"/></packageSource></packageSourceMapping></configuration>
"@ | Set-Content "$sample/NuGet.Config"
    $previousCache = $env:NUGET_PACKAGES
    $env:NUGET_PACKAGES = Join-Path $root 'sample-cache'
    $dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
    try {
        function Invoke-SampleControl([string]$Name) {
            Invoke-PackageCommand $dotnet @('build',"$sample/Consumer.csproj",'-c','Release','--disable-build-servers') (Join-Path $root "$Name.build") $sample
            Invoke-PackageCommand $dotnet @("$sample/bin/Release/net10.0/Consumer.dll") (Join-Path $root "$Name.run") $sample -TimeoutSeconds 30
        }
        Expect-Success 'actual packaged sample passes' { Invoke-SampleControl 'sample-positive' }
        $packages = Get-CompositionPackages $CandidateFeed $Version
        $null = Assert-CompositionAssets "$sample/obj/project.assets.json" $packages $env:NUGET_PACKAGES -PackageOnly -RequiredPackages 'Excalibur.Dispatch'
        $mutations = @(
            @{ name='sample-command'; file='Handlers/CreateOrderHandler.cs'; old='return Task.FromResult(orderId);'; replacement='return Task.FromResult(Guid.Empty);'; message='The command did not create the expected order.' },
            @{ name='sample-event'; file='Handlers/OrderCreatedHandler.cs'; old='store.Notifications[eventMessage.OrderId] = 0;'; replacement='store.Notifications.Clear();'; message='Both event handlers must process the created order.' },
            @{ name='sample-document'; file='Handlers/GetOrderHandler.cs'; old='store.Documents[document.OrderId] = orderData;'; replacement='store.Documents[document.OrderId] = orderData with { Quantity = -1 };'; message='The document handler did not read the expected order.' }
        )
        foreach ($mutation in $mutations) {
            $path = Join-Path $sample $mutation.file
            $original = [IO.File]::ReadAllText($path)
            if (-not $original.Contains($mutation.old)) { throw "Sample mutation no longer applies: $($mutation.name)" }
            try {
                [IO.File]::WriteAllText($path,$original.Replace($mutation.old,$mutation.replacement))
                Expect-Failure $mutation.name { Invoke-SampleControl $mutation.name } 'Command failed with exit'
                $errorText = Get-Content (Join-Path $root ($mutation.name + '.run.stderr.log')) -Raw
                if (-not $errorText.Contains($mutation.message)) { throw "Scenario failed for an unintended reason: $($mutation.name)" }
            }
            finally { [IO.File]::WriteAllText($path,$original) }
        }
        Expect-Success 'restored packaged sample passes' { Invoke-SampleControl 'sample-restored' }

        $mutationFeed = Join-Path $root 'mutated-feed'
        New-Item -ItemType Directory $mutationFeed -Force | Out-Null
        Copy-Item "$CandidateFeed/*.nupkg" $mutationFeed
        foreach ($id in @('Excalibur.Data.InMemory','Excalibur.Migrate.Tool')) {
            $originalPath = Join-Path $mutationFeed "$id.$Version.nupkg"
            $renamedPath = Join-Path $mutationFeed 'renamed.nupkg'
            Move-Item -LiteralPath $originalPath -Destination $renamedPath
            try {
                $log = Join-Path $root ($id + '.renamed')
                Expect-Failure "renamed $id archive cannot disappear" {
                    Invoke-PackageCommand $shell @('-NoProfile','-File',"$PSScriptRoot/smoke-test-packages.ps1",'-CandidateManifest',$CandidateManifest,'-CandidateFeed',$mutationFeed,'-EvidenceDirectory',"$root/renamed-$id") $log $repository -TimeoutSeconds 120
                } 'Command failed with exit'
                if (-not (Get-Content "$log.stdout.log" -Raw).Contains('Unrecognized package filename: renamed.nupkg')) { throw "Renamed archive failed for an unintended reason: $id" }
            }
            finally { Move-Item -LiteralPath $renamedPath -Destination $originalPath }
        }

        # An empty consumer still compiles after this package loses its internal dependencies.
        # Its independently collected SOURCE closure must expose the missing edge.
        $packagePath = Join-Path $mutationFeed "Excalibur.Data.InMemory.$Version.nupkg"
        $zip = [IO.Compression.ZipFile]::Open($packagePath,[IO.Compression.ZipArchiveMode]::Update)
        try {
            $entry = @($zip.Entries | Where-Object FullName -like '*.nuspec')[0]
            $name = $entry.FullName
            $reader = [IO.StreamReader]::new($entry.Open())
            try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
            foreach ($node in @($spec.SelectNodes('//*[local-name()="dependency"]'))) {
                if ($node.GetAttribute('id') -like 'Excalibur*') { $null = $node.ParentNode.RemoveChild($node) }
            }
            $entry.Delete()
            $writer = [IO.StreamWriter]::new($zip.CreateEntry($name).Open())
            try { $writer.Write($spec.OuterXml) } finally { $writer.Dispose() }
        }
        finally { $zip.Dispose() }
        $deletedConsumer = Join-Path $root 'deleted-dependency'
        New-Item -ItemType Directory $deletedConsumer -Force | Out-Null
        Copy-Item "$sample/Directory.Build.props","$sample/Directory.Build.targets","$sample/Directory.Packages.props" $deletedConsumer
        @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="Excalibur.Data.InMemory" Version="$Version"/></ItemGroup></Project>
"@ | Set-Content "$deletedConsumer/Consumer.csproj"
        [xml]$config = Get-Content "$sample/NuGet.Config" -Raw
        $config.SelectSingleNode('//packageSources/add[@key="candidate"]').SetAttribute('value',$mutationFeed)
        $config.Save("$deletedConsumer/NuGet.Config")
        $env:NUGET_PACKAGES = Join-Path $root 'deleted-dependency-cache'
        Expect-Success 'empty consumer alone misses deleted package dependencies' {
            Invoke-PackageCommand $dotnet @('build',"$deletedConsumer/Consumer.csproj",'-c','Release','--disable-build-servers') (Join-Path $root 'deleted-dependency.build') $deletedConsumer
        }
        $sourceManifest = Get-Content -LiteralPath $CandidateManifest -Raw | ConvertFrom-Json -AsHashtable
        $sourceRoot = @($sourceManifest.projects | Where-Object id -eq 'Excalibur.Data.InMemory')
        if ($sourceRoot.Count -ne 1 -or -not $sourceRoot[0].internalDependencies) { throw 'Missing independent Data.InMemory source closure.' }
        $required = @{}
        foreach ($target in $sourceRoot[0].internalDependencies.Keys) { $required[$target] = @('Excalibur.Data.InMemory') + @($sourceRoot[0].internalDependencies[$target]) }
        $mutatedPackages = Get-CompositionPackages $mutationFeed $Version
        Expect-Failure 'source closure rejects deleted nuspec dependencies' {
            Assert-CompositionAssets "$deletedConsumer/obj/project.assets.json" $mutatedPackages $env:NUGET_PACKAGES -PackageOnly -RequiredByTarget $required
        } 'Required package was not consumed'
    }
    finally { $env:NUGET_PACKAGES = $previousCache }
}
Write-Host "$script:passed package-composition controls passed. Evidence: $root"
exit 0
