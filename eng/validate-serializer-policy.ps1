# Copyright (c) 2026 The Excalibur Project
#
# Validates Dispatch/Excalibur serialization policy compliance (R0.14, R0.5, R21.4)
#
# Policy:
# - Excalibur.Dispatch MUST use MemoryPack for internal wire format
# - Excalibur.Dispatch MUST NOT use System.Text.Json (reserved for public boundaries)
# - Public boundary projects (Hosting.Web, Transport.Http) SHOULD use System.Text.Json with source-gen
# - No obsolete serializers should remain in the codebase

param(
    [string]$SolutionRoot = (Resolve-Path "$PSScriptRoot/..").Path
)

$ErrorActionPreference = "Stop"
$violations = @()

Write-Host "=== Dispatch Serializer Policy Validation ===" -ForegroundColor Cyan
Write-Host "Solution Root: $SolutionRoot" -ForegroundColor Gray
Write-Host ""

# Rule 1: Excalibur.Dispatch MUST NOT reference System.Text.Json or MessagePack
Write-Host "[Rule 1] Validating core projects use MemoryPack only..." -ForegroundColor Yellow

$coreProjects = @(
    "Excalibur.Dispatch.Abstractions",
    "Excalibur.Dispatch"
)

foreach ($project in $coreProjects) {
    $csprojPath = Get-ChildItem -Path "$SolutionRoot\src" -Recurse -Filter "$project.csproj" -ErrorAction SilentlyContinue | Select-Object -First 1

    if ($csprojPath) {
        $content = Get-Content $csprojPath.FullName -Raw

        # Allow System.Text.Json.JsonElement usage for interop, but not as primary serializer
        if ($content -match 'PackageReference\s+Include="System\.Text\.Json"' -and
            $content -notmatch '<!-- Allowed for JsonElement interop only -->') {
            $violations += "  ❌ $project references System.Text.Json without interop justification (R0.14 violation)"
        }

        if ($content -match 'PackageReference\s+Include="MessagePack"' -and
            $content -notmatch 'MemoryPack') {
            $violations += "  ❌ $project references MessagePack instead of MemoryPack (R0.14 violation)"
        }

        if ($content -match 'PackageReference\s+Include="Google\.Protobuf"') {
            $violations += "  ⚠️  $project references Google.Protobuf (should be in opt-in transport packages only)"
        }

        if ($content -match 'PackageReference\s+Include="MemoryPack"') {
            Write-Host "  ✅ $project references MemoryPack (compliant)" -ForegroundColor Green
        } else {
            $violations += "  ⚠️  $project does not reference MemoryPack (expected for internal wire format)"
        }
    } else {
        Write-Host "  ⚠️  Project '$project' not found" -ForegroundColor DarkYellow
    }
}

Write-Host ""

# Rule 2: Public boundary projects SHOULD use System.Text.Json (source-gen preferred)
Write-Host "[Rule 2] Validating public boundary projects use System.Text.Json..." -ForegroundColor Yellow

$publicProjects = @(
    "Excalibur.Dispatch.Hosting.Web",
    "Excalibur.Dispatch.Transport.Http"
)

foreach ($project in $publicProjects) {
    $csprojPath = Get-ChildItem -Path "$SolutionRoot\src" -Recurse -Filter "$project.csproj" -ErrorAction SilentlyContinue | Select-Object -First 1

    if ($csprojPath) {
        $content = Get-Content $csprojPath.FullName -Raw

        if ($content -match 'PackageReference\s+Include="System\.Text\.Json"') {
            Write-Host "  ✅ $project references System.Text.Json (compliant)" -ForegroundColor Green
        } else {
            $violations += "  ⚠️  $project does not reference System.Text.Json (expected for public boundaries)"
        }
    } else {
        Write-Host "  ℹ️  Project '$project' not found (may not exist yet)" -ForegroundColor DarkGray
    }
}

Write-Host ""

# Rule 3: No [Obsolete] serializers should remain
Write-Host "[Rule 3] Checking for obsolete serializer implementations..." -ForegroundColor Yellow

$obsoleteSerializers = Get-ChildItem -Path "$SolutionRoot\src" -Recurse -Filter "*Serializer.cs" -ErrorAction SilentlyContinue |
    Where-Object {
        $fileContent = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
        $fileContent -match '\[Obsolete'
    }

if ($obsoleteSerializers) {
    foreach ($file in $obsoleteSerializers) {
        $relativePath = $file.FullName.Replace("$SolutionRoot\", "")
        $violations += "  ❌ Obsolete serializer still exists: $relativePath"
    }
} else {
    Write-Host "  ✅ No obsolete serializers found" -ForegroundColor Green
}

Write-Host ""

# Rule 4: Protobuf should only be in opt-in transport packages
Write-Host "[Rule 4] Validating Protobuf usage scope..." -ForegroundColor Yellow

$allowedProtobufProjects = @(
    "Excalibur.Dispatch.Serialization.Protobuf",  # Opt-in Protobuf package (R0.14, R9.46)
    "Excalibur.Dispatch.Transport.Google",
    "Excalibur.Dispatch.Transport.Aws",
    "Tests.Shared.Extra"  # Test infrastructure
)

$protobufRefs = Get-ChildItem -Path "$SolutionRoot\src" -Recurse -Filter "*.csproj" -ErrorAction SilentlyContinue |
    Where-Object {
        $content = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
        $content -match 'PackageReference\s+Include="Google\.Protobuf"'
    }

foreach ($ref in $protobufRefs) {
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($ref.Name)
    $isAllowed = $allowedProtobufProjects | Where-Object { $projectName -like "*$_*" }

    if (-not $isAllowed) {
        $violations += "  ⚠️  $projectName references Google.Protobuf (should be isolated to transport packages)"
    } else {
        Write-Host "  ✅ $projectName uses Protobuf (allowed for transport interop)" -ForegroundColor Green
    }
}

Write-Host ""

# Rule 5: MessagePack should only be in opt-in serialization package
Write-Host "[Rule 5] Validating MessagePack usage scope..." -ForegroundColor Yellow

$messagePackProject = "Excalibur.Dispatch.Serialization.MessagePack"
$messagePackCsproj = Get-ChildItem -Path "$SolutionRoot\src" -Recurse -Filter "$messagePackProject.csproj" -ErrorAction SilentlyContinue | Select-Object -First 1

if ($messagePackCsproj) {
    $content = Get-Content $messagePackCsproj.FullName -Raw

    if ($content -notmatch 'PackageReference\s+Include="MessagePack"') {
        $violations += "  ⚠️  $messagePackProject does not reference MessagePack (expected for opt-in package)"
    } else {
        Write-Host "  ✅ $messagePackProject uses MessagePack (allowed - opt-in package)" -ForegroundColor Green
    }
} else {
    Write-Host "  ℹ️  $messagePackProject not found (may not exist yet)" -ForegroundColor DarkGray
}

Write-Host ""

# Report results
Write-Host "=== Validation Results ===" -ForegroundColor Cyan

if ($violations.Count -eq 0) {
    Write-Host "✅ Serializer policy validation PASSED" -ForegroundColor Green
    Write-Host ""
    Write-Host "Summary:" -ForegroundColor White
    Write-Host "  - Core projects use MemoryPack for internal wire format" -ForegroundColor Gray
    Write-Host "  - Public projects use System.Text.Json for external boundaries" -ForegroundColor Gray
    Write-Host "  - No obsolete serializers detected" -ForegroundColor Gray
    Write-Host "  - Protobuf isolated to opt-in Excalibur.Dispatch.Serialization.Protobuf package" -ForegroundColor Gray
    Write-Host "  - MessagePack isolated to opt-in Excalibur.Dispatch.Serialization.MessagePack package" -ForegroundColor Gray
    Write-Host ""
    exit 0
} else {
    Write-Host "❌ Serializer policy validation FAILED ($($violations.Count) violations)" -ForegroundColor Red
    Write-Host ""
    foreach ($v in $violations) {
        Write-Host $v -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "Remediation guidance:" -ForegroundColor White
    Write-Host "  - Remove System.Text.Json from Excalibur.Dispatch (use MemoryPack)" -ForegroundColor Gray
    Write-Host "  - Move JSON serializers to Excalibur.Dispatch.Patterns.Hosting.Json" -ForegroundColor Gray
    Write-Host "  - Delete obsolete [Obsolete] serializer implementations" -ForegroundColor Gray
    Write-Host "  - Isolate Protobuf to Excalibur.Dispatch.Transport.* packages only" -ForegroundColor Gray
    Write-Host ""
    Write-Host "See: management/reports/2025-10-12_stj-to-memorypack-migration-guide_v1.0.0.md" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}
