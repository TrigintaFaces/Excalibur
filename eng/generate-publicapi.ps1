#!/usr/bin/env pwsh
# Script to generate PublicAPI.Unshipped.txt from compiled assembly
param(
    [Parameter(Mandatory=$true)]
    [string]$AssemblyPath,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputPath
)

Write-Host "Loading assembly: $AssemblyPath"

# Set up dependency resolution from bin directory and runtime
$assemblyDir = Split-Path $AssemblyPath -Parent
Write-Host "Bin directory: $assemblyDir"

# Find actual .NET runtime location (not PowerShell's runtime)
$dotnetRoot = $null
$sharedFrameworkDir = $null

# Try to find .NET runtime via dotnet --list-runtimes
try {
    $runtimesList = dotnet --list-runtimes 2>$null | Where-Object { $_ -match 'Microsoft\.NETCore\.App' } | Select-Object -First 1
    if ($runtimesList -match '\[(.*?)\]') {
        $runtimePath = $matches[1]
        # Runtime path from dotnet --list-runtimes is like: C:\Program Files\dotnet\shared\Microsoft.NETCore.App
        # (without version subdirectory on some systems)
        $sharedFrameworkDir = $runtimePath
        $dotnetRoot = Split-Path (Split-Path $sharedFrameworkDir -Parent) -Parent
        Write-Host ".NET Root (from dotnet --list-runtimes): $dotnetRoot"
        Write-Host "Shared framework directory: $sharedFrameworkDir"
    }
}
catch {
    Write-Host "Warning: Could not detect .NET runtime via dotnet command: $_"
}

# Fallback to standard Windows installation path
if (-not $dotnetRoot) {
    $dotnetRoot = "C:\Program Files\dotnet"
    $sharedFrameworkDir = Join-Path $dotnetRoot "shared\Microsoft.NETCore.App"
    Write-Host ".NET Root (fallback): $dotnetRoot"
    if (Test-Path $sharedFrameworkDir) {
        Write-Host "Shared framework directory (fallback): $sharedFrameworkDir"
    }
    else {
        Write-Host "Warning: Shared framework directory not found at: $sharedFrameworkDir"
    }
}

$resolveEventHandler = [System.ResolveEventHandler] {
    param($sender, $eventArgs)
    $assemblyName = New-Object System.Reflection.AssemblyName($eventArgs.Name)
    
    Write-Host "DEBUG: Resolving assembly: $($assemblyName.Name)"

    # Try bin directory first
    $dllPath = Join-Path $assemblyDir "$($assemblyName.Name).dll"
    Write-Host "DEBUG: Checking bin path: $dllPath"
    if (Test-Path $dllPath) {
        Write-Host "  Resolving dependency from bin: $($assemblyName.Name)"
        return [System.Reflection.Assembly]::LoadFrom($dllPath)
    }

    # Try .NET shared framework directory (for Microsoft.Extensions.* and other framework assemblies)
    Write-Host "DEBUG: sharedFrameworkDir = $sharedFrameworkDir"
    if ($sharedFrameworkDir -and (Test-Path $sharedFrameworkDir)) {
        Write-Host "DEBUG: Shared framework directory exists"
        $versions = Get-ChildItem $sharedFrameworkDir
        Write-Host "DEBUG: Found $($versions.Count) version directories"
        $latestVersion = $versions | Sort-Object Name -Descending | Select-Object -First 1
        if ($latestVersion) {
            Write-Host "DEBUG: Latest version directory: $($latestVersion.FullName)"
            $dllPath = Join-Path $latestVersion.FullName "$($assemblyName.Name).dll"
            Write-Host "DEBUG: Checking framework path: $dllPath"
            if (Test-Path $dllPath) {
                Write-Host "  Resolving dependency from shared framework: $($assemblyName.Name)"
                return [System.Reflection.Assembly]::LoadFrom($dllPath)
            }
            else {
                Write-Host "DEBUG: DLL not found at framework path"
            }
        }
    }
    else {
        Write-Host "DEBUG: Shared framework directory check failed or path invalid"
    }

    # Try NuGet package cache (for Microsoft.Extensions.* and other NuGet packages)
    $nugetPackagesDir = Join-Path $env:USERPROFILE ".nuget\packages"
    Write-Host "DEBUG: NuGet packages directory: $nugetPackagesDir"
    if (Test-Path $nugetPackagesDir) {
        $packageDir = Join-Path $nugetPackagesDir $assemblyName.Name.ToLower()
        Write-Host "DEBUG: Checking NuGet package directory: $packageDir"
        if (Test-Path $packageDir) {
            $packageVersions = Get-ChildItem $packageDir -Directory | Sort-Object Name -Descending
            Write-Host "DEBUG: Found $($packageVersions.Count) package versions"
            foreach ($versionDir in $packageVersions) {
                Write-Host "DEBUG: Checking version: $($versionDir.Name)"
                # Try multiple target framework monikers
                foreach ($tfm in @("net9.0", "net8.0", "netstandard2.1", "netstandard2.0")) {
                    $dllPath = Join-Path $versionDir.FullName "lib\$tfm\$($assemblyName.Name).dll"
                    Write-Host "DEBUG: Checking NuGet path: $dllPath"
                    if (Test-Path $dllPath) {
                        Write-Host "  Resolving dependency from NuGet cache: $($assemblyName.Name) (version $($versionDir.Name), TFM $tfm)"
                        return [System.Reflection.Assembly]::LoadFrom($dllPath)
                    }
                }
            }
        }
        else {
            Write-Host "DEBUG: Package directory not found in NuGet cache"
        }
    }
    else {
        Write-Host "DEBUG: NuGet packages directory does not exist"
    }

    Write-Host "DEBUG: Failed to resolve $($assemblyName.Name)"
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($resolveEventHandler)

$assembly = [System.Reflection.Assembly]::LoadFrom($AssemblyPath)

$apiList = [System.Collections.Generic.List[string]]::new()

# Helper to format type name
function Get-TypeDisplayName {
    param([Type]$type)
    
    if ($type.IsGenericType) {
        $genericArgs = $type.GetGenericArguments() | ForEach-Object { Get-TypeDisplayName $_ }
        $baseName = $type.GetGenericTypeDefinition().FullName -replace '`\d+', ''
        return "$baseName<$($genericArgs -join ', ')>"
    }
    
    # Handle by-ref types
    if ($type.IsByRef) {
        $elementType = $type.GetElementType()
        return "ref $(Get-TypeDisplayName $elementType)"
    }
    
    # Handle arrays
    if ($type.IsArray) {
        $elementType = $type.GetElementType()
        return "$(Get-TypeDisplayName $elementType)[]"
    }
    
    # Handle nullable value types
    if ($type.IsGenericType -and $type.GetGenericTypeDefinition().FullName -eq 'System.Nullable`1') {
        $underlyingType = $type.GetGenericArguments()[0]
        return "$(Get-TypeDisplayName $underlyingType)?"
    }
    
    return $type.FullName
}

# Get public types with graceful error handling for assemblies with loading issues
$exportedTypes = @()
try {
    $exportedTypes = $assembly.GetExportedTypes()
}
catch [System.Reflection.ReflectionTypeLoadException] {
    Write-Host "Warning: Some types could not be loaded. Using partial type list."
    $exportedTypes = $_.Exception.Types | Where-Object { $_ -ne $null -and $_.IsPublic }
}
catch {
    Write-Host "Warning: GetExportedTypes failed: $($_.Exception.Message). Trying GetTypes()."
    try {
        $exportedTypes = $assembly.GetTypes() | Where-Object { $_.IsPublic }
    }
    catch [System.Reflection.ReflectionTypeLoadException] {
        $exportedTypes = $_.Exception.Types | Where-Object { $_ -ne $null -and $_.IsPublic }
    }
}

# Process each public type
foreach ($type in $exportedTypes | Sort-Object FullName) {
    Write-Host "Processing type: $($type.FullName)"
    
    # Add the type itself
    $apiList.Add($type.FullName)
    
    # Handle enums
    if ($type.IsEnum) {
        foreach ($field in $type.GetFields([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static) | Sort-Object Name) {
            $apiList.Add("$($type.FullName).$($field.Name) -> $($type.FullName)")
        }
        continue
    }
    
    # Add constructors
    foreach ($ctor in $type.GetConstructors([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) | Sort-Object { ($_.GetParameters() | Measure-Object).Count }) {
        $params = $ctor.GetParameters() | ForEach-Object {
            "$(Get-TypeDisplayName $_.ParameterType) $($_.Name)"
        }
        $paramString = $params -join ', '
        $apiList.Add("$($type.FullName).$($type.Name)($paramString) -> void")
    }
    
    # Add methods
    foreach ($method in $type.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::DeclaredOnly) | Sort-Object Name) {
        # Skip property/event accessor methods
        if ($method.IsSpecialName) { continue }
        
        $params = $method.GetParameters() | ForEach-Object {
            "$(Get-TypeDisplayName $_.ParameterType) $($_.Name)"
        }
        $paramString = $params -join ', '
        $returnType = Get-TypeDisplayName $method.ReturnType
        
        $staticModifier = if ($method.IsStatic) { "static " } else { "" }
        $apiList.Add("$staticModifier$($type.FullName).$($method.Name)($paramString) -> $returnType")
    }
    
    # Add properties
    foreach ($prop in $type.GetProperties([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::DeclaredOnly) | Sort-Object Name) {
        $propType = Get-TypeDisplayName $prop.PropertyType
        $staticModifier = if ($prop.GetMethod.IsStatic -or $prop.SetMethod.IsStatic) { "static " } else { "" }
        
        $accessors = @()
        if ($prop.GetMethod -and $prop.GetMethod.IsPublic) { $accessors += "get;" }
        if ($prop.SetMethod -and $prop.SetMethod.IsPublic) { $accessors += "set;" }
        $accessorString = $accessors -join " "
        
        $apiList.Add("$staticModifier$($type.FullName).$($prop.Name).get -> $propType")
        if ($prop.SetMethod -and $prop.SetMethod.IsPublic) {
            $apiList.Add("$staticModifier$($type.FullName).$($prop.Name).set -> void")
        }
    }
    
    # Add fields
    foreach ($field in $type.GetFields([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::DeclaredOnly) | Sort-Object Name) {
        $fieldType = Get-TypeDisplayName $field.FieldType
        $staticModifier = if ($field.IsStatic) { "static " } else { "" }
        $readonlyModifier = if ($field.IsInitOnly) { "readonly " } else { "" }
        $constModifier = if ($field.IsLiteral) { "const " } else { "" }
        
        $apiList.Add("$staticModifier$readonlyModifier$constModifier$($type.FullName).$($field.Name) -> $fieldType")
    }
    
    # Add events
    foreach ($event in $type.GetEvents([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::DeclaredOnly) | Sort-Object Name) {
        $eventType = Get-TypeDisplayName $event.EventHandlerType
        $staticModifier = if ($event.AddMethod.IsStatic) { "static " } else { "" }
        
        $apiList.Add("$staticModifier$($type.FullName).$($event.Name).add -> void")
        $apiList.Add("$staticModifier$($type.FullName).$($event.Name).remove -> void")
    }
}

# Sort and write to file
$sortedApi = $apiList | Sort-Object -Unique
Write-Host "Generated $($sortedApi.Count) API entries"
$sortedApi | Out-File -FilePath $OutputPath -Encoding utf8 -Force

Write-Host "PublicAPI file written to: $OutputPath"
