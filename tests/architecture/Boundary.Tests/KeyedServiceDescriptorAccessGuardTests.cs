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
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
    public void Analyzer_Flags_ANullConditionalRawRead()
    {
        // THE ARM THAT DID NOT EXIST. Roslyn models `descriptor?.ImplementationType` as a
        // ConditionalAccessExpressionSyntax whose WhenNotNull is a MemberBindingExpressionSyntax -- NOT a
        // MemberAccessExpressionSyntax at any depth. A walk over MemberAccessExpressionSyntax alone is
        // therefore blind to every '?.' read BY CONSTRUCTION. Two such reads exist in production source
        // and were invisible to this gate, so converting the reads it DID report would have turned the
        // gate green over a keyed-unsafe read that still shipped.
        //
        // This arm goes RED against the pre-fix analyzer. If it ever goes green while the plain-dot arm
        // still passes, the walk has been narrowed back and every '?.' read is unguarded again.
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            internal static class Offender
            {
                public static bool Read(ServiceDescriptor? descriptor)
                {
                    return descriptor?.ImplementationType == typeof(string);
                }
            }
            """;

        var hits = FindRawImplementationReads(source);

        hits.ShouldNotBeEmpty(
            "the analyzer MUST flag a null-conditional '?.ImplementationType' read. If this is empty, the "
            + "walk only covers MemberAccessExpressionSyntax and every '?.' read of a banned "
            + "ServiceDescriptor getter is invisible to this gate.");
        hits.ShouldContain(h => h.Snippet.Contains("ImplementationType", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyzer_Flags_ANullConditionalRawRead_WhenTheAccessIsSplitAcrossLines()
    {
        // The multi-line form, which is how ServiceCollectionTransportExtensions.cs writes it: the '?'
        // terminates one line and the member begins the next. A line-oriented text search for "?\." cannot
        // match this at all -- I made exactly that mistake classifying these sites, which is why the
        // syntax walk is the right instrument and why this case gets its own arm.
        //
        // It also pins the reported LINE to the banned member rather than to the start of the chained
        // expression, because the per-line allowlist opt-out consults that line: report the receiver's
        // line and an allowlist marker placed on the offending line would be silently ignored.
        const string source = """
            using System.Linq;
            using Microsoft.Extensions.DependencyInjection;
            internal static class Offender
            {
                public static object? Read(IServiceCollection services)
                {
                    return services
                        .FirstOrDefault(static d => d.ServiceType == typeof(string))?
                        .ImplementationInstance;
                }
            }
            """;

        var hits = FindRawImplementationReads(source);

        hits.ShouldNotBeEmpty(
            "the analyzer MUST flag a null-conditional read whose '?' and member sit on different lines.");
        hits.ShouldContain(
            h => h.Line == 9,
            "the hit MUST be reported on the line carrying '.ImplementationInstance' (9), not on the line "
            + "the chained expression starts at -- the per-line allowlist opt-out reads that line, so a "
            + "misreported line makes the opt-out consult source the banned member is not on. Reported: "
            + string.Join(", ", hits.Select(h => h.Line)));
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

        // BOTH access shapes, because Roslyn models them as DIFFERENT node types and walking only the
        // first is blind to the second BY CONSTRUCTION, not by tuning:
        //
        //   descriptor.ImplementationType     -> MemberAccessExpressionSyntax
        //   descriptor?.ImplementationType    -> ConditionalAccessExpressionSyntax
        //                                        whose .WhenNotNull is a MemberBindingExpressionSyntax
        //
        // The null-conditional form is NOT a MemberAccessExpression at any depth, so an OfType<
        // MemberAccessExpressionSyntax>() walk cannot see it however the source is formatted -- including
        // when the '?' and the member sit on different lines, which is how one of the two real sites in
        // this repo is written:
        //
        //   .FirstOrDefault(static d => d.ServiceType == typeof(TransportValidationOptions))?
        //   .ImplementationInstance as TransportValidationOptions;
        //
        // Converting only the reported sites would therefore turn this gate GREEN while a real
        // keyed-unsafe read still shipped -- a false green in the gate built to prevent exactly that.
        foreach (var node in root.DescendantNodes())
        {
            SimpleNameSyntax? name = node switch
            {
                MemberAccessExpressionSyntax member => member.Name,
                MemberBindingExpressionSyntax binding => binding.Name,
                _ => null
            };

            if (name is null || !BannedMembers.Contains(name.Identifier.ValueText))
            {
                continue;
            }

            // For a null-conditional read, report the whole `receiver?.Member` expression rather than the
            // bare `.Member` binding, so the diagnostic names something a reader can find in the source.
            var access = node is MemberBindingExpressionSyntax
                ? node.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>() ?? node
                : node;

            // 1-based line of the BANNED MEMBER ITSELF, not of the enclosing expression. A chained
            // conditional access spans several lines and starts at its receiver, so keying off the
            // expression would report the wrong line AND make the per-line allowlist opt-out below
            // consult a line the banned member is not on.
            var lineSpan = name.SyntaxTree.GetLineSpan(name.Span);
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
