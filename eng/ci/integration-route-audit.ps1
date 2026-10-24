[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path $output) { throw 'Routing evidence directory must be new.' }
[void][IO.Directory]::CreateDirectory($output)
$hostPath = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$link = [IO.File]::ResolveLinkTarget($hostPath, $true)
if ($null -ne $link) { $hostPath = $link.FullName }
$sdkVersion = (Get-Content (Join-Path $repo 'global.json') -Raw | ConvertFrom-Json).sdk.version
$sdk = Join-Path ([IO.Path]::GetDirectoryName($hostPath)) "sdk/$sdkVersion"
$routeJson = & python (Join-Path $PSScriptRoot 'integration-route-audit.py') --repo $repo
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve supported scheduled integration routes.' }
$routeJson | Set-Content (Join-Path $output 'routes.json') -Encoding utf8NoBOM
$routes = ($routeJson | ConvertFrom-Json).routes
$sourcePath = (Resolve-Path $Source).Path
$inputPaths = @($sourcePath, $PSCommandPath, (Join-Path $PSScriptRoot 'integration-route-audit.py'),
    (Join-Path $PSScriptRoot 'get-integration-test-filter.ps1'), (Join-Path $PSScriptRoot 'get-test-filter.ps1'),
    (Join-Path $PSScriptRoot 'flaky-tests-quarantine.json'))
$inputHashes = @($inputPaths | ForEach-Object { @{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } })
$inputHashes | ConvertTo-Json | Set-Content (Join-Path $output 'inputs.json') -Encoding utf8NoBOM
$filter = Get-Content $sourcePath -Raw | ConvertFrom-Json
$solution = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($sourcePath)) ($filter.solution.path -replace '\\','/')))
$projects = @($filter.solution.projects | Where-Object { ($_ -replace '\\','/') -like 'tests/unit/*' })
if ($projects.Count -eq 0) { throw 'Unit routing audit has no unit projects.' }
$sha = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify candidate.' }
$contextBase = @{
    candidateSha = $sha
    runId = $(if ($env:GITHUB_RUN_ID) { $env:GITHUB_RUN_ID } else { 'local' })
    runAttempt = $(if ($env:GITHUB_RUN_ATTEMPT) { $env:GITHUB_RUN_ATTEMPT } else { '1' })
    job = 'embedded-integration-routing'; shard = [IO.Path]::GetFileNameWithoutExtension($sourcePath)
    provider = 'routing-only'
    os = $(if ($IsWindows) { 'Windows' } elseif ($IsLinux) { 'Linux' } else { 'macOS' })
    targetPlatform = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
}
function Properties([string]$Project, [string]$Framework = '') {
    $arguments = @('msbuild', $Project, '-nologo', '-p:Configuration=Release', '-p:BuildExamplesAndTests=true',
        '-getProperty:IsTestProject,TargetFramework,TargetFrameworks,TargetPath,RunSettingsFilePath')
    if ($Framework) { $arguments += "-p:TargetFramework=$Framework" }
    $raw = & $hostPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "Cannot evaluate $Project" }
    return ($raw -join "`n" | ConvertFrom-Json).Properties
}
function Discover([string]$Label, [string]$Expression, $Properties, [string]$Directory, [string]$Context) {
    [xml]$settings = Get-Content -LiteralPath $Properties.RunSettingsFilePath -Raw
    $configuration = $settings.SelectSingleNode('/RunSettings/RunConfiguration')
    if ($null -eq $configuration) { $configuration = $settings.DocumentElement.AppendChild($settings.CreateElement('RunConfiguration')) }
    foreach ($pair in @(@('TestCaseFilter', $Expression), @('TargetPlatform', $contextBase.targetPlatform))) {
        $node = $configuration.SelectSingleNode($pair[0])
        if ($null -eq $node) { $node = $configuration.AppendChild($settings.CreateElement($pair[0])) }
        $node.InnerText = $pair[1]
    }
    $settingsFile = Join-Path $Directory "$Label.runsettings"
    $settings.Save($settingsFile)
    $census = Join-Path $Directory "$Label.json"
    & (Join-Path $PSScriptRoot 'discover-required-tests.ps1') -AssemblyPath $Properties.TargetPath `
        -EffectiveRunSettings $settingsFile -SdkDirectory $sdk -ContextPath $Context -OutputPath $census
    if (-not (Test-Path $census)) { throw "Discovery did not produce $Label" }
    return $census
}
$total = 0
$assemblies = 0
foreach ($relative in $projects) {
    $normalized = $relative -replace '\\','/'
    $project = Join-Path ([IO.Path]::GetDirectoryName($solution)) $normalized
    $properties = Properties $project
    if ($properties.IsTestProject -ne 'true') { throw "Unit project is not evaluated as a test project: $normalized" }
    $frameworks = if ($properties.TargetFrameworks) { @($properties.TargetFrameworks.Split(';')) } else { @($properties.TargetFramework) }
    foreach ($framework in $frameworks) {
        $properties = Properties $project $framework
        $directory = Join-Path $output ('assembly-{0:d4}' -f $assemblies++)
        [void][IO.Directory]::CreateDirectory($directory)
        $context = $contextBase.Clone(); $context.targetFramework = $framework
        $contextPath = Join-Path $directory 'context.json'
        $context | ConvertTo-Json | Set-Content $contextPath -Encoding utf8NoBOM
        $required = Discover 'required' '(Category=Integration|Category=EndToEnd)' $properties $directory $contextPath
        $admitted = @()
        foreach ($route in $routes | Where-Object { $_.projects -contains $normalized }) {
            $composed = & (Join-Path $PSScriptRoot 'get-integration-test-filter.ps1') -Leg $route.leg | ConvertFrom-Json
            $admitted += Discover $route.name $composed.filter $properties $directory $contextPath
        }
        $count = & python (Join-Path $PSScriptRoot 'integration-route-audit.py') --required $required --admitted @admitted
        if ($LASTEXITCODE -ne 0) { throw "Unrouted integration tests in $normalized [$framework]" }
        $total += [int]$count
    }
}
$currentRoutes = & python (Join-Path $PSScriptRoot 'integration-route-audit.py') --repo $repo
if ($LASTEXITCODE -ne 0 -or ($currentRoutes -join "`n") -cne ($routeJson -join "`n")) { throw 'Scheduled routes changed during audit.' }
foreach ($inputHash in $inputHashes) {
    if ((Get-FileHash -LiteralPath $inputHash.path -Algorithm SHA256).Hash -cne $inputHash.sha256) { throw 'Routing inputs changed during audit.' }
}
@{ scope = 'Compiled embedded integration routing; not execution'; assemblies = $assemblies; integrationCases = $total } |
    ConvertTo-Json | Set-Content (Join-Path $output 'summary.json') -Encoding utf8NoBOM
Write-Host "Routing verified: $total integration cases in $assemblies evaluated unit assemblies."
