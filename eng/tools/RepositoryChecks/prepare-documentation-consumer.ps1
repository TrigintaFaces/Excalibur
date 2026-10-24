param(
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath($RepositoryRoot)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Documentation consumer evidence must use a fresh directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$globals = @('-p:Configuration=Release', '-p:BuildExamplesAndTests=true', "-p:SolutionDir=$repo/", '-p:SolutionName=Excalibur')
function Invoke-Recorded([string]$Name, [string[]]$Arguments) {
    $log = Join-Path $output "$Name.log"
    $raw = & dotnet @Arguments 2>&1
    $code = $LASTEXITCODE
    $raw | Set-Content -LiteralPath $log -Encoding utf8
    if ($code -ne 0) { throw "Documentation consumer $Name failed ($code). See $log`n$($raw -join "`n")" }
    return ($raw -join "`n")
}
$projects = @(Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Recurse -Filter '*.csproj' -File |
    Where-Object { $_.FullName.Split([IO.Path]::DirectorySeparatorChar) -notcontains 'obj' -and $_.FullName.Split([IO.Path]::DirectorySeparatorChar) -notcontains 'bin' } |
    Sort-Object FullName)
if (!$projects.Count) { throw 'No source project population.' }
$selected = [Collections.Generic.List[string]]::new()
$evaluated = [Collections.Generic.List[object]]::new()
foreach ($project in $projects) {
    $name = [IO.Path]::GetFileNameWithoutExtension($project.Name)
    $properties = (Invoke-Recorded "evaluate-$name" (@('msbuild', $project.FullName, '-nologo',
        '-getProperty:IsPackable,IsRoslynComponent,DevelopmentDependency,OutputType,IncludeBuildOutput,TargetFramework,TargetFrameworks,AssemblyName') + $globals) | ConvertFrom-Json).Properties
    $library = $properties.IsPackable -eq 'true' -and $properties.OutputType -eq 'Library' -and
        $properties.IsRoslynComponent -ne 'true' -and $properties.DevelopmentDependency -ne 'true' -and $properties.IncludeBuildOutput -ne 'false'
    $evaluated.Add(@{ project=$project.FullName; included=$library; properties=$properties })
    if ($library) { $selected.Add($project.FullName) }
}
if (!$selected.Count) { throw 'No runtime library projects selected.' }
$evaluated | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'projects.json') -Encoding utf8
Write-Host "Documentation consumer: $($selected.Count) runtime libraries selected from $($projects.Count) evaluated source projects."
function Get-SourceLocks {
    Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Recurse -Filter 'packages.lock.json' -File |
        Where-Object { $_.FullName.Split([IO.Path]::DirectorySeparatorChar) -notcontains 'obj' -and $_.FullName.Split([IO.Path]::DirectorySeparatorChar) -notcontains 'bin' } |
        Sort-Object FullName | ForEach-Object { "$($_.FullName) $((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)" }
}
$before = @(Get-SourceLocks)
$before | Set-Content -LiteralPath (Join-Path $output 'source-locks-before.txt') -Encoding utf8
# This fresh consumer has its own build configuration. Referenced projects retain their repository configuration.
foreach ($file in @('Directory.Build.props', 'Directory.Build.targets')) {
    '<Project />' | Set-Content -LiteralPath (Join-Path $output $file) -Encoding utf8
}
# ProjectReference consumers share this repository's central dependency pins. NuGet package
# consumption (including promotion of transitive security pins into nuspecs) is checked separately.
('<Project><Import Project="' + [Security.SecurityElement]::Escape((Join-Path $repo 'Directory.Packages.props')) + '" /></Project>') |
    Set-Content -LiteralPath (Join-Path $output 'Directory.Packages.props') -Encoding utf8
$references = @($selected | ForEach-Object { '<ProjectReference Include="' + [Security.SecurityElement]::Escape($_) + '" />' }) -join "`n"
$consumer = Join-Path $output 'DocumentationConsumer.csproj'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>$references</ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $consumer -Encoding utf8
$null = Invoke-Recorded 'restore' (@('restore', $consumer, '--disable-parallel', '-m:1', '-warnaserror') + $globals)
$after = @(Get-SourceLocks)
$after | Set-Content -LiteralPath (Join-Path $output 'source-locks-after.txt') -Encoding utf8
if (Compare-Object $before $after) { throw 'Documentation preparation changed source dependency locks.' }
$null = Invoke-Recorded 'locked-restore' (@('restore', $consumer, '--locked-mode', '--disable-parallel', '-m:1', '-warnaserror') + $globals)
$null = Invoke-Recorded 'build' (@('build', $consumer, '--no-restore', '-m:1', '-warnaserror') + $globals)
$resolved = Invoke-Recorded 'references' (@('msbuild', $consumer, '-nologo', '-target:ResolveReferences', '-p:BuildProjectReferences=false', '-getItem:ReferencePath', '-warnaserror') + $globals) | ConvertFrom-Json
$finalLocks = @(Get-SourceLocks)
$finalLocks | Set-Content -LiteralPath (Join-Path $output 'source-locks-final.txt') -Encoding utf8
if (Compare-Object $before $finalLocks) { throw 'Documentation build changed source dependency locks.' }
$paths = @($resolved.Items.ReferencePath | ForEach-Object { $_.FullPath } | Sort-Object -Unique)
if (!$paths.Count) { throw 'MSBuild returned no consumer reference population.' }
foreach ($path in $paths) { if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing resolved reference: $path" } }
$developmentAssemblies = @($evaluated | Where-Object { $_.properties.IsRoslynComponent -eq 'true' -or $_.properties.DevelopmentDependency -eq 'true' } | ForEach-Object { $_.properties.AssemblyName })
foreach ($path in $paths) {
    if ($developmentAssemblies -contains [IO.Path]::GetFileNameWithoutExtension($path)) {
        throw "Analyzer implementation leaked into consumer compile references: $path"
    }
}
@{ scope='Release/net10.0 ProjectReference consumer; installed package assets are verified separately'; projects=$evaluated; references=$paths } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'consumer.json') -Encoding utf8
