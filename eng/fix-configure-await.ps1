<#
.SYNOPSIS
    Adds ConfigureAwait(false) to await expressions in library code.

.DESCRIPTION
    Scans all .cs files under src/Dispatch/ and src/Excalibur/ (excluding obj/,
    SourceGenerators/, and .g.cs files) and adds .ConfigureAwait(false) to await
    expressions that don't already have it.

    Handles:
    - Single-line await expressions ending with ;
    - Multi-line await expressions (looks ahead for the terminating ; line)
    - await foreach patterns (paren-depth aware)
    - await using var declarations
    - await using (expr) block forms (paren-depth aware)

    Skips:
    - Lines already containing ConfigureAwait (on same or continuation lines)
    - await Task.Yield()
    - Lines in comments (// or /* */)
    - Lines in strings
    - Files in obj/, SourceGenerators/, .g.cs
    - GetAwaiter() patterns

.EXAMPLE
    .\eng\fix-configure-await.ps1
    .\eng\fix-configure-await.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$searchDirs = @(
    Join-Path $repoRoot 'src\Dispatch'
    Join-Path $repoRoot 'src\Excalibur'
)

$totalFilesChanged = 0
$totalLinesChanged = 0
$manualReview = [System.Collections.Generic.List[string]]::new()

function Test-LineIsComment {
    param([string]$Line)
    $t = $Line.TrimStart()
    return ($t.StartsWith('//') -or $t.StartsWith('///') -or $t.StartsWith('*'))
}

function Get-ParenDepth {
    <#
    .SYNOPSIS
        Counts net parenthesis depth in a string (open minus close).
    #>
    param([string]$Text)
    $depth = 0
    foreach ($ch in $Text.ToCharArray()) {
        if ($ch -eq '(') { $depth++ }
        elseif ($ch -eq ')') { $depth-- }
    }
    return $depth
}

function Find-AwaitTerminator {
    <#
    .SYNOPSIS
        Starting from $StartLine, scans forward to find the line where the await
        expression terminates (line ending with ;). Returns the index of that line,
        or -1 if not found within the look-ahead window.
        Also returns $true if ConfigureAwait is already present in the span.
    #>
    param(
        [string[]]$Lines,
        [int]$StartLine
    )

    $hasConfigureAwait = $false

    for ($j = $StartLine; $j -lt [Math]::Min($StartLine + 50, $Lines.Length); $j++) {
        $scanLine = $Lines[$j]

        if ($scanLine -match 'ConfigureAwait') {
            $hasConfigureAwait = $true
        }

        $trimScan = $scanLine.TrimStart()

        # The expression terminates when we hit a line ending with ;
        if ($trimScan -match ';\s*$') {
            return @{ Index = $j; HasConfigureAwait = $hasConfigureAwait }
        }
    }

    return @{ Index = -1; HasConfigureAwait = $hasConfigureAwait }
}

function Find-ParenTerminator {
    <#
    .SYNOPSIS
        Tracks parenthesis depth to find where a paren-delimited block closes.
        Used for both await foreach (...) and await using (expr).
        Returns the line index where parens balance and whether ConfigureAwait is present.
    #>
    param(
        [string[]]$Lines,
        [int]$StartLine
    )

    $hasConfigureAwait = $false
    $depth = 0
    $started = $false

    for ($j = $StartLine; $j -lt [Math]::Min($StartLine + 30, $Lines.Length); $j++) {
        $scanLine = $Lines[$j]

        if ($scanLine -match 'ConfigureAwait') {
            $hasConfigureAwait = $true
        }

        foreach ($ch in $scanLine.ToCharArray()) {
            if ($ch -eq '(') { $depth++; $started = $true }
            elseif ($ch -eq ')') { $depth-- }
        }

        # When depth returns to 0 after starting, the (...) is complete
        if ($started -and $depth -eq 0) {
            return @{ Index = $j; HasConfigureAwait = $hasConfigureAwait }
        }
    }

    return @{ Index = -1; HasConfigureAwait = $hasConfigureAwait }
}

foreach ($dir in $searchDirs) {
    if (-not (Test-Path $dir)) {
        Write-Warning "Directory not found: $dir"
        continue
    }

    $files = Get-ChildItem -Path $dir -Filter '*.cs' -Recurse |
        Where-Object {
            $rel = $_.FullName
            $rel -notmatch '[/\\]obj[/\\]' -and
            $rel -notmatch '[/\\]SourceGenerators[/\\]' -and
            $rel -notmatch '\.g\.cs$'
        }

    foreach ($file in $files) {
        $lines = [System.IO.File]::ReadAllLines($file.FullName)
        $changed = 0
        $inBlockComment = $false
        $skipUntilLine = -1

        for ($i = 0; $i -lt $lines.Length; $i++) {
            # Skip lines we've already processed as part of a multi-line await
            if ($i -le $skipUntilLine) { continue }

            $line = $lines[$i]
            $trimmed = $line.TrimStart()

            # Track block comments
            if ($inBlockComment) {
                if ($trimmed.Contains('*/')) {
                    $inBlockComment = $false
                }
                continue
            }
            if ($trimmed.StartsWith('/*')) {
                $inBlockComment = $true
                if ($trimmed.Contains('*/')) {
                    $inBlockComment = $false
                }
                continue
            }

            # Skip line comments and XML doc comments
            if (Test-LineIsComment $line) {
                continue
            }

            # Skip lines that don't contain 'await'
            if ($line -notmatch '\bawait\b') {
                continue
            }

            # Skip lines already having ConfigureAwait
            if ($line -match 'ConfigureAwait') {
                continue
            }

            # Skip GetAwaiter patterns
            if ($line -match 'GetAwaiter') {
                continue
            }

            # Skip await Task.Yield()
            if ($line -match 'await\s+Task\.Yield\s*\(\s*\)') {
                continue
            }

            # Skip lines where await appears inside a string literal (rough heuristic)
            if ($line -match '"[^"]*await[^"]*"') {
                continue
            }

            # --- await foreach ---
            if ($line -match '\bawait\s+foreach\s*\(') {
                # Use paren-depth tracking to determine if the foreach (...) closes on this line
                $parenDepth = Get-ParenDepth $trimmed
                if ($parenDepth -eq 0) {
                    # Single-line: await foreach (var x in expr)
                    # Insert .ConfigureAwait(false) before the last )
                    $newLine = $line -replace '\)\s*$', '.ConfigureAwait(false))'
                    if ($newLine -ne $line) {
                        $lines[$i] = $newLine
                        $changed++
                    }
                } else {
                    # Multi-line await foreach -- track parens to find closing line
                    $result = Find-ParenTerminator $lines $i
                    if ($result.HasConfigureAwait) {
                        $skipUntilLine = $result.Index
                    } elseif ($result.Index -ge 0) {
                        # Need to add .ConfigureAwait(false) before the closing )
                        $termLine = $lines[$result.Index]
                        $newTermLine = $termLine -replace '\)\s*$', '.ConfigureAwait(false))'
                        if ($newTermLine -ne $termLine) {
                            $lines[$result.Index] = $newTermLine
                            $changed++
                        }
                        $skipUntilLine = $result.Index
                    } else {
                        $manualReview.Add("$($file.FullName):$($i + 1) (await foreach, multi-line)")
                    }
                }
                continue
            }

            # --- await using (block form) ---
            # Distinguish block form "await using (expr)" from declaration form "await using var x = expr;"
            if ($line -match '\bawait\s+using\s*\(') {
                # Block form: await using (expr) { ... }
                $parenDepth = Get-ParenDepth $trimmed
                if ($parenDepth -eq 0) {
                    # Single-line: await using (var x = expr)
                    # Insert .ConfigureAwait(false) before the last )
                    $newLine = $line -replace '\)\s*$', '.ConfigureAwait(false))'
                    if ($newLine -ne $line) {
                        $lines[$i] = $newLine
                        $changed++
                    }
                } else {
                    # Multi-line block-form await using
                    $result = Find-ParenTerminator $lines $i
                    if ($result.HasConfigureAwait) {
                        $skipUntilLine = $result.Index
                    } elseif ($result.Index -ge 0) {
                        $termLine = $lines[$result.Index]
                        $newTermLine = $termLine -replace '\)\s*$', '.ConfigureAwait(false))'
                        if ($newTermLine -ne $termLine) {
                            $lines[$result.Index] = $newTermLine
                            $changed++
                        }
                        $skipUntilLine = $result.Index
                    } else {
                        $manualReview.Add("$($file.FullName):$($i + 1) (await using block, multi-line)")
                    }
                }
                continue
            }

            # --- await using var (declaration form) ---
            if ($line -match '\bawait\s+using\b') {
                if ($trimmed.EndsWith(';')) {
                    # Insert .ConfigureAwait(false) before the trailing ;
                    $newLine = $line -replace ';\s*$', '.ConfigureAwait(false);'
                    if ($newLine -ne $line) {
                        $lines[$i] = $newLine
                        $changed++
                    }
                } else {
                    # Multi-line await using declaration -- look ahead
                    $result = Find-AwaitTerminator $lines $i
                    if ($result.HasConfigureAwait) {
                        $skipUntilLine = $result.Index
                    } elseif ($result.Index -ge 0) {
                        $termLine = $lines[$result.Index]
                        $newTermLine = $termLine -replace ';\s*$', '.ConfigureAwait(false);'
                        if ($newTermLine -ne $termLine) {
                            $lines[$result.Index] = $newTermLine
                            $changed++
                        }
                        $skipUntilLine = $result.Index
                    } else {
                        $manualReview.Add("$($file.FullName):$($i + 1) (await using, multi-line)")
                    }
                }
                continue
            }

            # --- Standard await ---
            if ($trimmed.EndsWith(';')) {
                # Single-line await ending with ;
                $newLine = $line -replace ';\s*$', '.ConfigureAwait(false);'
                if ($newLine -ne $line) {
                    $lines[$i] = $newLine
                    $changed++
                }
            } else {
                # Multi-line await -- look ahead for ConfigureAwait or terminator
                $result = Find-AwaitTerminator $lines $i
                if ($result.HasConfigureAwait) {
                    # Already has ConfigureAwait in the continuation -- skip
                    $skipUntilLine = $result.Index
                } elseif ($result.Index -ge 0) {
                    # Found terminator line without ConfigureAwait -- add it
                    $termLine = $lines[$result.Index]
                    $newTermLine = $termLine -replace ';\s*$', '.ConfigureAwait(false);'
                    if ($newTermLine -ne $termLine) {
                        $lines[$result.Index] = $newTermLine
                        $changed++
                    }
                    $skipUntilLine = $result.Index
                } else {
                    # Could not find terminator -- flag for manual review
                    $manualReview.Add("$($file.FullName):$($i + 1) (multi-line, no terminator found)")
                }
            }
        }

        if ($changed -gt 0) {
            $totalFilesChanged++
            $totalLinesChanged += $changed
            Write-Host "  $changed changes: $($file.FullName)"

            if (-not $DryRun) {
                [System.IO.File]::WriteAllLines($file.FullName, $lines)
            }
        }
    }
}

Write-Host ""
Write-Host "=== Summary ==="
Write-Host "Files changed: $totalFilesChanged"
Write-Host "Lines changed: $totalLinesChanged"
if ($DryRun) {
    Write-Host "(DRY RUN - no files were written)"
}

if ($manualReview.Count -gt 0) {
    Write-Host ""
    Write-Host "=== Requiring manual review ($($manualReview.Count)) ==="
    foreach ($entry in $manualReview) {
        Write-Host "  $entry"
    }
}
