# REVIEW CANDIDATE. Runs the SDK's symbol target, but never compiles, updates baselines or invokes scanner.
# Scope: source Compile items present at evaluation plus execution of AddImplicitDefineConstants.
# This deliberately DOES NOT claim target-added/generated Compile items or captured compiler input.
# Invoke only after root obtains serialized native-tool permission/slots. Not executed by the author.
param(
    [Parameter(Mandatory)][string] $RepositoryRoot,
    [Parameter(Mandatory)][string] $ShippingProjectList,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath($RepositoryRoot)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Fresh output directory required; preserve old evaluation evidence.' }
New-Item -ItemType Directory -Path $output | Out-Null
$shippingProjects = @(Get-Content -LiteralPath $ShippingProjectList | Where-Object { $_.Trim() -and -not $_.StartsWith('#') })
if (!$shippingProjects.Count) { throw 'Shipping project input is empty.' }
# The historical gate governed ALL src, not merely shipping projects. Include nonshipping analyzer,
# codefix and other source projects; retain the original shipping list as a separately hashed input.
$projects = @(Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Recurse -File -Filter '*.csproj' |
    Where-Object { $_.FullName.Replace('\','/') -notmatch '/(bin|obj)/' } |
    ForEach-Object { [IO.Path]::GetRelativePath($repo, $_.FullName).Replace('\','/') })
if (!$projects.Count) { throw 'All-src project census is empty.' }
foreach ($shippingProject in $shippingProjects) {
    if ($projects -notcontains $shippingProject.Replace('\','/')) { throw "Shipping project absent from all-src census: $shippingProject" }
}
$coveredFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$scopes = [Collections.Generic.List[object]]::new()
function Evaluate([string] $project, [string[]] $properties, [string] $receipt) {
    $arguments = @('msbuild', $project, '-nologo', '-getProperty:Configuration,TargetFramework,TargetFrameworks,LangVersion,DefineConstants,ImplicitFrameworkDefine,DisableImplicitFrameworkDefines,MSBuildSDKsPath', '-getItem:Compile') + $properties
    $raw = @(& dotnet @arguments 2> "$receipt.stderr.log")
    $exitCode = $LASTEXITCODE
    [IO.File]::WriteAllText($receipt, ($raw -join "`n"), [Text.UTF8Encoding]::new($false))
    if ($exitCode -ne 0) { throw "MSBuild evaluation failed: $project ($exitCode); retain $receipt" }
    return ($raw -join "`n" | ConvertFrom-Json)
}
foreach ($relativeProject in ($projects | Sort-Object -Unique)) {
    $project = [IO.Path]::GetFullPath($relativeProject, $repo)
    $relative = [IO.Path]::GetRelativePath($repo, $project).Replace('\', '/')
    if ($relative.StartsWith('../') -or [IO.Path]::IsPathRooted($relative)) { throw 'Project escapes repository.' }
    if (![IO.File]::Exists($project)) { throw "Missing project: $relative" }
    $key = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($relative)))
    $outer = Evaluate $project @("-p:Configuration=$Configuration") (Join-Path $output "$key.outer.json")
    $frameworks = @($outer.Properties.TargetFrameworks -split ';' | Where-Object { $_ })
    if (!$frameworks.Count) { $frameworks = @($outer.Properties.TargetFramework | Where-Object { $_ }) }
    if (!$frameworks.Count) { throw "No evaluated framework for $relative" }
    foreach ($framework in $frameworks) {
        $receipt = Join-Path $output "$key.$framework.evaluated.json"
        # Confirmed on pinned SDK 10.0.401: BeforeCommon.targets:290-296.
        # AddImplicitDefineConstants depends on the four framework/platform generators and tracing filter;
        # invoking the target lets -getProperty observe their final values, unlike evaluation alone.
        $evaluation = Evaluate $project @("-p:Configuration=$Configuration", "-p:TargetFramework=$framework", '-target:AddImplicitDefineConstants') $receipt
        if ($evaluation.Properties.Configuration -ne $Configuration -or $evaluation.Properties.TargetFramework -ne $framework) {
            throw "Evaluated configuration mismatch for $relative/$framework"
        }
        $symbols = @($evaluation.Properties.DefineConstants -split ';' | Where-Object { $_ } | Sort-Object -Unique)
        if ($evaluation.Properties.DisableImplicitFrameworkDefines -eq 'true') {
            throw "Implicit defines disabled for $relative/$framework; explicit scope review required."
        }
        $implicitFramework = $evaluation.Properties.ImplicitFrameworkDefine
        if (!$implicitFramework -or $symbols -notcontains $implicitFramework) {
            throw "SDK target did not provide the expected implicit framework symbol for $relative/$framework"
        }
        if ($framework -eq 'net10.0' -and ($symbols -notcontains 'NET10_0' -or $symbols -notcontains 'NET10_0_OR_GREATER' -or $symbols -notcontains 'NET')) {
            throw 'Must-fail control: net10.0 symbol population is incomplete.'
        }
        if ($framework -eq 'netstandard2.0' -and ($symbols -notcontains 'NETSTANDARD2_0' -or $symbols -notcontains 'NETSTANDARD2_0_OR_GREATER' -or $symbols -notcontains 'NETSTANDARD')) {
            throw 'Must-fail control: netstandard2.0 symbol population is incomplete.'
        }
        $symbolTarget = Join-Path $evaluation.Properties.MSBuildSDKsPath 'Microsoft.NET.Sdk/targets/Microsoft.NET.Sdk.BeforeCommon.targets'
        if (!(Test-Path -LiteralPath $symbolTarget -PathType Leaf)) { throw 'SDK symbol target source is unavailable.' }
        $files = @($evaluation.Items.Compile | ForEach-Object {
            if (!$_.FullPath) { throw "Compile item missing FullPath metadata: $relative/$framework" }
            $path = [IO.Path]::GetFullPath($_.FullPath)
            if (![IO.File]::Exists($path)) { throw "Compile item unavailable: $path; generate it through the normal owned build before evaluation." }
            [IO.Path]::GetRelativePath($repo, $path).Replace('\', '/')
        } | Sort-Object -Unique)
        foreach ($file in $files) { [void]$coveredFiles.Add($file) }
        $manifest = [ordered]@{
            project = $relative; configuration = $Configuration; targetFramework = $framework
            langVersion = $evaluation.Properties.LangVersion
            populationMode = 'evaluatedCompileItems'
            sourcePopulationScope = 'Evaluated Compile items with SDK AddImplicitDefineConstants executed; excludes items added by other build targets and generator-produced sources. Not compiled population completeness.'
            symbolTarget = 'AddImplicitDefineConstants'
            symbolTargetSourceSha256 = (Get-FileHash -LiteralPath $symbolTarget -Algorithm SHA256).Hash
            symbols = $symbols
            sourceFiles = $files
            evaluatedReceiptSha256 = (Get-FileHash -LiteralPath $receipt -Algorithm SHA256).Hash
        }
        if (!$manifest.langVersion) { throw "No evaluated LangVersion for $relative/$framework" }
        if (!$files.Count) {
            # Explicit evaluated result, not an inferred skip. The raw receipt proves zero Compile items.
            $manifest.zeroDeclarationDisposition = 'evaluated-source-free'
        }
        $manifestPath = Join-Path $output "$key.$framework.manifest.json"
        [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
        $scopes.Add([ordered]@{ manifest = $manifestPath; sha256 = (Get-FileHash $manifestPath -Algorithm SHA256).Hash })
    }
}
$residuals = @(Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName.Replace('\','/') -notmatch '/(bin|obj)/' } | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($repo, $_.FullName).Replace('\','/')
        if (!$coveredFiles.Contains($relative)) {
            [ordered]@{ path=$relative; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
                disposition='uncovered-by-evaluated-compile-items; explicit review required; never silently ignored' }
        }
    })
[ordered]@{
    status = 'evaluated only; scanner and review not run'; configuration = $Configuration
    shippingProjectListSha256 = (Get-FileHash -LiteralPath $ShippingProjectList -Algorithm SHA256).Hash
    governedSourcePopulation = 'All src/**/*.cs excluding bin and obj, matching historical directory scope; configuration-conditional branches remain scoped to explicitly evaluated TFMs/configurations.'
    governedProjects = @($projects | Sort-Object -Unique)
    sourceCoverageComplete = ($residuals.Count -eq 0)
    uncoveredSourceFiles = $residuals
    scopes = $scopes.ToArray()
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'scope-index.json') -Encoding utf8
