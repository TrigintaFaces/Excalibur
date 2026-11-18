# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param(
    [string]$SourcePath = (Join-Path $PSScriptRoot 'get-integration-test-filter.ps1'),
    [string]$MetadataPath = (Join-Path $PSScriptRoot 'alpha22-keyvault-unverified.json')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile([IO.Path]::GetFullPath($SourcePath), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Filter source parse failed.' }
$functions = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-Alpha22KeyVaultScope' }, $true))
if ($functions.Count -ne 1) { throw 'Expected the actual single scope function.' }
. ([scriptblock]::Create($functions[0].Extent.Text))
$metadata = Get-Content -LiteralPath $MetadataPath -Raw | ConvertFrom-Json
function Assert-True([bool]$Value, [string]$Name) { if (-not $Value) { throw $Name } }
function Invoke-Scope([string]$Version = '10.0.0-alpha.22', [string]$Source = 'eng/ci/shards/IntegrationTests-Dispatch.slnf', [bool]$Live = $false, $Data = $metadata) {
    Get-Alpha22KeyVaultScope -Version $Version -Source $Source -IncludeCloud $Live -Metadata $Data
}
function Assert-Refuses([string]$Name, [scriptblock]$Action) {
    $refused = $false
    try { & $Action | Out-Null } catch { $refused = $true }
    Assert-True $refused $Name
}
$selected = Invoke-Scope
Assert-True ($selected.plannedDeferredCount -eq 11 -and $selected.disposition -eq 'deferred-unverified') 'Exact eleven scope'
Assert-True (@($selected.suffix -split '&').Count -eq 11) 'Eleven exact FQN terms'
foreach ($row in $metadata.tests) { Assert-True ($selected.suffix.Contains('FullyQualifiedName!=' + $row.fullyQualifiedName)) 'Every approved exact FQN' }
foreach ($name in @('ThrowArgumentException_WhenVaultUriNotConfigured','ThrowArgumentNullException_WhenOptionsIsNull','ThrowArgumentNullException_WhenCacheIsNull','ThrowArgumentNullException_WhenLoggerIsNull')) {
    Assert-True (-not $selected.suffix.Contains($name)) ('Local guard retained: ' + $name)
}
foreach ($version in @('', '10.0.0-alpha.23', '10.0.0')) { Assert-True ((Invoke-Scope -Version $version).plannedDeferredCount -eq 0) 'Other versions strict' }
foreach ($source in @('', 'eng/ci/shards/IntegrationTests-Excalibur.slnf', 'eng/ci/shards/IntegrationTests-Embedded.slnf')) { Assert-True ((Invoke-Scope -Source $source).plannedDeferredCount -eq 0) 'Other owners strict' }
Assert-True ((Invoke-Scope -Live $true).plannedDeferredCount -eq 0) 'Credentialed full override'
function Copy-Metadata { $metadata | ConvertTo-Json -Depth 8 | ConvertFrom-Json }
$bad = Copy-Metadata; $bad.tests = @($bad.tests | Select-Object -Skip 1); Assert-Refuses 'Missing identity' { Invoke-Scope -Data $bad }
$bad = Copy-Metadata; $bad.tests[1].id = $bad.tests[0].id; Assert-Refuses 'Duplicate ID' { Invoke-Scope -Data $bad }
$bad = Copy-Metadata; $bad.tests[1].fullyQualifiedName = $bad.tests[0].fullyQualifiedName; Assert-Refuses 'Duplicate FQN' { Invoke-Scope -Data $bad }
$bad = Copy-Metadata; $bad.tests[0].fullyQualifiedName = 'Other.Provider.Test'; Assert-Refuses 'Other class' { Invoke-Scope -Data $bad }
$bad = Copy-Metadata; $bad.releaseVersion = '10.0.0-alpha.23'; Assert-Refuses 'Metadata version drift' { Invoke-Scope -Data $bad }
$bad = Copy-Metadata; $bad.disposition = 'passed'; Assert-Refuses 'Disposition drift' { Invoke-Scope -Data $bad }
# The old leg partition and quarantine composer are still used by the executable script.
$source = Get-Content -LiteralPath $SourcePath -Raw
Assert-True ($source.Contains('unknown leg')) 'Unknown leg refusal retained'
Assert-True ($source.Contains("'get-test-filter.ps1'") -and $source.Contains('-Mode Exclude -AsJson')) 'Existing quarantine composer retained'
Write-Host 'PASS: alpha.22 Key Vault exact selection, default/future/other-source/live controls and metadata refusals.'
