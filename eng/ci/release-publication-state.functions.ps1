# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

. "$PSScriptRoot/../package-composition.functions.ps1"

function Initialize-ReleaseNuGetVersion {
    if ('NuGet.Versioning.NuGetVersion' -as [type]) { return }
    # Use the selected SDK's own NuGet parser; package identity is not a filename heuristic.
    $selected = @(& dotnet --version)
    if ($LASTEXITCODE -ne 0 -or $selected.Count -ne 1) { throw 'Cannot identify the selected SDK.' }
    $sdks = @(& dotnet --list-sdks)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate installed SDKs.' }
    $pattern = '^' + [regex]::Escape($selected[0].Trim()) + ' \[(.+)\]$'
    $sdkMatches = @($sdks | Where-Object { $_ -match $pattern })
    if ($sdkMatches.Count -ne 1) { throw 'Cannot uniquely locate the selected SDK.' }
    $null = $sdkMatches[0] -match $pattern
    $assembly = Join-Path $Matches[1] "$($selected[0].Trim())/NuGet.Versioning.dll"
    Add-Type -Path $assembly
}

function Get-ReleasePublicationState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Feed,
        [Parameter(Mandatory)][string]$Version,
        [scriptblock]$Probe = { param($Uri) (Invoke-WebRequest -Uri $Uri -Method Head -SkipHttpErrorCheck -TimeoutSec 20).StatusCode }
    )
    Initialize-ReleaseNuGetVersion
    $parsedVersion = [NuGet.Versioning.NuGetVersion]::Parse($Version)
    $normalized = $parsedVersion.ToNormalizedString().ToLowerInvariant()
    if (-not (Test-Path -LiteralPath $Feed -PathType Container)) { throw "Missing package feed: $Feed" }
    $packages = Get-CompositionPackages -Feed $Feed -Version $Version
    foreach ($package in $packages.Values) {
        if ($package.id -notmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]{0,99}$') { throw "Invalid package ID: '$($package.id)'" }
    }
    $result = [pscustomobject]@{
        status = 'not_checked'; mode = 'inconclusive'; version = $Version
        normalizedVersion = $normalized; feed = [IO.Path]::GetFullPath($Feed)
        packageCount = $packages.Count; published = 0; unknown = 0
        reason = $null; packages = @()
        versionParserAssembly = [NuGet.Versioning.NuGetVersion].Assembly.FullName
        versionParserPath = [NuGet.Versioning.NuGetVersion].Assembly.Location
        versionParserSha256 = (Get-FileHash -LiteralPath ([NuGet.Versioning.NuGetVersion].Assembly.Location) -Algorithm SHA256).Hash
    }
    if ($normalized -eq '0.0.0-local') {
        $result.reason = 'Local placeholder; publication was not checked.'
        return $result
    }
    foreach ($package in @($packages.Values | Sort-Object id)) {
        $id = $package.id.ToLowerInvariant()
        $url = "https://api.nuget.org/v3-flatcontainer/$id/$normalized/$id.$normalized.nupkg"
        $response = @(); $requestError = $null
        try { $response = @(& $Probe $url) }
        catch { $requestError = $_.Exception.Message }
        if (-not $requestError -and ($response.Count -ne 1 -or $response[0] -isnot [int])) {
            throw 'Publication probe must return exactly one integer HTTP status.'
        }
        $code = if ($requestError) { $null } else { $response[0] }
        $outcome = switch ($code) { 200 { 'published' } 404 { 'absent' } default { 'unknown' } }
        if ($outcome -eq 'published') { $result.published++ }
        if ($outcome -eq 'unknown') { $result.unknown++ }
        $result.packages += [pscustomobject]@{
            id = $package.id; version = $package.version; sha256 = $package.sha256
            uri = $url; httpStatus = $code; outcome = $outcome; error = $requestError
        }
    }
    if ($result.unknown -gt 0) {
        $result.reason = 'One or more publication lookups were unanswered; no complete publication mode is established.'
        return $result
    }
    $result.status = 'passed'
    $result.mode = if ($result.published -eq 0) { 'fresh' } elseif ($result.published -eq $result.packageCount) { 'resumed' } else { 'partial' }
    return $result
}
