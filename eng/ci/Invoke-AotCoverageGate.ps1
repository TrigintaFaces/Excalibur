<#
.SYNOPSIS
    Derives AOT publish coverage from the set of projects declaring IsAotCompatible=true,
    instead of a hand-maintained sample's ProjectReference list.

.DESCRIPTION
    A hand-maintained sample's reference list and IsAotCompatible=true (inherited by default
    from src/Directory.Build.props, so the claim is automatic) are two independently-maintained
    populations that can only diverge, and only in the direction of overstating coverage
    This script closes the gap by construction: it enumerates every
    src/**/*.csproj whose EFFECTIVE IsAotCompatible is true, generates a throwaway console app
    that references each one and force-roots its assembly (TrimmerRootAssembly) -- so a
    referenced-but-never-called assembly still produces real IL2xxx/IL3xxx evidence instead of
    being silently trimmed away unanalyzed -- and delegates the actual publish + verdict to
    Invoke-AotPublishValidation.ps1, never duplicating its PASS/WARNINGS/ERROR/REFUSE logic.

    Exit codes are Invoke-AotPublishValidation.ps1's, passed through unchanged:
      0 = PASSED, 1 = WARNINGS, 2 = ERROR, 3 = REFUSE (could not determine either way).

.PARAMETER SelfTest
    Runs the declaring-set classifier's self-test and exits without generating or publishing
    anything.

.EXAMPLE
    ./Invoke-AotCoverageGate.ps1 -SelfTest
    ./Invoke-AotCoverageGate.ps1 -Configuration Release -OutputPath ./validation-results
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = '',
    [string]$OutputPath = './validation-results',
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# A project's EFFECTIVE IsAotCompatible: the project's own <IsAotCompatible> element wins;
# absent, it inherits src/Directory.Build.props' default. Read from RAW TEXT (no MSBuild
# evaluation, no build dependency) so this runs in milliseconds against 200+ files.
# The inherited default is READ from src/Directory.Build.props, never assumed. It was flipped from
# true to false when the AOT claim became opt-in, and a hardcoded default here would have kept
# classifying the one project that relies on inheritance as claiming compatibility while its own
# file explicitly declines it.
function Get-SrcInheritedAotDefault {
    param([string]$RepoRoot)

    $props = Join-Path $RepoRoot 'src/Directory.Build.props'
    if (-not (Test-Path $props)) {
        throw "REFUSE: src/Directory.Build.props not found at $props -- the inherited default is UNKNOWN, and guessing it would silently mis-classify every project that omits the element."
    }
    $text = Get-Content -Raw -Path $props
    if ($text -match '<IsAotCompatible>\s*(true|false)\s*</IsAotCompatible>') {
        return [bool]::Parse($matches[1])
    }
    throw "REFUSE: src/Directory.Build.props declares no IsAotCompatible -- the inherited default is UNKNOWN."
}

function Get-EffectiveIsAotCompatible {
    param([string]$CsprojContent, [bool]$DefaultValue = $true)

    if ($CsprojContent -match '<IsAotCompatible>\s*(true|false)\s*</IsAotCompatible>') {
        return [bool]::Parse($matches[1])
    }

    return $DefaultValue
}

if ($SelfTest) {
    # Each case names the arm it guards. The classifier must distinguish an explicit opt-out from
    # the inherited default, or the derived coverage set silently drifts the same way the sample
    # did -- this self-test is what proves the derivation can actually fail to include a project.
    $cases = @(
        @{ Name = 'explicit true stays true'; Content = '<Project><PropertyGroup><IsAotCompatible>true</IsAotCompatible></PropertyGroup></Project>'; Expect = $true }
        @{ Name = 'explicit false opts out'; Content = '<Project><PropertyGroup><IsAotCompatible>false</IsAotCompatible></PropertyGroup></Project>'; Expect = $false }
        @{ Name = 'absent element inherits the default when that default is true'; Content = '<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>'; Expect = $true; Default = $true }
        @{ Name = 'absent element inherits the default when that default is FALSE'; Content = '<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>'; Expect = $false; Default = $false }
        @{ Name = 'whitespace-tolerant around the value'; Content = "<Project><PropertyGroup>`n    <IsAotCompatible>  false  </IsAotCompatible>`n  </PropertyGroup></Project>"; Expect = $false }
    )

    $failed = 0
    foreach ($case in $cases) {
        $caseDefault = if ($case.ContainsKey('Default')) { $case.Default } else { $true }
        $result = Get-EffectiveIsAotCompatible -CsprojContent $case.Content -DefaultValue $caseDefault
        if ($result -eq $case.Expect) {
            Write-Host "  PASS  $($case.Name) -> $result"
        }
        else {
            Write-Host "  FAIL  $($case.Name): expected $($case.Expect), got $result" -ForegroundColor Red
            $failed++
        }
    }

    if ($failed -gt 0) {
        Write-Host "Self-test FAILED: $failed of $($cases.Count) cases." -ForegroundColor Red
        exit 1
    }

    Write-Host "Self-test passed: $($cases.Count) of $($cases.Count) cases." -ForegroundColor Green
    exit 0
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$srcRoot = Join-Path $repoRoot 'src'

$inheritedDefault = Get-SrcInheritedAotDefault -RepoRoot $repoRoot
Write-Host "Inherited default from src/Directory.Build.props: IsAotCompatible=$inheritedDefault" -ForegroundColor Cyan

$allCsproj = Get-ChildItem -Path $srcRoot -Filter '*.csproj' -Recurse
$declaring = @($allCsproj | Where-Object { Get-EffectiveIsAotCompatible -CsprojContent (Get-Content $_.FullName -Raw) -DefaultValue $inheritedDefault })

Write-Host "Declared IsAotCompatible=true: $($declaring.Count) of $($allCsproj.Count) src/ projects." -ForegroundColor Cyan

if ($declaring.Count -eq 0) {
    # A zero here almost certainly means the classifier regex broke, not that the repo stopped
    # claiming AOT support -- fail loud rather than silently "passing" an empty publish.
    Write-Host "ERROR: zero projects declared IsAotCompatible=true. Refusing to publish an empty coverage set." -ForegroundColor Red
    exit 2
}

# MSBuild refuses to ProjectReference an OutputType=Exe project from another self-contained
# executable (NETSDK1150) -- a hard rule, not a bug in the enumeration. A declaring project that
# is itself an app (e.g. a CLI tool) is published standalone, on its own; everything else is
# rooted together in the shared harness below.
$declaringLibraries = @($declaring | Where-Object { (Get-Content $_.FullName -Raw) -notmatch '<OutputType>\s*Exe\s*</OutputType>' })
$declaringExecutables = @($declaring | Where-Object { (Get-Content $_.FullName -Raw) -match '<OutputType>\s*Exe\s*</OutputType>' })

if ($declaringExecutables.Count -gt 0) {
    Write-Host "$($declaringExecutables.Count) declaring project(s) are executables and will be published standalone: $(($declaringExecutables | ForEach-Object { $_.BaseName }) -join ', ')" -ForegroundColor Cyan
}

# Generate the throwaway coverage harness. Regenerated every run from the current declaring set,
# so it can never drift from it the way a hand-maintained sample's reference list does.
$harnessDir = Join-Path $repoRoot 'eng' 'ci' '.aot-coverage-harness'
if (Test-Path $harnessDir) { Remove-Item $harnessDir -Recurse -Force }
New-Item -ItemType Directory -Path $harnessDir -Force | Out-Null

$projectRefs = ($declaringLibraries | ForEach-Object { "    <ProjectReference Include=`"$($_.FullName)`" />" }) -join "`n"

# DO NOT REMOVE TrimmerRootAssembly TO "SIMPLIFY" THIS HARNESS. A ProjectReference alone is not
# coverage: ILC trims away any assembly nothing in Program.cs actually calls, so a harness that
# only references all 165 packages would publish clean and prove NOTHING for the ones it never
# rooted -- while looking, to anyone reading the workflow, exactly like comprehensive coverage.
# That silent gap is what let Excalibur.Compliance ship an unverified AOT claim even
# though it WAS referenced by the old hand-maintained sample. TrimmerRootAssembly forces the
# trimmer/ILC to treat every reachable member of the named assembly as a root regardless of
# whether Program.cs calls into it, so every declaring project is actually analyzed, not merely
# present in the dependency graph. This is the one line that makes this gate worth having.
$rootAssemblies = ($declaringLibraries | ForEach-Object {
    $asm = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
    "    <TrimmerRootAssembly Include=`"$asm`" />"
}) -join "`n"

$harnessCsproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- PublishAot lives HERE, in the project, never on the CLI: -p:PublishAot=true on the
         command line cascades to every project in the graph, including the netstandard2.0
         source generators, and breaks them. -->
    <PublishAot>true</PublishAot>
    <TrimMode>full</TrimMode>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <SuppressTrimAnalysisWarnings>false</SuppressTrimAnalysisWarnings>
    <TrimmerSingleWarn>false</TrimmerSingleWarn>
    <!-- Referencing the whole declaring set intentionally couples many features; that is the point. -->
    <NoWarn>`$(NoWarn);CA1506;CA1505</NoWarn>
  </PropertyGroup>
  <ItemGroup>
$projectRefs
  </ItemGroup>
  <ItemGroup>
$rootAssemblies
  </ItemGroup>
</Project>
"@

Set-Content -Path (Join-Path $harnessDir 'Excalibur.AotCoverageHarness.csproj') -Value $harnessCsproj -Encoding utf8
Set-Content -Path (Join-Path $harnessDir 'Program.cs') -Value 'System.Console.WriteLine("aot-coverage-harness");' -Encoding utf8

Write-Host "Generated coverage harness referencing + rooting all $($declaringLibraries.Count) library projects at $harnessDir" -ForegroundColor Cyan

# Delegate the actual publish + verdict logic (PASS/WARNINGS/ERROR/REFUSE, log capture, IL2xxx/
# IL3xxx parsing, the vswhere PATH fixup) to the existing, self-tested gate -- never duplicate it.
# Publish the shared harness, then each standalone executable, into separate output subfolders so
# no run's log/report overwrites another's.
$validationScript = Join-Path $PSScriptRoot 'Invoke-AotPublishValidation.ps1'
$harnessProject = Join-Path $harnessDir 'Excalibur.AotCoverageHarness.csproj'

$runs = @(@{ Name = 'harness'; ProjectPath = $harnessProject; ProjectCount = $declaringLibraries.Count })
foreach ($exe in $declaringExecutables) {
    $runs += @{ Name = $exe.BaseName; ProjectPath = $exe.FullName; ProjectCount = 1 }
}

$exitCodes = @{}
foreach ($run in $runs) {
    Write-Host "--- Publishing: $($run.Name) ---" -ForegroundColor Cyan
    $runOutputPath = Join-Path $OutputPath $run.Name
    & $validationScript -Configuration $Configuration -Runtime $Runtime -OutputPath $runOutputPath -ProjectPath $run.ProjectPath
    $exitCodes[$run.Name] = $LASTEXITCODE
}

# Three numbers that answer different questions (do not collapse them into one "coverage %"):
#   declaring   = how many projects CLAIM IsAotCompatible (the denominator).
#   published   = how many the gate actually got far enough to ANALYZE. A run that ERRORed or
#                 REFUSEd contributes ZERO here -- a single compile failure in the shared harness
#                 blocks analysis of every project it was rooting, so there is no partial credit.
#   findings    = IL2xxx/IL3xxx warnings, counted ONLY from runs that published successfully. A
#                 failed publish's "0 warnings" is not evidence of cleanliness (same rule the
#                 underlying verdict logic already enforces for a single project).
# REFUSE is tracked separately from PASS/WARNINGS/ERROR: a REFUSEd run is UNMEASURED, not clean.
$declaringCount = $declaring.Count
$publishedCount = 0
$findingsCount = 0
$unmeasuredCount = 0
$refusedRuns = @()
$erroredRuns = @()
$analyzedWithFindingsRuns = @()
foreach ($run in $runs) {
    $code = $exitCodes[$run.Name]
    $reportPath = Join-Path (Join-Path $OutputPath $run.Name) 'aot-validation-report.json'
    $report = $null
    if (Test-Path $reportPath) { $report = Get-Content $reportPath -Raw | ConvertFrom-Json }

    if ($code -eq 0 -or $code -eq 1) {
        $publishedCount += $run.ProjectCount
        if ($report) { $findingsCount += $report.TotalWarnings }
    }
    elseif ($code -eq 3) {
        $refusedRuns += $run.Name
    }
    elseif ($report -and $report.TotalWarnings -gt 0) {
        # ANALYSIS COMPLETED AND FOUND THINGS. This is NOT a build failure, and the distinction is
        # the whole point of the gate.
        #
        # With SuppressTrimAnalysisWarnings=false, IL warnings become errors, so a publish that
        # analyses cleanly and a publish that analyses and finds 2,488 warnings BOTH exit non-zero.
        # Reading the exit code alone puts them in the same bucket -- and this gate did exactly
        # that on its first real run: it classified a harness that had successfully analysed 164
        # libraries as "could not build", and reported FINDINGS: 0 while its own report file
        # recorded 2,488 across 52 packages.
        #
        # That is the failure this gate exists to prevent, committed by the gate. So the
        # discriminator is the REPORT, not the exit code: if ILC produced findings, ILC ran.
        $publishedCount += $run.ProjectCount
        $findingsCount += $report.TotalWarnings
        $analyzedWithFindingsRuns += $run.Name
    }
    else {
        # No report, or a report with no findings behind a non-zero exit: the publish genuinely
        # did not get far enough to analyse anything. A single compile failure in the shared
        # harness blocks every project it was rooting, so there is no partial credit -- and those
        # projects are UNMEASURED, which is reported below rather than being silently absent.
        $erroredRuns += $run.Name
        $unmeasuredCount += $run.ProjectCount
    }
}

$coverageSummary = [ordered]@{
    DeclaringProjects = $declaringCount
    PublishedProjects = $publishedCount
    Findings          = $findingsCount
    RefusedRuns       = $refusedRuns
    ErroredRuns       = $erroredRuns
    UnmeasuredProjects = $unmeasuredCount
    AnalyzedWithFindingsRuns = $analyzedWithFindingsRuns
}
$coverageSummary | ConvertTo-Json | Out-File -FilePath (Join-Path $OutputPath 'aot-coverage-gate-report.json') -Encoding utf8

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  AOT Coverage Gate Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
foreach ($name in $exitCodes.Keys) {
    Write-Host "  $name -> exit $($exitCodes[$name])"
}
Write-Host "  Declaring projects: $declaringCount"
Write-Host "  Published (analyzed): $publishedCount"
Write-Host "  Findings (IL2xxx/IL3xxx): $findingsCount"
Write-Host "  Refused (unmeasured): $(if ($refusedRuns.Count) { $refusedRuns -join ', ' } else { 'none' })"
Write-Host "  Errored (could not build): $(if ($erroredRuns.Count) { $erroredRuns -join ', ' } else { 'none' })"
Write-Host "  UNMEASURED projects (no analysis reached them): $unmeasuredCount"
if ($analyzedWithFindingsRuns.Count) {
    Write-Host "  Analyzed-with-findings (published non-zero BECAUSE of findings, not a build failure): $($analyzedWithFindingsRuns -join ', ')"
}

# Worst result wins, in severity order: ERROR (could not build) > REFUSE (indeterminate) >
# WARNINGS (built, but not trim/AOT clean) > PASSED. A REFUSE on ANY run means the overall verdict
# can never read as a clean PASS, even if every other run was clean.
foreach ($severity in 2, 3, 1, 0) {
    if ($exitCodes.Values -contains $severity) {
        exit $severity
    }
}

# Unreachable if $runs is non-empty (every run sets one of the four codes above), but never let an
# empty result silently read as success.
exit 2
