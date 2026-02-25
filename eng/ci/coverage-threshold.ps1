# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

param(
	[Parameter(Mandatory = $true)]
	[string]$CoverageRoot,
	[int]$Threshold = 60,
	[switch]$FailBelowThreshold
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-NormalizedSourcePath {
	param([string]$Path)

	if ([string]::IsNullOrWhiteSpace($Path)) {
		return $null
	}

	$normalized = $Path.Trim().Replace("\", "/")

	# Handle SourceLink URLs (https://raw.githubusercontent.com/.../src/...)
	$uri = $null
	if ([Uri]::TryCreate($normalized, [UriKind]::Absolute, [ref]$uri)) {
		try {
			$normalized = $uri.AbsolutePath
		} catch {
			# Fall through and attempt regex extraction.
		}
	}

	if ($normalized -match '(?i)(src/(Dispatch|Excalibur)/.+)$') {
		return $Matches[1]
	}

	return $null
}

function Get-IntAttributeOrDefault {
	param(
		[System.Xml.XmlElement]$Element,
		[string]$Name,
		[int]$Default = 0
	)

	$value = [string]$Element.GetAttribute($Name)
	if ([string]::IsNullOrWhiteSpace($value)) {
		return $Default
	}

	try {
		return [int]$value
	} catch {
		return $Default
	}
}

function Get-LineStatsForClass {
	param([System.Xml.XmlElement]$ClassElement)

	$linesCoveredAttr = [string]$ClassElement.GetAttribute("lines-covered")
	$linesValidAttr = [string]$ClassElement.GetAttribute("lines-valid")
	if (-not [string]::IsNullOrWhiteSpace($linesCoveredAttr) -and -not [string]::IsNullOrWhiteSpace($linesValidAttr)) {
		return [pscustomobject]@{
			Covered = [int]$linesCoveredAttr
			Valid = [int]$linesValidAttr
		}
	}

	$covered = 0
	$valid = 0
	foreach ($line in $ClassElement.SelectNodes("lines/line")) {
		$valid++
		if ([int]$line.hits -gt 0) {
			$covered++
		}
	}

	return [pscustomobject]@{
		Covered = $covered
		Valid = $valid
	}
}

function Add-LineHit {
	param(
		[hashtable]$LineHits,
		[string]$Path,
		[int]$LineNumber,
		[int]$Hits
	)

	$key = "$Path`:$LineNumber"
	if ($LineHits.ContainsKey($key)) {
		if ($Hits -gt [int]$LineHits[$key]) {
			$LineHits[$key] = $Hits
		}
		return
	}

	$LineHits[$key] = $Hits
}

$files = @(Get-ChildItem -Path $CoverageRoot -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue)
if ($files.Count -eq 0) {
	Write-Error "No coverage.cobertura.xml files found under '$CoverageRoot'."
	exit 1
}

[hashtable]$lineHitsByKey = @{}
[hashtable]$classFallbackByKey = @{}

foreach ($file in $files) {
	$xml = [xml](Get-Content -Raw -- $file.FullName)
	foreach ($clsNode in $xml.SelectNodes("//class")) {
		$cls = [System.Xml.XmlElement]$clsNode
		$sourcePath = Get-NormalizedSourcePath -Path ([string]$cls.GetAttribute("filename"))
		if ($null -eq $sourcePath) {
			continue
		}

		$lineNodes = $cls.SelectNodes("lines/line")
		if ($lineNodes.Count -gt 0) {
			foreach ($lineNode in $lineNodes) {
				$line = [System.Xml.XmlElement]$lineNode
				$lineNumber = Get-IntAttributeOrDefault -Element $line -Name "number" -Default -1
				if ($lineNumber -lt 1) {
					continue
				}

				$hits = Get-IntAttributeOrDefault -Element $line -Name "hits" -Default 0
				Add-LineHit -LineHits $lineHitsByKey -Path $sourcePath -LineNumber $lineNumber -Hits $hits
			}

			continue
		}

		# Fallback for coverage formats that don't include per-line entries.
		$stats = Get-LineStatsForClass -ClassElement $cls
		if ($stats.Valid -le 0) {
			continue
		}

		$className = [string]$cls.GetAttribute("name")
		if ([string]::IsNullOrWhiteSpace($className)) {
			$className = "<unknown>"
		}

		$fallbackKey = "$sourcePath`::$className"
		if ($classFallbackByKey.ContainsKey($fallbackKey)) {
			$existing = $classFallbackByKey[$fallbackKey]
			$existing.Covered = [math]::Max([int]$existing.Covered, [int]$stats.Covered)
			$existing.Valid = [math]::Max([int]$existing.Valid, [int]$stats.Valid)
			continue
		}

		$classFallbackByKey[$fallbackKey] = [pscustomobject]@{
			Path = $sourcePath
			Covered = [int]$stats.Covered
			Valid = [int]$stats.Valid
		}
	}
}

[int]$dispatchCovered = 0
[int]$dispatchValid = 0
[int]$excaliburCovered = 0
[int]$excaliburValid = 0

foreach ($entry in $lineHitsByKey.GetEnumerator()) {
	$key = [string]$entry.Key
	$hits = [int]$entry.Value

	$separatorIndex = $key.LastIndexOf(":")
	if ($separatorIndex -lt 1) {
		continue
	}

	$path = $key.Substring(0, $separatorIndex)
	$isCovered = $hits -gt 0

	if ($path.StartsWith("src/Dispatch/", [StringComparison]::OrdinalIgnoreCase)) {
		$dispatchValid++
		if ($isCovered) {
			$dispatchCovered++
		}
		continue
	}

	if ($path.StartsWith("src/Excalibur/", [StringComparison]::OrdinalIgnoreCase)) {
		$excaliburValid++
		if ($isCovered) {
			$excaliburCovered++
		}
	}
}

foreach ($fallback in $classFallbackByKey.Values) {
	if ($fallback.Path.StartsWith("src/Dispatch/", [StringComparison]::OrdinalIgnoreCase)) {
		$dispatchCovered += [int]$fallback.Covered
		$dispatchValid += [int]$fallback.Valid
		continue
	}

	if ($fallback.Path.StartsWith("src/Excalibur/", [StringComparison]::OrdinalIgnoreCase)) {
		$excaliburCovered += [int]$fallback.Covered
		$excaliburValid += [int]$fallback.Valid
	}
}

$totalCovered = $dispatchCovered + $excaliburCovered
$totalValid = $dispatchValid + $excaliburValid

if ($totalValid -eq 0) {
	Write-Error "No coverable lines found for src/Dispatch or src/Excalibur."
	exit 1
}

$dispatchPct = if ($dispatchValid -gt 0) { [math]::Round(100.0 * $dispatchCovered / $dispatchValid, 2) } else { 0.0 }
$excaliburPct = if ($excaliburValid -gt 0) { [math]::Round(100.0 * $excaliburCovered / $excaliburValid, 2) } else { 0.0 }
$totalPct = [math]::Round(100.0 * $totalCovered / $totalValid, 2)

Write-Host "Coverage (src/Dispatch, dedup lines): $dispatchPct% ($dispatchCovered/$dispatchValid)"
Write-Host "Coverage (src/Excalibur, dedup lines): $excaliburPct% ($excaliburCovered/$excaliburValid)"
Write-Host "Coverage (combined, dedup lines): $totalPct% ($totalCovered/$totalValid); threshold=$Threshold%"

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
	Add-Content -Path $env:GITHUB_OUTPUT -Value "coverage_dispatch_pct=$dispatchPct"
	Add-Content -Path $env:GITHUB_OUTPUT -Value "coverage_excalibur_pct=$excaliburPct"
	Add-Content -Path $env:GITHUB_OUTPUT -Value "coverage_combined_pct=$totalPct"
}

if ($FailBelowThreshold -and $totalPct -lt $Threshold) {
	Write-Error "Coverage below threshold: combined $totalPct% < required $Threshold%."
	exit 1
}
