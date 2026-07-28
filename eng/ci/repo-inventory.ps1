param(
  [string]$Output = "management/reports/dependency-inventory.md",
  [string]$ProjectRoot = ".",
  # 45s was too tight on a cold NuGet cache: `dotnet list package --include-transitive` resolves the
  # graph per project, and the first few projects on a fresh runner pay for the whole restore. The
  # 12-minute total budget below is the real runaway guard; this per-project limit only needs to catch
  # a genuinely hung process, not a slow one.
  [int]$PerProjectTimeoutSeconds = 150,
  [int]$TotalBudgetMinutes = 12,
  [int]$MaxDependencyProjects = 200,
  [switch]$FailOnGuardrailBreach
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null
$script:GuardrailBreached = $false
$script:TimedOutProjects = @()
$script:GlobalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$script:Budget = [TimeSpan]::FromMinutes([Math]::Max(1, $TotalBudgetMinutes))
$repoRoot = (Resolve-Path $ProjectRoot).Path

function Write-Section($title) {
  "`n## $title`n" | Out-File -FilePath $Output -Encoding UTF8 -Append
}

function Get-RelativePath {
  param(
    [string]$BasePath,
    [string]$TargetPath
  )
  $base = [System.IO.Path]::GetFullPath($BasePath)
  $target = [System.IO.Path]::GetFullPath($TargetPath)
  return [System.IO.Path]::GetRelativePath($base, $target)
}

function Get-Projects {
  param([string]$BasePath)
  Get-ChildItem -Path $BasePath -Recurse -File -Filter *.csproj |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } |
    Sort-Object FullName
}

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

function Get-ProjectTargetFrameworks {
  param([string]$ProjectPath)
  $xml = [xml](Get-Content -Raw -- $ProjectPath)
  $values = @()

  foreach ($node in @($xml.SelectNodes('//PropertyGroup/TargetFrameworks'))) {
    if ($node -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) {
      $values += $node.InnerText.Trim()
    }
  }
  foreach ($node in @($xml.SelectNodes('//PropertyGroup/TargetFramework'))) {
    if ($node -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) {
      $values += $node.InnerText.Trim()
    }
  }

  $values = @($values | Where-Object { $_ } | Sort-Object -Unique)
  if (@($values).Count -eq 0) {
    return '(unspecified)'
  }
  return ($values -join ';')
}

"# Repository Inventory and Dependency Audit`nGenerated: $(Get-Date -Format o)" | Out-File -FilePath $Output -Encoding UTF8

Write-Section "Solutions and Projects"
Get-ChildItem -Path $repoRoot -Recurse -File -Include *.sln,*.csproj |
  Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } |
  Sort-Object FullName |
  ForEach-Object { "- ``$(Get-RelativePath -BasePath (Get-Location).Path -TargetPath $_.FullName)``" } |
  Out-File -FilePath $Output -Encoding UTF8 -Append

Write-Section "Target Frameworks"
Get-Projects -BasePath $repoRoot |
  ForEach-Object {
    $tfm = Get-ProjectTargetFrameworks -ProjectPath $_.FullName
    "- ``$(Get-RelativePath -BasePath $repoRoot -TargetPath $_.FullName)`` → $tfm"
  } | Out-File -FilePath $Output -Encoding UTF8 -Append

Write-Section "Dependency Snapshot (transitive)"
try {
  $allProjects = @(Get-Projects -BasePath $repoRoot)
  $selectedProjects = @($allProjects | Select-Object -First ([Math]::Max(1, $MaxDependencyProjects)))

  if ($allProjects.Count -gt $selectedProjects.Count) {
    $script:GuardrailBreached = $true
    "- Guardrail: capped dependency snapshot to first $($selectedProjects.Count) project(s) of $($allProjects.Count)." |
      Out-File -FilePath $Output -Encoding UTF8 -Append
  }

  $index = 0
  foreach ($proj in $selectedProjects) {
    $index++
    $relative = Get-RelativePath -BasePath $repoRoot -TargetPath $proj.FullName
    Write-Progress -Activity "Repo Inventory" -Status "Dependency snapshot $index/$($selectedProjects.Count): $relative" -PercentComplete (($index / $selectedProjects.Count) * 100)
    Write-Host "[$index/$($selectedProjects.Count)] Dependency snapshot: $relative"

    # Only treat budget as a hard breach when there are still projects left to process.
    # This avoids false failures on the final iteration (e.g. "[N/N]" logged, then breach).
    if ($script:GlobalStopwatch.Elapsed -ge $script:Budget -and $index -lt $selectedProjects.Count) {
      $script:GuardrailBreached = $true
      "- Guardrail: stopped dependency snapshot after reaching total budget of $($script:Budget.TotalMinutes) minute(s)." |
        Out-File -FilePath $Output -Encoding UTF8 -Append
      break
    }

    $result = Invoke-ProcessWithTimeout -FilePath "dotnet" -Arguments @("list", $proj.FullName, "package", "--include-transitive", "--format", "json") -TimeoutSeconds $PerProjectTimeoutSeconds
    $durationMs = [Math]::Round([double]$result.DurationMs, 1)

    if ($result.TimedOut) {
      $script:GuardrailBreached = $true
      $script:TimedOutProjects += $relative
      # Also to the console. This detail used to go ONLY to the report file, so CI failed with a bare
      # "guardrails were breached (timeout/budget)" and nothing about WHICH project or WHICH limit --
      # a gate that fails without saying what it found.
      Write-Host "::warning::repo-inventory: '$relative' timed out after ${durationMs}ms (limit ${PerProjectTimeoutSeconds}s)."
      "- ``$relative`` → timeout after ${durationMs}ms (limit: ${PerProjectTimeoutSeconds}s)." |
        Out-File -FilePath $Output -Encoding UTF8 -Append
      continue
    }

    if ($result.ExitCode -ne 0) {
      "- ``$relative`` → failed (exit $($result.ExitCode)) after ${durationMs}ms." |
        Out-File -FilePath $Output -Encoding UTF8 -Append
      continue
    }

    try {
      $json = $result.StdOut | ConvertFrom-Json
      $frameworkNodes = @($json.projects.frameworks)
      $topLevelCount = 0
      $transitiveCount = 0
      foreach ($framework in $frameworkNodes) {
        $topLevelCount += @($framework.topLevelPackages).Count
        if ($framework.PSObject.Properties.Name -contains 'transitivePackages') {
          $transitiveCount += @($framework.transitivePackages).Count
        }
      }

      "- ``$relative`` → frameworks=$($frameworkNodes.Count), top-level=$topLevelCount, transitive=$transitiveCount, duration=${durationMs}ms." |
        Out-File -FilePath $Output -Encoding UTF8 -Append
    }
    catch {
      "- ``$relative`` → parse-error after ${durationMs}ms: $($_.Exception.Message)" |
        Out-File -FilePath $Output -Encoding UTF8 -Append
    }
  }

  Write-Progress -Activity "Repo Inventory" -Completed
} catch {
  "- dotnet list package failed: $($_.Exception.Message)" | Out-File -FilePath $Output -Encoding UTF8 -Append
  $script:GuardrailBreached = $true
}

Write-Section "LOC Summary (.cs)"
$files = Get-ChildItem -Path $repoRoot -Recurse -File -Include *.cs |
  Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }
$total = 0
foreach ($f in $files) {
  $content = Get-Content -ReadCount 0 -- $f.FullName
  $lines = if ($null -eq $content) { 0 } else { $content.Length }
  $total += $lines
}
"- Files: $($files.Count)" | Out-File -FilePath $Output -Encoding UTF8 -Append
"- Total LOC: $total" | Out-File -FilePath $Output -Encoding UTF8 -Append

Write-Section "End of Report"

if ($script:GuardrailBreached -and $FailOnGuardrailBreach) {
  if ($script:TimedOutProjects.Count -gt 0) {
    Write-Error ("Repo inventory: {0} project(s) exceeded the {1}s per-project limit: {2}" -f `
      $script:TimedOutProjects.Count, $PerProjectTimeoutSeconds, ($script:TimedOutProjects -join ', '))
  }
  else {
    Write-Error ("Repo inventory: total budget of {0} minute(s) exhausted with projects still unprocessed." -f `
      $script:Budget.TotalMinutes)
  }
  exit 1
}

