#!/usr/bin/env pwsh
<#
.SYNOPSIS
Builds a VSTest filter expression with optional flaky-test quarantine include/exclude clauses.
#>
param(
    [string]$BaseFilter = '',
    [ValidateSet('Exclude', 'Include')]
    [string]$Mode = 'Exclude',
    [string]$QuarantineFile = 'eng/ci/flaky-tests-quarantine.json',
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertFrom-JsonCompat {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [int]$Depth = 20
    )

    $jsonText = if ($Json -is [string]) { $Json } else { ($Json -join [Environment]::NewLine) }
    $convertFromJsonCommand = Get-Command ConvertFrom-Json -ErrorAction Stop
    if ($convertFromJsonCommand.Parameters.ContainsKey('Depth')) {
        return ($jsonText | ConvertFrom-Json -Depth $Depth)
    }

    return ($jsonText | ConvertFrom-Json)
}

$resolvedQuarantineFile = [IO.Path]::GetFullPath((Join-Path (Get-Location) $QuarantineFile))
if (-not (Test-Path $resolvedQuarantineFile)) {
    $result = [pscustomobject]@{
        file   = $resolvedQuarantineFile
        mode   = $Mode
        count  = 0
        filter = $BaseFilter
    }

    if ($AsJson) {
        $result | ConvertTo-Json -Depth 5 -Compress
    }
    else {
        Write-Output $result.filter
    }
    exit 0
}

$manifest = ConvertFrom-JsonCompat -Json (Get-Content $resolvedQuarantineFile -Raw) -Depth 20
$tests = @($manifest.tests)

$names = @(
    $tests |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.fullyQualifiedName) } |
        ForEach-Object { $_.fullyQualifiedName.Trim() } |
        Select-Object -Unique
)

$filter = $BaseFilter
if ($names.Count -gt 0) {
    if ($Mode -eq 'Include') {
        $filter = ($names | ForEach-Object { "FullyQualifiedName=$_" }) -join '|'
    }
    else {
        $exclusions = ($names | ForEach-Object { "FullyQualifiedName!=$_" }) -join '&'
        if ([string]::IsNullOrWhiteSpace($BaseFilter)) {
            $filter = $exclusions
        }
        else {
            $filter = "($BaseFilter)&$exclusions"
        }
    }
}
elseif ($Mode -eq 'Include') {
    $filter = ''
}

$result = [pscustomobject]@{
    file   = $resolvedQuarantineFile
    mode   = $Mode
    count  = $names.Count
    filter = $filter
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 5 -Compress
}
else {
    Write-Output $result.filter
}
