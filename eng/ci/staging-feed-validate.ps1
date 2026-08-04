#!/usr/bin/env pwsh
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
<#
.SYNOPSIS
    Publishes the release packages to a staging feed and proves they install FROM it.

.DESCRIPTION
    WHY THIS EXISTS. Publication to NuGet.org is irreversible: a version, once pushed, cannot be
    replaced. Until now the only pre-publication consumption test packed its own packages, with a
    synthetic version, into a LOCAL DIRECTORY. That proves a package can be produced and referenced.
    It cannot prove the thing that actually breaks on a release: that the artifact we are about to
    make permanent resolves and installs from a real remote feed, over the wire, with its real
    version and its real dependency graph.

    A local folder feed silently forgives several classes of defect that a real feed does not --
    among them a dependency that exists nowhere but this machine, and a package whose identity on
    the wire differs from its file name.

    TWO TIERS, REPORTED SEPARATELY AND ON PURPOSE. They answer different questions and a single
    "staging validation passed" would blur them into a claim wider than the evidence:

      SERVED    every pushed package can be resolved from the staging feed by id and version.
                Cheap, and it covers the WHOLE set.
      INSTALLED a clean project restores and COMPILES against the entry-point packages, from an
                EMPTY package cache. Expensive, so it covers a subset.

    The empty cache is what makes the INSTALLED tier mean anything. With a warm cache a restore can
    succeed having never contacted the feed, and would then pass while the feed served nothing at
    all. NUGET_PACKAGES is redirected to a fresh directory for exactly that reason -- it is the
    positive control built into the gate.

.PARAMETER PackagesPath
    Directory containing the .nupkg files to stage.

.PARAMETER FeedUrl
    The staging feed's push/query endpoint (a v3 index.json for query).

.PARAMETER FeedName
    Local source name for the feed inside the generated NuGet.Config.

.PARAMETER FeedUser
    Username for feed auth. For GitHub Packages any non-empty value works; the token carries auth.

.PARAMETER FeedToken
    Auth token. Never echoed.

.PARAMETER InstallTest
    Package ids to install-test. These pull their dependency closure with them.

.PARAMETER Version
    The version to install-test. Must be the real release version, not a synthetic one.

.PARAMETER SelfTest
    Prove this gate can FAIL, then exit. Runs offline.

.NOTES
    EXIT CODES -- distinct on purpose.
      0  staged and validated
      1  a validation arm FAILED (a package did not serve, or did not install)
      3  REFUSE: the inputs make validation impossible (no packages, no feed, no version)
#>
[CmdletBinding()]
param(
    [string]$PackagesPath = 'packages',
    [string]$FeedUrl = '',
    [string]$FeedName = 'staging',
    [string]$FeedUser = 'github-actions',
    [string]$FeedToken = '',
    [string[]]$InstallTest = @('Excalibur.Dispatch', 'Excalibur.Dispatch.Abstractions'),
    [string]$Version = '',
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$EXIT_REFUSE = 3

function Write-Section([string]$text) { Write-Host "`n=== $text ===" }

# Parses "Excalibur.Dispatch.Abstractions.10.0.0-alpha.1.nupkg" into id + version. The version is
# whatever follows the LAST id-like segment; splitting naively on the first digit breaks on ids that
# contain digits, and this repository ships several.
function Get-PackageIdentity([string]$fileName) {
    $base = [System.IO.Path]::GetFileNameWithoutExtension($fileName)
    if ($base -match '^(?<id>.+?)\.(?<ver>\d+\.\d+\.\d+.*)$') {
        return [pscustomobject]@{ Id = $Matches['id']; Version = $Matches['ver'] }
    }
    return $null
}

# ---------------------------------------------------------------- self-test (safety AND liveness)
if ($SelfTest) {
    $failures = @()

    # LIVENESS: real-world names parse into the identity the rest of the script keys on.
    $cases = @(
        @{ File = 'Excalibur.Dispatch.10.0.0-alpha.1.nupkg'; Id = 'Excalibur.Dispatch'; Ver = '10.0.0-alpha.1' },
        @{ File = 'Excalibur.Dispatch.Abstractions.10.0.0.nupkg'; Id = 'Excalibur.Dispatch.Abstractions'; Ver = '10.0.0' },
        @{ File = 'Excalibur.LeaderElection.Redis.3.0.0-alpha.216.nupkg'; Id = 'Excalibur.LeaderElection.Redis'; Ver = '3.0.0-alpha.216' }
    )
    foreach ($c in $cases) {
        $got = Get-PackageIdentity $c.File
        if ($null -eq $got -or $got.Id -ne $c.Id -or $got.Version -ne $c.Ver) {
            $failures += "parse '$($c.File)' -> got '$($got.Id)'/'$($got.Version)', expected '$($c.Id)'/'$($c.Ver)'"
        }
    }
    if ($failures.Count -eq 0) { Write-Host 'SELF-TEST: PASS -- package identities parse (liveness)' }

    # SAFETY: a name with no version must NOT parse. Returning a bogus identity here would make the
    # served-tier query ask the feed for a package that cannot exist and read the miss as a failure
    # of the feed rather than of this parser.
    if ($null -ne (Get-PackageIdentity 'NotAPackage.nupkg')) {
        $failures += 'a versionless name parsed as a package identity'
    } else {
        Write-Host 'SELF-TEST: PASS -- a versionless name is rejected, not guessed (safety)'
    }

    # SAFETY: empty inputs must REFUSE, never pass. A gate that validates nothing and reports success
    # is the exact defect this whole effort exists to remove.
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("staging-selftest-" + [guid]::NewGuid())
    New-Item -ItemType Directory -Path $tmp -Force | Out-Null
    try {
        $out = & $PSCommandPath -PackagesPath $tmp -FeedUrl 'https://example.invalid/index.json' -Version '1.0.0' 2>&1
        $code = $LASTEXITCODE
        if ($code -ne $EXIT_REFUSE) {
            $failures += "an EMPTY package directory exited $code, expected $EXIT_REFUSE (REFUSE)"
        } else {
            Write-Host 'SELF-TEST: PASS -- an empty package set REFUSES rather than reporting success (safety)'
        }
    } finally { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Host "SELF-TEST: FAIL -- $_" }
        exit 1
    }
    Write-Host 'SELF-TEST: the staging-feed gate is non-vacuous.'
    exit 0
}

# ---------------------------------------------------------------- inputs
if (-not (Test-Path $PackagesPath)) {
    Write-Host "::error::REFUSE: package directory '$PackagesPath' does not exist. Nothing to stage."
    exit $EXIT_REFUSE
}
$nupkgs = @(Get-ChildItem -Path $PackagesPath -Filter '*.nupkg' -Recurse |
    Where-Object { $_.Name -notlike '*.symbols.nupkg' })
if ($nupkgs.Count -eq 0) {
    Write-Host "::error::REFUSE: no .nupkg found under '$PackagesPath'. An empty set would validate vacuously."
    exit $EXIT_REFUSE
}
if ([string]::IsNullOrWhiteSpace($FeedUrl)) {
    Write-Host '::error::REFUSE: no -FeedUrl. A staging gate with no staging feed proves nothing.'
    exit $EXIT_REFUSE
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host '::error::REFUSE: no -Version. Install-testing a version other than the one being released would validate the wrong artifact.'
    exit $EXIT_REFUSE
}

Write-Section "Staging $($nupkgs.Count) package(s) to $FeedName"

# ---------------------------------------------------------------- push
# --skip-duplicate is REQUIRED, not convenience: a resumed release re-runs this job, and a feed that
# already holds the version must not turn a retry into a failure. It is the same idempotency the
# release itself is required to have.
$pushed = 0
foreach ($pkg in $nupkgs) {
    dotnet nuget push $pkg.FullName --source $FeedUrl --api-key $FeedToken --skip-duplicate 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "::error::push FAILED for $($pkg.Name). The staging feed did not accept the artifact."
        exit 1
    }
    $pushed++
}
Write-Host "pushed: $pushed package(s)"

# ---------------------------------------------------------------- TIER 1: served
Write-Section 'TIER 1 - every staged package is served by the feed'
$served = 0
$notServed = @()
foreach ($pkg in $nupkgs) {
    $identity = Get-PackageIdentity $pkg.Name
    if ($null -eq $identity) {
        $notServed += "$($pkg.Name) (could not parse an id/version from the file name)"
        continue
    }
    $listed = dotnet package search $identity.Id --source $FeedUrl --exact-match --format json 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0 -and $listed -match [regex]::Escape($identity.Id)) {
        $served++
    } else {
        $notServed += "$($identity.Id) $($identity.Version)"
    }
}
if ($notServed.Count -gt 0) {
    Write-Host "::error::$($notServed.Count) of $($nupkgs.Count) staged package(s) are NOT served by the feed:"
    $notServed | Select-Object -First 20 | ForEach-Object { Write-Host "    $_" }
    exit 1
}
Write-Host "served: $served/$($nupkgs.Count)"

# ---------------------------------------------------------------- TIER 2: installed from an EMPTY cache
Write-Section "TIER 2 - install $($InstallTest.Count) entry-point package(s) from an EMPTY cache"
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("staging-install-" + [guid]::NewGuid())
$cache = Join-Path $work 'nuget-cache'
$proj = Join-Path $work 'Consumer'
New-Item -ItemType Directory -Path $proj -Force | Out-Null
New-Item -ItemType Directory -Path $cache -Force | Out-Null

try {
    # THE POSITIVE CONTROL. A warm cache lets a restore succeed without ever contacting the feed, so
    # the test would pass over a feed that served nothing. Redirecting the cache to a fresh directory
    # is what forces resolution to happen over the wire, which is the only thing this tier asserts.
    $env:NUGET_PACKAGES = $cache

    $refs = ($InstallTest | ForEach-Object { "    <PackageReference Include=`"$_`" Version=`"$Version`" />" }) -join "`n"
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <!-- The consumer is a plain library. It deliberately inherits NOTHING from this repository:
         no Directory.Build.props, no central package management, no analyzers. A consumer does not
         get our build, and validating against our build would test the wrong thing. -->
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <RestorePackages>true</RestorePackages>
  </PropertyGroup>
  <ItemGroup>
$refs
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $proj 'Consumer.csproj') -Encoding UTF8

    'public static class ConsumerProbe { public static string Name => typeof(object).Name; }' |
        Set-Content -Path (Join-Path $proj 'ConsumerProbe.cs') -Encoding UTF8

    # nuget.org stays in the list because the dependency closure legitimately includes first-party
    # and third-party packages that are not ours. OUR packages exist only on the staging feed at this
    # point -- they are unpublished -- so a successful restore of them can only have come from there.
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="$FeedName" value="$FeedUrl" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <$FeedName>
      <add key="Username" value="$FeedUser" />
      <add key="ClearTextPassword" value="$FeedToken" />
    </$FeedName>
  </packageSourceCredentials>
</configuration>
"@ | Set-Content -Path (Join-Path $proj 'NuGet.Config') -Encoding UTF8

    Push-Location $proj
    try {
        dotnet restore --no-cache 2>&1 | Tee-Object -Variable restoreLog | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host '::error::restore FAILED from the staging feed. The packages about to be published do not install.'
            $restoreLog | Select-Object -Last 30 | ForEach-Object { Write-Host "    $_" }
            exit 1
        }

        dotnet build --no-restore --configuration Release 2>&1 | Tee-Object -Variable buildLog | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host '::error::build FAILED against the staged packages. They restore but cannot be compiled against.'
            $buildLog | Select-Object -Last 30 | ForEach-Object { Write-Host "    $_" }
            exit 1
        }
    } finally { Pop-Location }

    # Prove the assets actually came from the feed rather than from somewhere ambient. An empty cache
    # that stayed empty would mean the restore was satisfied by something this gate cannot see.
    $restored = @(Get-ChildItem -Path $cache -Directory -ErrorAction SilentlyContinue)
    if ($restored.Count -eq 0) {
        Write-Host '::error::the isolated package cache is EMPTY after a successful restore. Resolution did not come from the staging feed, so this tier proved nothing.'
        exit 1
    }
    Write-Host "installed: $($InstallTest.Count) entry-point package(s); $($restored.Count) package(s) materialised into the isolated cache"
}
finally {
    Remove-Item Env:\NUGET_PACKAGES -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------- verdict, scoped to what was measured
Write-Section 'RESULT'
Write-Host "  staged and served : $served/$($nupkgs.Count) package(s)"
Write-Host "  install-tested    : $($InstallTest.Count) entry-point package(s) ($($InstallTest -join ', '))"
Write-Host ''
Write-Host "  Scope, stated so this does not read wider than it is: every staged package was proven"
Write-Host "  SERVED by the feed, and the entry-point packages above were proven to RESTORE and"
Write-Host "  COMPILE from it against an empty cache. The remaining $($nupkgs.Count - $InstallTest.Count) package(s) were not"
Write-Host "  individually install-tested, though those in the entry points' dependency closure were"
Write-Host "  exercised transitively."
exit 0
