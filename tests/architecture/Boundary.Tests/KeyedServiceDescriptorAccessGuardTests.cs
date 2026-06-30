// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Boundary.Tests;

/// <summary>
/// Author≠implementer structural guard for <c>bd-ybem93</c> (S856): production / shipped source MUST NOT
/// read the NON-keyed <see cref="System.Object"/> members
/// <c>ServiceDescriptor.ImplementationType</c> / <c>.ImplementationInstance</c> / <c>.ImplementationFactory</c>
/// directly. Those getters throw <see cref="System.InvalidOperationException"/> for a keyed descriptor on
/// .NET 8.x (dotnet/runtime#95789) and silently mis-read on net9/10, so every read must go through the single
/// sanctioned keyed-safe accessor <c>ServiceDescriptorExtensions.GetImplementation*</c>.
/// </summary>
/// <remarks>
/// <para>
/// Implementer = BackendDeveloper (the codebase-wide sweep to <c>GetImplementation*</c>); this is the
/// independent guard authored by TestsDeveloper.
/// </para>
/// <para>
/// <b>Why syntax-based, not a text grep (SA ruling 17440 §3 + <c>enforce-invariants-structurally</c>):</b> a
/// text scan false-positives on doc strings / <c>Justification=</c> attribute text and gets weakened until it
/// stops catching real reads. This guard walks the C# <b>syntax tree</b> and only flags
/// <see cref="MemberAccessExpressionSyntax"/> nodes — so string literals (e.g. the <c>Justification</c> text at
/// <c>DispatchServiceCollectionExtensions.cs:164</c>) and XML-doc <c>&lt;see cref&gt;</c> references are
/// inherently excluded (they are not member-access nodes). The sanctioned accessor's own file is excluded by
/// path. Non-shipped unit-test code may opt out per-line with the allowlist marker below; shipped packages
/// never may.
/// </para>
/// <para>
/// Non-vacuity is proven WITHOUT mutating shared production source: the in-memory self-tests prove the
/// analyzer flags a reintroduced raw read and does NOT flag the member name inside a string literal or
/// XML-doc cref.
/// </para>
/// </remarks>
public sealed class KeyedServiceDescriptorAccessGuardTests
{
    /// <summary>The non-keyed <c>ServiceDescriptor</c> implementation getters that must never be read directly.</summary>
    private static readonly HashSet<string> BannedMembers = new(StringComparer.Ordinal)
    {
        "ImplementationType",
        "ImplementationInstance",
        "ImplementationFactory",
    };

    /// <summary>The single sanctioned file allowed to read the raw getters (it IS the keyed-safe accessor).</summary>
    private const string SanctionedAccessorFileName = "ServiceDescriptorExtensions.cs";

    /// <summary>Per-line opt-out marker for non-shipped test code that deliberately constructs/reads raw descriptors.</summary>
    private const string AllowlistMarker = "ybem93-allow-raw-descriptor-read";

    [Fact]
    public void ProductionSourceMustNotReadRawServiceDescriptorImplementationMembers()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");
        Directory.Exists(srcRoot).ShouldBeTrue($"Expected production source root at '{srcRoot}'.");

        var violations = new List<string>();

        foreach (var file in EnumerateAnalyzableSourceFiles(srcRoot))
        {
            var text = File.ReadAllText(file);
            foreach (var (line, snippet) in FindRawImplementationReads(text))
            {
                violations.Add($"{NormalizeForReport(repoRoot, file)}:{line} -> {snippet}");
            }
        }

        violations.ShouldBeEmpty(
            "bd-ybem93: raw non-keyed ServiceDescriptor.Implementation* reads found in shipped source. " +
            "Replace each with the keyed-safe ServiceDescriptorExtensions.GetImplementation* accessor " +
            $"(a keyed descriptor throws on those raw getters). Offending sites:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    // ── Non-vacuity self-tests (no shared-source mutation) ───────────────────

    [Fact]
    public void Analyzer_Flags_AReintroducedRawRead()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            internal static class Offender
            {
                public static object? Read(ServiceDescriptor descriptor)
                {
                    return descriptor.ImplementationInstance;
                }
            }
            """;

        var hits = FindRawImplementationReads(source);

        hits.ShouldNotBeEmpty("the analyzer MUST flag a raw .ImplementationInstance member-access read.");
        hits.ShouldContain(h => h.Snippet.Contains("ImplementationInstance", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyzer_DoesNotFlag_MemberNameInsideStringLiteralOrXmlDocCref()
    {
        // Mirrors the legitimate DispatchServiceCollectionExtensions.cs:164 Justification string + an
        // XML-doc cref. A text grep would false-positive on both; the syntax walk must not.
        const string source = """
            using System.Diagnostics.CodeAnalysis;
            internal static class NotAnOffender
            {
                /// <summary>See <see cref="ServiceDescriptor.ImplementationType"/> for details.</summary>
                [SuppressMessage("x", "y",
                    Justification = "Handler types come from ServiceDescriptor.ImplementationType which lacks annotations.")]
                public static string Doc() => "reads ImplementationFactory and ImplementationInstance in prose";
            }
            """;

        var hits = FindRawImplementationReads(source);

        hits.ShouldBeEmpty(
            "the analyzer MUST NOT flag the banned member names when they appear only inside string " +
            "literals or XML-doc <see cref> — that is the vacuity trap a text grep falls into.");
    }

    // ── Analyzer ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <paramref name="sourceText"/> as C# and returns every direct member-access read of a banned
    /// non-keyed <c>ServiceDescriptor</c> implementation getter. Allowlist-marked lines are skipped.
    /// </summary>
    private static IReadOnlyList<(int Line, string Snippet)> FindRawImplementationReads(string sourceText)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        var hits = new List<(int Line, string Snippet)>();

        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (!BannedMembers.Contains(access.Name.Identifier.ValueText))
            {
                continue;
            }

            // 1-based line number of the member access.
            var lineSpan = access.SyntaxTree.GetLineSpan(access.Span);
            var line = lineSpan.StartLinePosition.Line + 1;

            // Per-line opt-out for deliberately-raw non-shipped test code.
            if (GetSourceLine(sourceText, line).Contains(AllowlistMarker, StringComparison.Ordinal))
            {
                continue;
            }

            hits.Add((line, access.ToString()));
        }

        return hits;
    }

    private static IEnumerable<string> EnumerateAnalyzableSourceFiles(string srcRoot)
    {
        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');

            if (normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            {
                continue; // build artifacts
            }

            if (normalized.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/GeneratedFiles/", StringComparison.OrdinalIgnoreCase))
            {
                continue; // source-generated output
            }

            if (Path.GetFileName(file).Equals(SanctionedAccessorFileName, StringComparison.Ordinal))
            {
                continue; // the one sanctioned raw-read site (the keyed-safe accessor itself)
            }

            yield return file;
        }
    }

    private static string GetSourceLine(string sourceText, int oneBasedLine)
    {
        var lines = sourceText.Split('\n');
        var index = oneBasedLine - 1;
        return index >= 0 && index < lines.Length ? lines[index] : string.Empty;
    }

    private static string NormalizeForReport(string repoRoot, string file)
    {
        var relative = Path.GetRelativePath(repoRoot, file);
        return relative.Replace('\\', '/');
    }
}
