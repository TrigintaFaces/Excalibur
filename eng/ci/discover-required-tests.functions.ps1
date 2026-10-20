# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

function Test-ExpectedZeroMatchDiagnostic {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Diagnostic,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Filter,
        [Parameter(Mandatory)][string]$AssemblyPath,
        [Parameter(Mandatory)][long]$DiscoveredCount,
        [Parameter(Mandatory)][long]$ReceivedCount
    )
    if (-not $Filter -or $DiscoveredCount -ne 0 -or $ReceivedCount -ne 0) { return $false }
    # Pinned SDK 10.0.401's VSTest abbreviates only the diagnostic, not the execution filter:
    # microsoft/vstest TestCaseFilterDeterminer.cs, max 256 UTF-16 characters plus three dots.
    # https://github.com/microsoft/vstest/blob/a29631cbd11a5d8381e7fd9feacf1b92715293aa/src/Microsoft.TestPlatform.CrossPlatEngine/Utilities/TestCaseFilterDeterminer.cs#L8-L14
    $displayFilter = if ($Filter.Length -gt 256) { $Filter.Substring(0,256) + '...' } else { $Filter }
    $expected = 'Warning: No test matches the given testcase filter `' + $displayFilter + '` in ' + $AssemblyPath
    return $Diagnostic.Trim() -ceq $expected
}
