# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
# Exercises the real audit in isolated repositories. Never regenerates a baseline.
param([string]$CandidateScript = (Join-Path $PSScriptRoot 'Invoke-AotSuppressionAudit.ps1'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'eng/package-composition.functions.ps1')
$candidate = (Resolve-Path -LiteralPath $CandidateScript).Path
$candidateHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$realBaseline = Join-Path $workspace 'eng/ci/aot-suppression-baseline.json'
$realBefore = (Get-FileHash -LiteralPath $realBaseline -Algorithm SHA256).Hash
$attempt = Join-Path $workspace ('artifacts/aot-audit-parser/attempt-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $attempt -Force | Out-Null
$results = [Collections.Generic.List[object]]::new()
$pwsh = (Get-Command pwsh -ErrorAction Stop).Source
$cases = [Collections.Generic.List[object]]::new()
function Add-Case([string]$Name, [string]$Source, [int]$Exit, [string[]]$Reasons = @()) {
    $cases.Add(@{ Name=$Name; Source=$Source; Exit=$Exit; Reasons=$Reasons })
}
function Attribute-Source([string]$Expression) {
    return 'using System.Diagnostics.CodeAnalysis; [UnconditionalSuppressMessage("Trimming", "IL2026"' + $Expression + ')] public class Fixture {}'
}
Add-Case 'missing' (Attribute-Source '') 3
Add-Case 'reordered-named-constructor' 'using System.Diagnostics.CodeAnalysis; [UnconditionalSuppressMessage(checkId: "IL2026", category: "Trimming", Justification="named reason")] public class Fixture {}' 1 @('named reason')
Add-Case 'named-constructor' 'using System.Diagnostics.CodeAnalysis; [UnconditionalSuppressMessage(category: "Trimming", checkId: "IL2026", Justification="named reason")] public class Fixture {}' 1 @('named reason')
Add-Case 'empty' (Attribute-Source ', Justification = ""') 3
Add-Case 'spaces' (Attribute-Source ', Justification = "   "') 3
Add-Case 'escaped-tab' (Attribute-Source ', Justification = "\t"') 3
Add-Case 'unicode-space' (Attribute-Source ', Justification = "\u0020"') 3
Add-Case 'hex-space' (Attribute-Source ', Justification = "\x20"') 3
Add-Case 'visible-backslash-t' (Attribute-Source ', Justification = "\\t"') 1 @('\\t')
Add-Case 'quotes-and-delimiters' (Attribute-Source ', Justification = "reviewed \"quoted\" )] reason"') 1 @('reviewed \"quoted\" )] reason')
Add-Case 'malformed-with-valid' ((Attribute-Source ', Justification = "valid"') + "`n" + '[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = )] public class Broken {}') 3
Add-Case 'multiple-same-line' 'using System.Diagnostics.CodeAnalysis; [UnconditionalSuppressMessage("Trimming", "IL2026", Justification="first"), UnconditionalSuppressMessage("AOT", "IL3050", Justification="second")] public class Fixture {}' 1 @('first','second')
Add-Case 'attribute-alias' 'using Suppress = System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute; [Suppress("Trimming", "IL2026", Justification="alias reason")] public class Fixture {}' 1 @('alias reason')
Add-Case 'local-const-concatenation' 'using System.Diagnostics.CodeAnalysis; public class Fixture { const string Reason = "reviewed " + "path"; [UnconditionalSuppressMessage("Trimming", "IL2026", Justification=Reason)] public void Run() {} }' 1 @('Reason')
Add-Case 'empty-const' 'using System.Diagnostics.CodeAnalysis; public class Fixture { const string Reason = ""; [UnconditionalSuppressMessage("Trimming", "IL2026", Justification=Reason)] public void Run() {} }' 3
Add-Case 'cyclic-const' 'using System.Diagnostics.CodeAnalysis; public class Fixture { const string A = B; const string B = A; [UnconditionalSuppressMessage("Trimming", "IL2026", Justification=A)] public void Run() {} }' 3
Add-Case 'unresolved-const' (Attribute-Source ', Justification = UnknownReason') 3
Add-Case 'external-qualified-const' (Attribute-Source ', Justification = External.Reason') 3
Add-Case 'duplicate-const-scopes' 'using System.Diagnostics.CodeAnalysis; class First { const string Reason="approved"; [UnconditionalSuppressMessage("Trimming", "IL2026", Justification=Reason)] void Run() {} } class Second { const string Reason=""; [UnconditionalSuppressMessage("Trimming", "IL2026", Justification=Reason)] void Run() {} }' 3
Add-Case 'shadowed-const' 'using System.Diagnostics.CodeAnalysis; class Outer { const string Reason="approved"; class Inner { const string Reason=""; [UnconditionalSuppressMessage("Trimming", "IL2026", Justification=Reason)] void Run() {} } }' 3
Add-Case 'concat-visible-then-empty' (Attribute-Source ', Justification = "full reason" + ""') 1 @('full reason')
Add-Case 'concat-empty-then-visible' (Attribute-Source ', Justification = "" + "full reason"') 1 @('full reason')
Add-Case 'concat-two-visible' (Attribute-Source ', Justification = "full " + "reason"') 1 @('full reason')
Add-Case 'comment-not-justification' (Attribute-Source ', /* Justification = "misleading" */') 3
Add-Case 'blanket-pragma' "#pragma warning disable // reviewed reason`npublic class Fixture {}" 3
Add-Case 'ca-comment-not-il' "#pragma warning disable CA1034 // IL2026 explanation`npublic class Fixture {}" 0
Add-Case 'empty-il-pragma' "#pragma warning disable IL2026`npublic class Fixture {}" 3
Add-Case 'justified-il-pragma' "#pragma warning disable IL2026 // reviewed reason`npublic class Fixture {}" 1 @('reviewed reason')
Add-Case 'disabled-candidate' ("#if NEVER_DEFINED`n" + (Attribute-Source ', Justification = "conditional"') + "`n#endif`npublic class Active {}") 3
Add-Case 'generator-strings-excluded' @'
using System.Diagnostics.CodeAnalysis;
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification="applied reason")]
public class Fixture {
    const string Generator = "[UnconditionalSuppressMessage(\"Trimming\", \"IL2026\", Justification=\"fake\")]";
    const string Analyzer = "#pragma warning disable IL3050 // fake";
}
'@ 1 @('applied reason')
try {
    foreach ($case in $cases) {
        $caseRoot = Join-Path $attempt $case.Name
        $repo = Join-Path $caseRoot 'repo'
        $ci = Join-Path $repo 'eng/ci'
        $src = Join-Path $repo 'src'
        $output = Join-Path $caseRoot 'output'
        New-Item -ItemType Directory -Path $ci,$src -Force | Out-Null
        $copy = Join-Path $ci 'Invoke-AotSuppressionAudit.ps1'
        Copy-Item -LiteralPath $candidate -Destination $copy
        if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $candidateHash) { throw 'Candidate copy changed.' }
        $case.Source | Set-Content -LiteralPath (Join-Path $src 'Fixture.cs') -Encoding utf8
        $baseline = Join-Path $ci 'aot-suppression-baseline.json'
        '{"description":"preserve-parser-sentinel","suppressions":[]}' | Set-Content -LiteralPath $baseline -Encoding utf8
        $before = (Get-FileHash -LiteralPath $baseline).Hash
        $log = Join-Path $caseRoot 'native'
        $commandError = $null
        try { Invoke-PackageCommand -Executable $pwsh -Arguments @('-NoProfile','-File',$copy,'-BaselinePath',$baseline,'-SrcPath',$src,'-OutputPath',$output) -Log $log -WorkingDirectory $repo -TimeoutSeconds 60 }
        catch { $commandError = $_.Exception.Message }
        $receipt = Get-Content -LiteralPath "$log.receipt.json" -Raw | ConvertFrom-Json
        $after = (Get-FileHash -LiteralPath $baseline).Hash
        $parsedReasons = @()
        $parsedCount = $null
        $auditPath = Join-Path $output 'aot-suppression-audit.json'
        if (Test-Path -LiteralPath $auditPath) {
            $audit = Get-Content -LiteralPath $auditPath -Raw | ConvertFrom-Json
            if ($audit.PSObject.Properties.Name -contains 'SourceSuppressions') { $parsedCount = $audit.SourceSuppressions }
            if ($audit.PSObject.Properties.Name -contains 'NewDetails') { $parsedReasons = @($audit.NewDetails | ForEach-Object Justification | Sort-Object) }
        }
        $passed = -not $receipt.timedOut -and $receipt.exitCode -eq $case.Exit -and $before -eq $after
        if ($case.Exit -in @(0,1)) {
            $expectedReasons = @($case.Reasons | Sort-Object)
            $passed = $passed -and $parsedCount -eq $expectedReasons.Count -and ($parsedReasons -join "`n") -ceq ($expectedReasons -join "`n")
        }
        $results.Add([ordered]@{ name=$case.Name; status=$(if($passed){'passed'}else{'failed'}); expectedExit=$case.Exit; actualExit=$receipt.exitCode; parsedCount=$parsedCount; expectedReasons=$case.Reasons; parsedReasons=$parsedReasons; baselineBefore=$before; baselineAfter=$after; receipt="$log.receipt.json"; commandError=$commandError })
    }
}
finally {
    $realAfter = (Get-FileHash -LiteralPath $realBaseline -Algorithm SHA256).Hash
    [ordered]@{ candidate=$candidate; candidateSha256=$candidateHash; attempt=$attempt; realBaselineBefore=$realBefore; realBaselineAfter=$realAfter; cases=$results.ToArray(); expectedCount=$cases.Count; completedCount=$results.Count } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $attempt 'report.json') -Encoding utf8
}
if ($realBefore -ne $realAfter) { throw "Real baseline changed; evidence: $attempt" }
if ($results.Count -ne $cases.Count -or @($results | Where-Object status -eq 'failed').Count -gt 0) { throw "Parser controls failed or incomplete; evidence: $attempt" }
Write-Output "Parser controls passed ($($results.Count)); retained evidence: $attempt"
