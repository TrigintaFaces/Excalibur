<#
.SYNOPSIS
    Audits [UnconditionalSuppressMessage] attributes in src/ against a known baseline.

.DESCRIPTION
    Scans all .cs files under src/ for [UnconditionalSuppressMessage] attributes,
    compares them against the approved baseline (eng/ci/aot-suppression-baseline.json),
    and reports:
      - NEW suppressions not in the baseline (blocks CI)
      - STALE baseline entries no longer in source (warning)

    Baseline-first approach: existing suppressions are grandfathered. Only new
    unapproved suppressions block the build.

    POPULATION -- what this audit does and does NOT cover.

    IN SCOPE: a suppression that takes effect in THIS repository's own compilation --
    an [UnconditionalSuppressMessage] attribute applied to a member, and a
    `#pragma warning disable IL2xxx/IL3xxx` region.

    OUT OF SCOPE, DELIBERATELY: the suppressions our source generators EMIT into a
    consumer's compilation. A generator holds the attribute as a C# string literal
    (`sb.AppendLine("[UnconditionalSuppressMessage(...)]")`); that literal suppresses
    nothing here, and the suppression it produces exists only after the generator has
    run against consumer code. Counting the template line is not an audit of the
    emitted suppression -- it is a different artifact in a different compilation, and
    treating one as the other reports a population this repository does not have.
    Auditing the emitted set requires compiling a representative consumer and reading
    the generated sources, which is a build-time job and is NOT performed by any gate
    in this repository today. Until one exists, generator-emitted suppressions are
    UNAUDITED, and this header is the record of that gap rather than a silent
    omission. Template lines are excluded from the population below, not counted.

    THREE-STATE RESULT. A suppression this script can SEE but cannot PARSE is
    reported as REFUSE and is never silently fingerprinted on an empty justification.
    REFUSE is not PASS: the fingerprint used for the NEW/STALE comparison is
    (file, warningId, justification), so a justification that fails to parse yields a
    row that can never match its baseline entry and reports NEW *and* STALE on every
    run, forever, while burning the CI summary's display cap. Refusing is what makes
    that failure visible instead of permanent.

    Exit codes:
      0 = PASS   -- evaluated, no new suppressions found
      1 = FAIL   -- evaluated, new unapproved suppressions detected
      2 = ERROR  -- script error
      3 = REFUSE -- one or more suppressions could not be parsed; the population is
                   not trustworthy, so NOTHING is asserted about it. Not a pass.

.PARAMETER BaselinePath
    Path to the suppression baseline JSON file.

.PARAMETER SrcPath
    Root source directory to scan (default: src/).

.PARAMETER OutputPath
    Directory for audit results.

.PARAMETER GenerateBaseline
    When set, generates a new baseline from the current source instead of auditing.

    DESTRUCTIVE. It OVERWRITES -BaselinePath and writes only file/line/warningId/
    justification. The committed baseline additionally carries hand-authored fields
    (verdict, classifiedBy, note) that this switch does not emit, so regenerating
    DELETES them and nothing can restore them from the scan. Prefer editing the file
    for a small change; diff the result before committing a large one.

    Refuses generation from any source population other than the repository's full
    src/ tree into a repository baseline, including explicitly supplied paths.
    Use an explicit scratch baseline for fixture generation. Reparse-point paths
    require their actual filesystem paths for generation; read-only audits are unaffected.

.EXAMPLE
    ./Invoke-AotSuppressionAudit.ps1
    ./Invoke-AotSuppressionAudit.ps1 -GenerateBaseline
    ./Invoke-AotSuppressionAudit.ps1 -SrcPath ./fixtures/src -BaselinePath ./scratch/baseline.json -OutputPath ./scratch/audit -GenerateBaseline
    ./Invoke-AotSuppressionAudit.PathGuard.Tests.ps1
#>
[CmdletBinding()]
param(
    [string]$BaselinePath = '',
    [string]$SrcPath = '',
    [string]$OutputPath = 'aot-suppression-audit',
    [switch]$GenerateBaseline
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if (-not $SrcPath) {
    $SrcPath = Join-Path $repoRoot 'src'
}
if (-not $BaselinePath) {
    $BaselinePath = Join-Path $repoRoot 'eng' 'ci' 'aot-suppression-baseline.json'
}

# ---- REFUSE a fixture scan that would write a REPOSITORY baseline ------------------------------
# -BaselinePath defaults to the repo file. -SrcPath does not have to be overridden for that to be
# fine, but the COMBINATION "scan somewhere else, write the real baseline" has no legitimate use:
# a scan of a fixture tree cannot produce a baseline for this repository, and writing one destroys
# whatever was there. That is not hypothetical -- a fixture run proving this script's own REFUSE arm
# non-vacuous overwrote the real baseline with three rows, and the file it destroyed carried
# hand-authored fields (verdict / classifiedBy / note) that -GenerateBaseline does not emit and a
# regeneration therefore cannot restore.
#
# The relation between the actual paths matters, not which arguments were explicitly supplied.
# Check before creating output or writing anything. Missing target files are checked through their
# nearest existing ancestor. A link could redirect an apparently external target into the repository;
# generation refuses such uncertain paths rather than guessing. Ordinary read-only audits keep their
# existing path behavior.
if ($GenerateBaseline) {
    function Get-GenerationPath {
        param([string]$Path)
        $full = [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))
        $pathComparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
        $ancestor = $full
        while ($true) {
            $entry = $null
            try { $entry = Get-Item -LiteralPath $ancestor -Force -ErrorAction Stop }
            catch [System.Management.Automation.ItemNotFoundException] {
                # A dangling link can be absent to Test-Path yet remain a filesystem entry.
                $parent = [IO.Path]::GetDirectoryName($ancestor)
                if ($parent -and (Test-Path -LiteralPath $parent -PathType Container)) {
                    $leaf = [IO.Path]::GetFileName($ancestor)
                    $entry = Get-ChildItem -LiteralPath $parent -Force -ErrorAction Stop |
                        Where-Object { $_.Name.Equals($leaf, $pathComparison) } | Select-Object -First 1
                }
            }
            if ($null -ne $entry) { break }
            $parent = [IO.Path]::GetDirectoryName($ancestor)
            if (-not $parent -or $parent -eq $ancestor) { throw "Cannot resolve generation path: $Path" }
            $ancestor = $parent
        }
        while ($ancestor) {
            $entry = Get-Item -LiteralPath $ancestor -Force
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Generation path contains a link at $ancestor. Supply its actual filesystem path."
            }
            $parent = [IO.Path]::GetDirectoryName($ancestor)
            if (-not $parent -or $parent -eq $ancestor) { break }
            $ancestor = $parent
        }
        return $full.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    }
    try {
        $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
        $generationRepo = Get-GenerationPath $repoRoot
        $generationSource = Get-GenerationPath $SrcPath
        $repositorySource = Get-GenerationPath (Join-Path $repoRoot 'src')
        $generationBaseline = Get-GenerationPath $BaselinePath
        $repositoryPrefix = $generationRepo + [IO.Path]::DirectorySeparatorChar
        $insideRepository = $generationBaseline.Equals($generationRepo, $comparison) -or
            $generationBaseline.StartsWith($repositoryPrefix, $comparison)
        if ($insideRepository -and -not $generationSource.Equals($repositorySource, $comparison)) {
            throw "Source $SrcPath is not the full repository src tree; it cannot generate repository baseline $BaselinePath. Supply an explicit scratch -BaselinePath."
        }
    }
    catch {
        Write-Host 'REFUSING to generate a baseline.' -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Yellow
        exit 2
    }
}

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# ============================================================================
# Scan source for [UnconditionalSuppressMessage] attributes
# ============================================================================

$parserAssemblies = @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll') | ForEach-Object { Join-Path $PSHOME $_ }
foreach ($path in $parserAssemblies) { [void][Reflection.Assembly]::LoadFrom($path) }
$references = @($parserAssemblies) + @('System.Runtime.dll','System.Collections.dll','System.Collections.Immutable.dll','System.Linq.dll','System.Text.RegularExpressions.dll') | ForEach-Object {
    if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path "$PSHOME/ref" $_ }
}
Add-Type -ReferencedAssemblies $references -CompilerOptions '/nowarn:1701' -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Excalibur.CI {
public sealed class AuditRow { public int Line; public string WarningId; public string Justification; }
public sealed class AuditScan {
    public List<AuditRow> Rows = new List<AuditRow>();
    public List<string> Refusals = new List<string>();
}
public static class AuditSyntax {
    static int Line(SyntaxNode n) => n.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    static string Terminal(NameSyntax n) {
        if (n is QualifiedNameSyntax q) return Terminal(q.Right);
        if (n is AliasQualifiedNameSyntax a) return Terminal(a.Name);
        return n is SimpleNameSyntax s ? s.Identifier.ValueText : "";
    }
    static bool Candidate(string n) => n == "UnconditionalSuppressMessage" || n == "UnconditionalSuppressMessageAttribute";
    static string Field(TypeDeclarationSyntax scope, string name, HashSet<SyntaxNode> seen, int depth) {
        var fields = scope.Members.OfType<FieldDeclarationSyntax>().SelectMany(f => f.Declaration.Variables).Where(v => v.Identifier.ValueText == name).ToArray();
        if (fields.Length != 1 || !(fields[0].Parent.Parent is FieldDeclarationSyntax field)
            || !field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)) || fields[0].Initializer == null)
            throw new InvalidOperationException("missing, ambiguous or non-constant field");
        if (!seen.Add(fields[0])) throw new InvalidOperationException("cyclic constant");
        try { return Resolve(fields[0].Initializer.Value, seen, depth + 1); }
        finally { seen.Remove(fields[0]); }
    }
    static string Resolve(ExpressionSyntax e, HashSet<SyntaxNode> seen, int depth) {
        if (depth > 32) throw new InvalidOperationException("constant recursion bound exceeded");
        string value;
        if (e is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)) value = literal.Token.ValueText;
        else if (e is ParenthesizedExpressionSyntax paren) value = Resolve(paren.Expression, seen, depth + 1);
        else if (e is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression)) value = Resolve(binary.Left, seen, depth + 1) + Resolve(binary.Right, seen, depth + 1);
        else if (e is IdentifierNameSyntax id) {
            foreach (var scope in e.Ancestors().OfType<TypeDeclarationSyntax>()) {
                if (scope.Members.OfType<FieldDeclarationSyntax>().SelectMany(f => f.Declaration.Variables).Any(v => v.Identifier.ValueText == id.Identifier.ValueText))
                    return Field(scope, id.Identifier.ValueText, seen, depth);
                if (scope.Members.OfType<PropertyDeclarationSyntax>().Any(p => p.Identifier.ValueText == id.Identifier.ValueText)) throw new InvalidOperationException("shadowed constant");
                if (scope.BaseList != null) throw new InvalidOperationException("inherited constant requires semantic resolution");
            }
            throw new InvalidOperationException("unresolved constant");
        } else if (e is MemberAccessExpressionSyntax member && member.Expression is IdentifierNameSyntax owner) {
            var scopes = e.Ancestors().OfType<TypeDeclarationSyntax>().Where(t => t.Identifier.ValueText == owner.Identifier.ValueText).ToArray();
            if (scopes.Length != 1) throw new InvalidOperationException("external or ambiguous qualified constant");
            return Field(scopes[0], member.Name.Identifier.ValueText, seen, depth);
        } else throw new InvalidOperationException("unsupported constant expression");
        if (value.Length > 65536) throw new InvalidOperationException("constant output bound exceeded");
        return value;
    }
    public static AuditScan Parse(string content, string path) {
        var result = new AuditScan();
        var tree = CSharpSyntaxTree.ParseText(content, new CSharpParseOptions(LanguageVersion.Preview), path);
        var root = tree.GetRoot();
        foreach (var error in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
            result.Refusals.Add((error.Location.GetLineSpan().StartLinePosition.Line + 1) + ": " + error.Id + " syntax error");
        var aliases = root.DescendantNodes().OfType<UsingDirectiveSyntax>().Where(u => u.Alias != null && u.Name != null && Candidate(Terminal(u.Name))).ToArray();
        foreach (var alias in aliases.Where(u => !u.GlobalKeyword.IsKind(SyntaxKind.None))) result.Refusals.Add(Line(alias) + ": global suppression alias requires compilation-wide resolution");
        foreach (var disabled in root.DescendantTrivia(descendIntoTrivia: true).Where(t => t.IsKind(SyntaxKind.DisabledTextTrivia))) {
            var text = disabled.ToString();
            if (text.Contains("UnconditionalSuppressMessage") || text.Contains("#pragma") || aliases.Any(a => text.Contains(a.Alias.Name.Identifier.ValueText)))
                result.Refusals.Add((disabled.GetLocation().GetLineSpan().StartLinePosition.Line + 1) + ": conditional candidate requires configuration review");
        }
        foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>()) {
            var name = Terminal(attribute.Name);
            if (!Candidate(name)) {
                var matching = aliases.Where(u => u.Alias.Name.Identifier.ValueText == name && (u.Parent is CompilationUnitSyntax || attribute.Ancestors().Contains(u.Parent))).ToArray();
                if (matching.Length == 0) continue;
                if (matching.Length != 1) { result.Refusals.Add(Line(attribute) + ": ambiguous alias"); continue; }
            }
            try {
                var args = attribute.ArgumentList == null ? new AttributeArgumentSyntax[0] : attribute.ArgumentList.Arguments.ToArray();
                var constructor = args.Where(a => a.NameEquals == null).ToArray();
                var parameters = new Dictionary<string, AttributeArgumentSyntax>(StringComparer.Ordinal);
                var parameterNames = new[] { "category", "checkId" };
                for (var index = 0; index < constructor.Length; index++) {
                    var argument = constructor[index];
                    if (index >= parameterNames.Length) throw new InvalidOperationException("too many constructor arguments");
                    var parameter = argument.NameColon == null ? parameterNames[index] : argument.NameColon.Name.Identifier.ValueText;
                    if (!parameterNames.Contains(parameter) || parameters.ContainsKey(parameter)) throw new InvalidOperationException("unknown or duplicate constructor argument");
                    parameters.Add(parameter, argument);
                }
                parameters.TryGetValue("checkId", out var diagnostic);
                if (diagnostic == null) throw new InvalidOperationException("missing diagnostic id");
                var warning = Resolve(diagnostic.Expression, new HashSet<SyntaxNode>(), 0);
                if (!System.Text.RegularExpressions.Regex.IsMatch(warning, @"^IL[23]\d{3}(?:$|:)")) throw new InvalidOperationException("no IL diagnostic id");
                var reasons = args.Where(a => a.NameEquals != null && a.NameEquals.Name.Identifier.ValueText == "Justification").ToArray();
                if (reasons.Length != 1) throw new InvalidOperationException("missing or duplicate Justification");
                var expression = reasons[0].Expression;
                var decoded = Resolve(expression, new HashSet<SyntaxNode>(), 0);
                if (String.IsNullOrWhiteSpace(decoded)) throw new InvalidOperationException("empty Justification");
                var fingerprint = decoded;
                if (expression is LiteralExpressionSyntax literal && literal.Token.Text.StartsWith("\"") && !literal.Token.Text.StartsWith("\"\"\"")) fingerprint = literal.Token.Text.Substring(1, literal.Token.Text.Length - 2);
                else if (expression is IdentifierNameSyntax || expression is MemberAccessExpressionSyntax) fingerprint = expression.ToString();
                result.Rows.Add(new AuditRow { Line=Line(attribute), WarningId=warning.Substring(0,6), Justification=fingerprint });
            } catch (InvalidOperationException ex) { result.Refusals.Add(Line(attribute) + ": " + ex.Message); }
        }
        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true)) {
            var pragma = trivia.GetStructure() as PragmaWarningDirectiveTriviaSyntax;
            if (pragma == null || !pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)) continue;
            if (pragma.ErrorCodes.Count == 0) { result.Refusals.Add(Line(pragma) + ": blanket warning disable includes IL diagnostics"); continue; }
            foreach (var code in pragma.ErrorCodes) {
                var warning = code.ToString();
                if (!System.Text.RegularExpressions.Regex.IsMatch(warning, @"^IL[23]\d{3}$")) continue;
                var comments = pragma.DescendantTrivia(descendIntoTrivia: true).Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)).ToArray();
                var reason = comments.Length == 1 ? comments[0].ToString().Substring(2).Trim() : "";
                if (String.IsNullOrWhiteSpace(reason)) result.Refusals.Add(Line(pragma) + ": missing IL pragma justification");
                else result.Rows.Add(new AuditRow { Line=Line(pragma), WarningId=warning, Justification=reason });
            }
        }
        return result;
    }
}
}
'@
$script:ParserEvidence = @($parserAssemblies | ForEach-Object { @{ Path=$_; Version=[Reflection.AssemblyName]::GetAssemblyName($_).Version.ToString(); Sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } })
$script:ParsedSources = @()
function Get-AuditSourceFiles {
    param([string]$SourceRoot)
    return @(Get-ChildItem -LiteralPath $SourceRoot -Filter '*.cs' -Recurse -File |
        Where-Object { $_.FullName -notmatch '(\\|/)obj(\\|/)|(\\|/)bin(\\|/)' } | Sort-Object FullName)
}
function Get-AuditBytesHash {
    param([byte[]]$Bytes)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes))
}
function Test-AuditSourceManifest {
    param([string]$SourceRoot)
    $current = @(Get-AuditSourceFiles $SourceRoot)
    if ((@($current | ForEach-Object FullName) -join "`n") -cne ($script:SourceManifest -join "`n")) {
        $script:ParseFailures += @{ File=$SourceRoot; Line=1; Reason='source file manifest changed during audit' }
    }
    foreach ($source in $script:ParsedSources) {
        try { $hash = Get-AuditBytesHash ([IO.File]::ReadAllBytes($source.Path)) }
        catch { $hash = '' }
        if ($hash -cne $source.Sha256) { $script:ParseFailures += @{ File=$source.File; Line=1; Reason='source bytes changed after parsing' } }
    }
}
function Assert-AuditSourceManifest {
    Test-AuditSourceManifest -SourceRoot $SrcPath
    if ($script:ParseFailures.Count -gt 0) {
        @{ Outcome='REFUSE'; ParserAssemblies=$script:ParserEvidence; SourceManifest=$script:SourceManifest; ParsedSources=$script:ParsedSources; CandidateAssumption=$script:CandidateAssumption; Refusals=$script:ParseFailures } |
            ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputPath 'aot-suppression-audit.json') -Encoding utf8
        Write-Host 'REFUSE: source candidate changed during audit; see provenance report.' -ForegroundColor Red
        exit 3
    }
}
# The audit evaluates an immutable candidate. Revalidation detects observed drift;
# it cannot lock the tree or detect an ABA edit restored between observations.
$script:CandidateAssumption = 'Source tree must remain immutable through audit publication; manifest/hash revalidation does not detect ABA edits restored between observations.'
function Get-SourceSuppressions {
    param([string]$SourceRoot)
    $script:ParseFailures = @()
    $rows = @()
    $files = @(Get-AuditSourceFiles $SourceRoot)
    $script:SourceManifest = @($files | ForEach-Object FullName)
    foreach ($file in $files) {
        $relative = $file.FullName.Replace($repoRoot, '').TrimStart('\','/') -replace '\\','/'
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $hash = Get-AuditBytesHash $bytes
        $stream = [IO.MemoryStream]::new($bytes, $false)
        $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true)
        try { $content = $reader.ReadToEnd() }
        finally { $reader.Dispose(); $stream.Dispose() }
        $scan = [Excalibur.CI.AuditSyntax]::Parse($content, $file.FullName)
        $script:ParsedSources += @{ File=$relative; Path=$file.FullName; Sha256=$hash }
        foreach ($reason in $scan.Refusals) {
            $parts = $reason -split ': ', 2
            $script:ParseFailures += @{ File=$relative; Line=[int]$parts[0]; Reason=$parts[1] }
        }
        foreach ($row in $scan.Rows) { $rows += @{ File=$relative; Line=$row.Line; WarningId=$row.WarningId; Justification=$row.Justification } }
    }
    return $rows
}
Write-Host "Scanning source for suppressions -- [UnconditionalSuppressMessage] attributes AND #pragma warning disable of IL2xxx/IL3xxx..." -ForegroundColor Cyan
$sourceSuppressions = @(Get-SourceSuppressions -SourceRoot $SrcPath)
Test-AuditSourceManifest -SourceRoot $SrcPath

# REFUSE gate -- runs BEFORE the baseline is loaded, because an unparsed row makes the
# population itself untrustworthy and there is nothing honest to compare against. A green
# printed over a population we could not read is the failure this gate exists to prevent.
if ($script:ParseFailures.Count -gt 0) {
    Write-Host ""
    Write-Host "REFUSE: $($script:ParseFailures.Count) suppression(s) were found but could not be parsed." -ForegroundColor Red
    foreach ($f in $script:ParseFailures) {
        Write-Host "  $($f.File):$($f.Line) -- $($f.Reason)" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "  Nothing was asserted about the suppression population. This is NOT a pass, and it is" -ForegroundColor Yellow
    Write-Host "  NOT 'no new suppressions'. Either write the suppression in a form this script reads" -ForegroundColor Yellow
    Write-Host "  (a quoted justification, or a constant reference), or teach the extractor the new form." -ForegroundColor Yellow
    Write-Host "  Missing or decoded-empty justifications also REFUSE; deleting a reason cannot clear this gate." -ForegroundColor Yellow
    @{ Outcome='REFUSE'; ParserAssemblies=$script:ParserEvidence; SourceManifest=$script:SourceManifest; ParsedSources=$script:ParsedSources; CandidateAssumption=$script:CandidateAssumption; Refusals=$script:ParseFailures } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputPath 'aot-suppression-audit.json') -Encoding utf8
    exit 3
}
Write-Host "  Found $($sourceSuppressions.Count) suppression(s) in source" -ForegroundColor Green

# ============================================================================
# Generate baseline mode
# ============================================================================

if ($GenerateBaseline) {
    Write-Host "Generating new baseline..." -ForegroundColor Yellow

    $baselineEntries = @($sourceSuppressions | ForEach-Object {
        @{
            file          = $_.File
            line          = $_.Line
            warningId     = $_.WarningId
            justification = $_.Justification
        }
    } | Sort-Object { $_.file }, { $_.line })

    $baseline = @{
        '$schema'     = 'https://json-schema.org/draft/2020-12/schema'
        description   = 'AOT suppression baseline for Excalibur.Dispatch. Existing suppressions are grandfathered. New suppressions require justification and approval.'
        generatedAt   = (Get-Date -Format 'yyyy-MM-dd')
        suppressions  = $baselineEntries
    }

    Assert-AuditSourceManifest
    $baseline | ConvertTo-Json -Depth 5 | Out-File -FilePath $BaselinePath -Encoding utf8
    @{ Outcome='GENERATED'; SourceSuppressions=$sourceSuppressions.Count; ParserAssemblies=$script:ParserEvidence; SourceManifest=$script:SourceManifest; ParsedSources=$script:ParsedSources; CandidateAssumption=$script:CandidateAssumption } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputPath 'aot-suppression-audit.json') -Encoding utf8
    Write-Host "Baseline written to $BaselinePath ($($baselineEntries.Count) entries)" -ForegroundColor Green
    exit 0
}

# ============================================================================
# Load baseline
# ============================================================================

if (-not (Test-Path $BaselinePath)) {
    Write-Host "ERROR: Baseline file not found at $BaselinePath" -ForegroundColor Red
    Write-Host "  Run with -GenerateBaseline to create one from current source." -ForegroundColor Yellow
    exit 2
}

$baselineJson = Get-Content -Path $BaselinePath -Raw | ConvertFrom-Json
$baselineSuppressions = @($baselineJson.suppressions)
Write-Host "  Loaded $($baselineSuppressions.Count) baseline suppression(s)" -ForegroundColor Green

# ============================================================================
# Compare: find NEW suppressions not in baseline
# ============================================================================

# Build lookup sets using fingerprint (file + warningId + justification) instead of
# line numbers. Line numbers shift whenever code above a suppression is edited, causing
# false-positive "new/stale" pairs. The fingerprint is stable across line shifts.
# Line numbers are still stored for reporting but not used for matching.
#
# To handle multiple identical suppressions in the same file (same warningId + justification
# on different methods), we count occurrences and match by count.

function Get-FingerprintCounts {
    param([array]$Items, [string]$FileKey, [string]$WarningKey, [string]$JustificationKey)
    $counts = @{}
    foreach ($item in $Items) {
        $key = "$($item.$FileKey)|$($item.$WarningKey)|$($item.$JustificationKey)"
        if ($counts.ContainsKey($key)) { $counts[$key]++ } else { $counts[$key] = 1 }
    }
    return $counts
}

Assert-AuditSourceManifest
$baselineCounts = Get-FingerprintCounts -Items $baselineSuppressions -FileKey 'file' -WarningKey 'warningId' -JustificationKey 'justification'
$sourceCounts = Get-FingerprintCounts -Items $sourceSuppressions -FileKey 'File' -WarningKey 'WarningId' -JustificationKey 'Justification'

# New suppressions: fingerprints in source with a higher count than baseline
$newSuppressions = @()
$seenSourceCounts = @{}
foreach ($s in $sourceSuppressions) {
    $key = "$($s.File)|$($s.WarningId)|$($s.Justification)"
    if ($seenSourceCounts.ContainsKey($key)) { $seenSourceCounts[$key]++ } else { $seenSourceCounts[$key] = 1 }
    $baselineCount = if ($baselineCounts.ContainsKey($key)) { $baselineCounts[$key] } else { 0 }
    if ($seenSourceCounts[$key] -gt $baselineCount) {
        $newSuppressions += $s
    }
}

# Stale baseline entries: fingerprints in baseline with a higher count than source
$staleEntries = @()
$seenBaselineCounts = @{}
foreach ($b in $baselineSuppressions) {
    $key = "$($b.file)|$($b.warningId)|$($b.justification)"
    if ($seenBaselineCounts.ContainsKey($key)) { $seenBaselineCounts[$key]++ } else { $seenBaselineCounts[$key] = 1 }
    $sourceCount = if ($sourceCounts.ContainsKey($key)) { $sourceCounts[$key] } else { 0 }
    if ($seenBaselineCounts[$key] -gt $sourceCount) {
        $staleEntries += $b
    }
}

# ============================================================================
# Report
# ============================================================================

$reportJson = @{
    SourceManifest = $script:SourceManifest
    CandidateAssumption = $script:CandidateAssumption
    ParserAssemblies = $script:ParserEvidence
    ParsedSources = $script:ParsedSources
    Timestamp          = (Get-Date -Format 'o')
    SourceSuppressions = $sourceSuppressions.Count
    BaselineEntries    = $baselineSuppressions.Count
    NewSuppressions    = $newSuppressions.Count
    StaleEntries       = $staleEntries.Count
    NewDetails         = @($newSuppressions | ForEach-Object {
        @{ File = $_.File; Line = $_.Line; WarningId = $_.WarningId; Justification = $_.Justification }
    })
    StaleDetails       = @($staleEntries | ForEach-Object {
        @{ File = $_.file; Line = $_.line; WarningId = $_.warningId; Justification = $_.justification }
    })
}

Assert-AuditSourceManifest
$reportJsonPath = Join-Path $OutputPath 'aot-suppression-audit.json'
$reportJson | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportJsonPath -Encoding utf8

Write-Host ""
Write-Host "========================================"
Write-Host "  AOT Suppression Audit Summary"
Write-Host "========================================"
Write-Host "  Source suppressions:  $($sourceSuppressions.Count)"
Write-Host "  Baseline entries:     $($baselineSuppressions.Count)"
Write-Host "  NEW (unapproved):     $($newSuppressions.Count)"
Write-Host "  STALE (removed):      $($staleEntries.Count)"
Write-Host "========================================"
Write-Host ""

if ($newSuppressions.Count -gt 0) {
    Write-Host "NEW UNAPPROVED SUPPRESSIONS:" -ForegroundColor Red
    foreach ($s in $newSuppressions) {
        Write-Host "  $($s.File):$($s.Line) $($s.WarningId)" -ForegroundColor Red
        if ($s.Justification) {
            Write-Host "    Justification: $($s.Justification)" -ForegroundColor Yellow
        } else {
            Write-Host "    Justification: MISSING" -ForegroundColor Red
        }
    }
    Write-Host ""
    Write-Host "To approve these suppressions, add them to the baseline:" -ForegroundColor Yellow
    Write-Host "  ./eng/ci/Invoke-AotSuppressionAudit.ps1 -GenerateBaseline" -ForegroundColor Yellow
    Write-Host "  Then review and commit eng/ci/aot-suppression-baseline.json" -ForegroundColor Yellow
    Write-Host "  WARNING: -GenerateBaseline OVERWRITES the baseline and emits only" -ForegroundColor Red
    Write-Host "  file/line/warningId/justification. Any hand-authored field on an existing row" -ForegroundColor Red
    Write-Host "  (verdict, classifiedBy, note) is DELETED, and no regeneration restores it." -ForegroundColor Red
    Write-Host "  Diff the result before committing; for a few rows, edit the file instead." -ForegroundColor Red
    Write-Host ""
}

if ($staleEntries.Count -gt 0) {
    Write-Host "STALE BASELINE ENTRIES (suppression removed from source):" -ForegroundColor Yellow
    foreach ($s in $staleEntries) {
        Write-Host "  $($s.file):$($s.line) $($s.warningId)" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "Consider regenerating the baseline to remove stale entries:" -ForegroundColor Yellow
    Write-Host "  ./eng/ci/Invoke-AotSuppressionAudit.ps1 -GenerateBaseline" -ForegroundColor Yellow
    Write-Host "  WARNING: this OVERWRITES the baseline and deletes any hand-authored verdict/" -ForegroundColor Red
    Write-Host "  classifiedBy/note fields on rows it rewrites. Diff before committing." -ForegroundColor Red
    Write-Host ""
}

Write-Host "Report: $reportJsonPath" -ForegroundColor Cyan

# Exit code: fail only on NEW suppressions
if ($newSuppressions.Count -gt 0) {
    Write-Host "AOT suppression audit FAILED - $($newSuppressions.Count) new unapproved suppression(s)." -ForegroundColor Red
    exit 1
}

Write-Host "AOT suppression audit PASSED." -ForegroundColor Green
exit 0
