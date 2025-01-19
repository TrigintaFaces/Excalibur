param(
	[string]$Version = $env:PACKAGE_VERSION
)

if (-not $Version) {
	Write-Host "ERROR: No version specified. Pass -Version <x.y.z> or set $env:PACKAGE_VERSION."
	exit 1
}

Write-Host "Using package version: $Version"
Write-Host "Searching for .csproj files and replacing <ProjectReference> with <PackageReference>..."

$csprojFiles = Get-ChildItem -Path . -Recurse -Filter *.csproj

foreach ($file in $csprojFiles) {

	Write-Host "Processing $($file.FullName)..."
	$content = Get-Content $file.FullName
	$resultLines = New-Object System.Collections.Generic.List[string]

	# Function to extract the project name from a line containing <ProjectReference Include="..\ProjectB\ProjectB.csproj" ... />
	function Get-PackageNameFromLine($text) {
		if ($text -match 'Include="([^"]+)\.csproj') {
			$projectReferencePath = $matches[1]
			return [System.IO.Path]::GetFileNameWithoutExtension($projectReferencePath)
		}
		return $null
	}

	$insideBlock = $false

	for ($i = 0; $i -lt $content.Count; $i++) {
		$line = $content[$i]

		# CASE 1: Self-closing <ProjectReference ... />
		if ($line -match "<ProjectReference " -and $line -match "/>") {
			$packageName = Get-PackageNameFromLine $line
			if ($packageName) {
				$replacement = "    <PackageReference Include=""$packageName"" Version=""$Version"" />"
				$resultLines.Add($replacement)
			}
			else {
				# If we can't parse it, keep the line or handle differently
				$resultLines.Add($line)
			}
			continue
		}

		# CASE 2: Opening tag (multi-line)
		if ($line -match "<ProjectReference " -and $line -notmatch "/>") {
			$insideBlock = $true
			$packageName = Get-PackageNameFromLine $line
			if ($packageName) {
				$replacement = "    <PackageReference Include=""$packageName"" Version=""$Version"" />"
				$resultLines.Add($replacement)
			}
			continue
		}

		# CASE 3: Inside a ProjectReference block
		if ($insideBlock -and $line -notmatch "</ProjectReference>") {
			# skip all lines until we see the closing tag
			continue
		}

		# CASE 4: Closing tag </ProjectReference>
		if ($line -match "</ProjectReference>") {
			$insideBlock = $false
			# skip the line
			continue
		}

		# Otherwise, keep the line as is
		$resultLines.Add($line)
	}

	Set-Content $file.FullName $resultLines
}

Write-Host "ProjectReference replacement complete."
exit 0
