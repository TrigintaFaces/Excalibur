# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
param([Parameter(Mandatory)][string]$RequestPath)
$ErrorActionPreference = 'Stop'
$request = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json
# Add-Type already uses PowerShell's Roslyn. Loading the SDK's different version into
# the same load context fails; use the host's coherent pair and fingerprint it explicitly.
#
# NO SDK IS RESOLVED HERE, DELIBERATELY. The parse runs on the Roslyn pair shipped inside
# PowerShell, so the repository's SDK takes no part in it and cannot inform it. An earlier
# revision read `dotnet --version` purely to label the fingerprint, and threw when it could
# not -- which let an unrelated toolchain veto the whole census, in a job that checks the
# tree out without installing an SDK. A language-level skew between this Roslyn and the
# source is still caught, loudly: every parse Error below becomes a Refusal, which the
# caller reports as UNPARSED with a file and a line. The identity that governs the parse is
# the Roslyn pair, and that is what the payload fingerprints.
$assemblies = @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll') | ForEach-Object { Join-Path $PSHOME $_ }
foreach ($path in $assemblies) { [void][Reflection.Assembly]::LoadFrom($path) }
$references = @($assemblies) + @('System.Runtime.dll','System.Collections.dll','System.Collections.Immutable.dll','System.Linq.dll') |
    ForEach-Object { if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path "$PSHOME/ref" $_ } }
# Match the SDK's default CS1701 version-unification policy for host framework references
# (Microsoft.NET.Sdk.CSharp.props). Other compiler diagnostics remain errors.
Add-Type -ReferencedAssemblies $references -CompilerOptions '/nowarn:1701' -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Excalibur.CI {
    public sealed class SkipSyntax {
        public string Arm { get; set; }
        public string Capability { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string DynamicArmKind { get; set; }
    }
    public sealed class DerivationSyntax {
        public string Kit { get; set; }
        public bool PrivateNested { get; set; }
    }
    public sealed class FileSyntax {
        public string Path { get; set; }
        public int CandidateCount { get; set; }
        public List<SkipSyntax> Calls { get; } = new List<SkipSyntax>();
        public List<DerivationSyntax> Derivations { get; } = new List<DerivationSyntax>();
        public List<string> Refusals { get; } = new List<string>();
    }
    public static class ConformanceSyntax {
        static string Terminal(TypeSyntax type) {
            if (type is IdentifierNameSyntax id) return id.Identifier.ValueText;
            if (type is GenericNameSyntax generic) return generic.Identifier.ValueText;
            if (type is QualifiedNameSyntax qualified) return Terminal(qualified.Right);
            if (type is AliasQualifiedNameSyntax alias) return Terminal(alias.Name);
            return null;
        }
        static string Method(ExpressionSyntax expression) {
            if (expression is IdentifierNameSyntax id) return id.Identifier.ValueText;
            if (expression is GenericNameSyntax generic) return generic.Identifier.ValueText;
            if (expression is MemberAccessExpressionSyntax member) return member.Name.Identifier.ValueText;
            if (expression is MemberBindingExpressionSyntax binding) return binding.Name.Identifier.ValueText;
            return null;
        }
        static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        public static FileSyntax Parse(string path, string[] kitNames) {
            var result = new FileSyntax { Path = path };
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.Preview), path);
            var root = tree.GetRoot();
            foreach (var error in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                result.Refusals.Add((error.Location.GetLineSpan().StartLinePosition.Line + 1) + ": " + error.Id + " syntax error");
            foreach (var disabled in root.DescendantTrivia(descendIntoTrivia: true).Where(t => t.IsKind(SyntaxKind.DisabledTextTrivia)))
                if (disabled.ToString().Contains("SkipArm") || kitNames.Any(k => disabled.ToString().Contains(k)))
                    result.Refusals.Add((disabled.GetLocation().GetLineSpan().StartLinePosition.Line + 1) + ": conditional candidate requires configuration review");
            var aliases = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                .Where(u => u.Alias != null && u.Name != null && kitNames.Contains(Terminal(u.Name)))
                .Select(u => u.Alias.Name.Identifier.ValueText).ToArray();
            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>()) {
                if (type.BaseList == null) continue;
                foreach (var baseType in type.BaseList.Types) {
                    var name = Terminal(baseType.Type);
                    if (aliases.Contains(name)) {
                        result.Refusals.Add(Line(baseType) + ": aliased kit base requires semantic resolution");
                        continue;
                    }
                    if (kitNames.Contains(name)) result.Derivations.Add(new DerivationSyntax {
                        Kit = name,
                        PrivateNested = type.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))
                            && type.Ancestors().OfType<TypeDeclarationSyntax>().Any()
                    });
                }
            }
            foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(c => Method(c.Expression) == "SkipArm")) {
                result.CandidateCount++;
                try {
                    if (call.Expression is GenericNameSyntax || call.Expression is MemberBindingExpressionSyntax
                        || call.Expression is MemberAccessExpressionSyntax genericMember && genericMember.Name is GenericNameSyntax)
                        throw new InvalidOperationException("generic or conditional receiver requires semantic resolution");
                    if (call.Expression is MemberAccessExpressionSyntax member
                        && !(member.Expression is ThisExpressionSyntax) && !(member.Expression is BaseExpressionSyntax))
                        throw new InvalidOperationException("receiver is not the kit instance");
                    var names = new[] { "arm", "capability", "reason" };
                    var values = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
                    var index = 0;
                    foreach (var argument in call.ArgumentList.Arguments) {
                        if (index >= names.Length || !argument.RefKindKeyword.IsKind(SyntaxKind.None))
                            throw new InvalidOperationException("unsupported argument shape");
                        var name = argument.NameColon == null ? names[index] : argument.NameColon.Name.Identifier.ValueText;
                        if (!names.Contains(name) || values.ContainsKey(name) || argument.Expression.IsMissing)
                            throw new InvalidOperationException("unknown, duplicate or missing argument");
                        values.Add(name, argument.Expression); index++;
                    }
                    if (values.Count != names.Length) throw new InvalidOperationException("incomplete argument list");
                    var arm = values["arm"] as InvocationExpressionSyntax;
                    string armName = null;
                    if (arm != null && arm.Expression is IdentifierNameSyntax function && function.Identifier.ValueText == "nameof"
                        && arm.ArgumentList.Arguments.Count == 1 && arm.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax identifier)
                        armName = identifier.Identifier.ValueText;
                    string capability = null;
                    if (!values["capability"].IsKind(SyntaxKind.NullLiteralExpression)) {
                        var typeOf = values["capability"] as TypeOfExpressionSyntax;
                        if (typeOf == null || !(typeOf.Type is IdentifierNameSyntax capName))
                            throw new InvalidOperationException("capability requires semantic resolution");
                        capability = capName.Identifier.ValueText;
                    }
                    result.Calls.Add(new SkipSyntax { Arm = armName, Capability = capability, Line = Line(call),
                        Column = call.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                        DynamicArmKind = armName == null ? values["arm"].Kind().ToString() : null });
                } catch (InvalidOperationException error) { result.Refusals.Add(Line(call) + ": " + error.Message); }
            }
            return result;
        }
    }
}
'@
$files = @($request.paths | ForEach-Object { [Excalibur.CI.ConformanceSyntax]::Parse($_, [string[]]$request.kits) })
[ordered]@{
    parserHost = "PowerShell $($PSVersionTable.PSVersion)"
    roslyn = @($assemblies | ForEach-Object { @{path=$_;sha256=(Get-FileHash -LiteralPath $_).Hash;version=[Reflection.AssemblyName]::GetAssemblyName($_).Version.ToString()} })
    files = $files
} | ConvertTo-Json -Depth 8 -Compress
