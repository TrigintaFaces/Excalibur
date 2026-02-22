param(
  [string]$OutDir = "DependencyAuditReport",
  [string]$ProjectRoot = "src",
  [int]$PerProjectTimeoutSeconds = 60,
  [int]$TotalBudgetMinutes = 15,
  [int]$MaxProjects = 200,
  [switch]$FailOnGuardrailBreach
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$jsonPath = Join-Path $OutDir 'vulnerabilities.json'
$sarifPath = Join-Path $OutDir 'vulnerabilities.sarif'

function Invoke-ProcessWithTimeout {
  param(
    [string]$FilePath,
    [string[]]$Arguments,
    [int]$TimeoutSeconds
  )

  $psi = [System.Diagnostics.ProcessStartInfo]::new()
  $psi.FileName = $FilePath
  foreach ($arg in $Arguments) {
    [void]$psi.ArgumentList.Add($arg)
  }
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.UseShellExecute = $false
  $psi.CreateNoWindow = $true

  $process = [System.Diagnostics.Process]::new()
  $process.StartInfo = $psi
  $watch = [System.Diagnostics.Stopwatch]::StartNew()
  [void]$process.Start()

  $stdoutTask = $process.StandardOutput.ReadToEndAsync()
  $stderrTask = $process.StandardError.ReadToEndAsync()
  $timedOut = -not $process.WaitForExit([Math]::Max(1, $TimeoutSeconds) * 1000)

  if ($timedOut) {
    try { $process.Kill($true) } catch { }
    [void]$process.WaitForExit()
  }

  [void][System.Threading.Tasks.Task]::WaitAll(@($stdoutTask, $stderrTask), 2000)
  $watch.Stop()

  [pscustomobject]@{
    ExitCode   = if ($timedOut) { -1 } else { $process.ExitCode }
    TimedOut   = $timedOut
    DurationMs = $watch.ElapsedMilliseconds
    StdOut     = $stdoutTask.Result
    StdErr     = $stderrTask.Result
  }
}

$results = @()
$guardrailBreached = $false
$budget = [TimeSpan]::FromMinutes([Math]::Max(1, $TotalBudgetMinutes))
$budgetWatch = [System.Diagnostics.Stopwatch]::StartNew()
$rootPath = if ([System.IO.Path]::IsPathRooted($ProjectRoot)) { $ProjectRoot } else { Join-Path (Get-Location).Path $ProjectRoot }

if (-not (Test-Path $rootPath -PathType Container)) {
  Write-Warning "ProjectRoot '$ProjectRoot' not found. Falling back to repository root."
  $rootPath = (Get-Location).Path
}

$allProjects = @(Get-ChildItem -Path $rootPath -Recurse -File -Filter *.csproj -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } |
  Sort-Object FullName)

$selectedProjects = @($allProjects | Select-Object -First ([Math]::Max(1, $MaxProjects)))
if ($allProjects.Count -gt $selectedProjects.Count) {
  Write-Warning "Project scan capped to $($selectedProjects.Count) of $($allProjects.Count) projects."
  $guardrailBreached = $true
}

for ($i = 0; $i -lt $selectedProjects.Count; $i++) {
  $proj = $selectedProjects[$i]
  $relativePath = [System.IO.Path]::GetRelativePath((Get-Location).Path, $proj.FullName)
  $projectNumber = $i + 1
  Write-Progress -Activity "Dependency audit" -Status "[$projectNumber/$($selectedProjects.Count)] $relativePath" -PercentComplete (($projectNumber / $selectedProjects.Count) * 100)
  Write-Host "[$projectNumber/$($selectedProjects.Count)] Auditing $relativePath"

  if ($budgetWatch.Elapsed -ge $budget) {
    Write-Warning "Total audit budget exceeded ($($budget.TotalMinutes) minute(s)); stopping early."
    $guardrailBreached = $true
    break
  }

  $run = Invoke-ProcessWithTimeout -FilePath "dotnet" -Arguments @("list", $proj.FullName, "package", "--vulnerable", "--include-transitive") -TimeoutSeconds $PerProjectTimeoutSeconds
  $output = (($run.StdOut + [Environment]::NewLine + $run.StdErr).Trim())
  if ([string]::IsNullOrWhiteSpace($output)) {
    $output = "(no output)"
  }

  if ($run.TimedOut) {
    Write-Warning "Timed out after $($run.DurationMs)ms: $relativePath"
    $guardrailBreached = $true
    $output = "TIMEOUT after $($run.DurationMs)ms`n$output"
  } elseif ($run.ExitCode -ne 0) {
    Write-Warning "Non-zero exit ($($run.ExitCode)) for $relativePath"
    $output = "FAILED (exit $($run.ExitCode))`n$output"
  }

  $results += [pscustomobject]@{
    Project     = $proj.FullName
    Relative    = $relativePath
    ExitCode    = $run.ExitCode
    TimedOut    = $run.TimedOut
    DurationMs  = $run.DurationMs
    Output      = $output
  }
}
Write-Progress -Activity "Dependency audit" -Completed

$results | ConvertTo-Json -Depth 5 | Out-File -FilePath $jsonPath -Encoding UTF8

# Emit minimal SARIF 2.1.0 with findings per line containing 'Vulnerable'
$runs = @()
foreach ($r in $results) {
  $lines = $r.Output -split "`n"
  $findings = @()
  $idx = 0
  foreach ($line in $lines) {
    if ($line -match '(?i)vulnerable') {
      $findings += @{ level = 'warning'; message = @{ text = $line.Trim() }; locations = @(@{ physicalLocation = @{ artifactLocation = @{ uri = $r.Project }; region = @{ startLine = $idx + 1 } } }) }
    }
    $idx++
  }
  $runs += @{
    tool = @{
      driver = @{
        name = 'dotnet list package'
        informationUri = 'https://learn.microsoft.com/dotnet/core/tools/dotnet-list-package'
      }
    }
    properties = @{
      project = $r.Project
      relativeProject = $r.Relative
      timedOut = $r.TimedOut
      durationMs = $r.DurationMs
      exitCode = $r.ExitCode
    }
    results = $findings
  }
}
$sarif = @{ version = '2.1.0'; '$schema' = 'https://json.schemastore.org/sarif-2.1.0.json'; runs = $runs }
$sarif | ConvertTo-Json -Depth 8 | Out-File -FilePath $sarifPath -Encoding UTF8
Write-Host "Wrote $jsonPath and $sarifPath"

if ($guardrailBreached -and $FailOnGuardrailBreach) {
  Write-Error "Dependency audit guardrails were breached (timeout/budget/project cap)."
  exit 1
}

