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

# ---------------------------------------------------------------- feed protocol
# WHY THIS TIER DOES NOT USE `dotnet package search`, which is what it used to do.
#
# Search is an OPTIONAL resource in the NuGet v3 protocol, and the staging feed does not usefully
# serve it. The result was a gate that could not pass: a release staged 195 packages successfully and
# was then told 195 of 195 were "NOT served", one hundred and eight seconds after the push that
# created them. A clean total zero immediately after a successful push of the same set is the
# signature of an instrument that cannot answer, not of a feed that lost everything -- a propagation
# race returns partial results, never a perfect nil.
#
# That is the mirror image of the defect this whole file exists to prevent. A gate that cannot fail
# reports success it did not earn; a gate that cannot pass blocks work that was fine. Both are the
# same underlying fault -- a verdict decoupled from the thing it claims to measure.
#
# PackageBaseAddress is the resource restore itself uses, so it is served wherever installation
# works, and this tier now asks the same question the INSTALLED tier answers the expensive way.
# It also lets the tier honour its own stated contract. The documented promise is resolution "by id
# and version"; a search matched only the ID, so a feed holding some OTHER version of the package
# satisfied it. The version index makes the version the thing actually asserted.

# Pure. Returns the PackageBaseAddress/3.x @id from a parsed service index, or $null when the feed
# does not advertise one. $null MUST reach the caller as a REFUSE: a feed we cannot interrogate is
# unmeasured, and reporting "served" because the question could not be asked is how a gate starts
# lying.
function Get-PackageBaseAddress($serviceIndex) {
    if ($null -eq $serviceIndex -or $null -eq $serviceIndex.resources) { return $null }
    foreach ($r in $serviceIndex.resources) {
        if ($r.'@type' -like 'PackageBaseAddress/3*' -and -not [string]::IsNullOrWhiteSpace($r.'@id')) {
            return ($r.'@id').TrimEnd('/')
        }
    }
    return $null
}

# Pure. Is $want present in $versions? NuGet normalises to lower case on the wire, so the comparison
# is case-insensitive.
#
# An EMPTY or absent version list returns $false, deliberately and load-bearingly. "The feed listed
# nothing" and "the feed listed our version" must never reach the same verdict; treating an empty
# list as satisfaction is precisely the vacuous pass this gate is built to make impossible.
function Test-VersionServed([string[]]$versions, [string]$want) {
    if ($null -eq $versions -or $versions.Count -eq 0) { return $false }
    if ([string]::IsNullOrWhiteSpace($want)) { return $false }
    foreach ($v in $versions) {
        if ($null -ne $v -and $v.Trim().ToLowerInvariant() -eq $want.Trim().ToLowerInvariant()) { return $true }
    }
    return $false
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

    # ---- the served tier's decision core, exercised in BOTH directions ----
    #
    # The HTTP call cannot run offline, so what is locked here is everything that turns a feed's
    # answer into a verdict. That is where the previous implementation went wrong: the network call
    # worked fine and returned a truthful "I cannot answer this", and the logic above it read that as
    # "the package is missing".

    # LIVENESS: a well-formed service index yields the address, so the tier can ask its question.
    $goodIndex = [pscustomobject]@{ resources = @(
            [pscustomobject]@{ '@type' = 'SearchQueryService/3.0.0-beta'; '@id' = 'https://feed.invalid/query' },
            [pscustomobject]@{ '@type' = 'PackageBaseAddress/3.0.0'; '@id' = 'https://feed.invalid/v3-flatcontainer/' }
        ) }
    if ((Get-PackageBaseAddress $goodIndex) -ne 'https://feed.invalid/v3-flatcontainer') {
        $failures += 'a service index advertising PackageBaseAddress did not yield its address'
    } else {
        Write-Host 'SELF-TEST: PASS -- the base address is resolved from a service index (liveness)'
    }

    # SAFETY: a feed WITHOUT the resource must yield $null so the caller REFUSES. Returning a guessed
    # or default address here would send every query to a URL nobody serves and report the resulting
    # misses as missing packages -- which is exactly the failure being repaired.
    $searchOnlyIndex = [pscustomobject]@{ resources = @(
            [pscustomobject]@{ '@type' = 'SearchQueryService/3.0.0-beta'; '@id' = 'https://feed.invalid/query' }
        ) }
    if ($null -ne (Get-PackageBaseAddress $searchOnlyIndex)) {
        $failures += 'a feed advertising no PackageBaseAddress produced an address anyway'
    } else {
        Write-Host 'SELF-TEST: PASS -- a feed that cannot be interrogated yields no address, forcing a REFUSE (safety)'
    }
    if ($null -ne (Get-PackageBaseAddress $null)) {
        $failures += 'a null service index produced an address'
    }

    # LIVENESS: the staged version, present, is recognised -- including the lower-cased spelling the
    # wire actually carries. Without this arm a matcher that never matches would pass every safety
    # arm below and block every release.
    if (-not (Test-VersionServed @('9.0.0', '10.0.0-alpha.1') '10.0.0-alpha.1')) {
        $failures += 'a version present in the feed index was not recognised'
    } elseif (-not (Test-VersionServed @('10.0.0-ALPHA.1') '10.0.0-alpha.1')) {
        $failures += 'version matching was case-sensitive; the wire form is lower case'
    } else {
        Write-Host 'SELF-TEST: PASS -- a staged version present in the index is recognised (liveness)'
    }

    # SAFETY: a version the feed does not hold must NOT be reported as served. This is the arm that
    # fails first if anyone ever "fixes" a red release by loosening this tier.
    if (Test-VersionServed @('9.0.0', '3.0.0-alpha.216') '10.0.0-alpha.1') {
        $failures += 'a version absent from the feed index was reported as served'
    } else {
        Write-Host 'SELF-TEST: PASS -- an absent version is not reported as served (safety)'
    }

    # SAFETY: the zero-guard. An empty list is the shape a feed returns when it holds nothing at all,
    # and it must never satisfy the check.
    if ((Test-VersionServed @() '10.0.0-alpha.1') -or (Test-VersionServed $null '10.0.0-alpha.1')) {
        $failures += 'an EMPTY version list satisfied the served check'
    } else {
        Write-Host 'SELF-TEST: PASS -- an empty version list is not a pass (safety)'
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

$authHeader = @{}
if (-not [string]::IsNullOrWhiteSpace($FeedToken)) {
    $basic = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${FeedUser}:${FeedToken}"))
    $authHeader = @{ Authorization = "Basic $basic" }
}

# Resolve the address from the feed rather than assuming one. A hard-coded URL would silently target
# the wrong host the moment the staging feed changes, and every miss would be reported as a missing
# package instead of as our own misconfiguration.
$baseAddress = $null
try {
    $serviceIndex = Invoke-RestMethod -Uri $FeedUrl -Headers $authHeader -Method Get -ErrorAction Stop
    $baseAddress = Get-PackageBaseAddress $serviceIndex
} catch {
    Write-Host "::error::REFUSE: the staging feed's service index at '$FeedUrl' could not be read: $($_.Exception.Message)"
    Write-Host '::error::Nothing was measured. This is NOT a statement about the packages.'
    exit $EXIT_REFUSE
}
if ([string]::IsNullOrWhiteSpace($baseAddress)) {
    Write-Host "::error::REFUSE: the staging feed advertises no PackageBaseAddress resource, so package presence cannot be established here."
    Write-Host '::error::A feed that cannot be interrogated is UNMEASURED. Reporting these packages as served would be a verdict with no evidence under it.'
    exit $EXIT_REFUSE
}
Write-Host "base address: $baseAddress"

$served = 0
$notServed = @()
$unmeasured = @()
foreach ($pkg in $nupkgs) {
    $identity = Get-PackageIdentity $pkg.Name
    if ($null -eq $identity) {
        $notServed += "$($pkg.Name) (could not parse an id/version from the file name)"
        continue
    }

    $idLower = $identity.Id.ToLowerInvariant()
    $versions = $null
    try {
        $index = Invoke-RestMethod -Uri "$baseAddress/$idLower/index.json" -Headers $authHeader -Method Get -ErrorAction Stop
        $versions = @($index.versions)
    } catch {
        # A 404 is a genuine answer -- the feed serves the resource and holds no such package, which
        # is a FAILURE. Anything else (a 500, a timeout, an auth rejection) means we did not get an
        # answer at all, and that is UNMEASURED rather than absent. Collapsing the two is how the
        # previous implementation turned "I cannot tell you" into "it is missing".
        $status = $null
        if ($null -ne $_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($status -eq 404) {
            $notServed += "$($identity.Id) $($identity.Version) (feed holds no such package)"
        } else {
            $unmeasured += "$($identity.Id) $($identity.Version) (query failed: $($_.Exception.Message))"
        }
        continue
    }

    if (Test-VersionServed $versions $identity.Version) {
        $served++
    } else {
        $held = if ($null -eq $versions -or $versions.Count -eq 0) { '<none>' } else { ($versions | Select-Object -Last 3) -join ', ' }
        $notServed += "$($identity.Id) $($identity.Version) (feed holds: $held)"
    }
}

# Reported before the failures, and separately, because they are a different claim. A query that did
# not complete is not evidence that a package is absent, and folding it into the failure list would
# blame the artifact for our inability to look at it.
if ($unmeasured.Count -gt 0) {
    Write-Host "::error::REFUSE: $($unmeasured.Count) of $($nupkgs.Count) package(s) could not be queried at all:"
    $unmeasured | Select-Object -First 20 | ForEach-Object { Write-Host "    $_" }
    exit $EXIT_REFUSE
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
