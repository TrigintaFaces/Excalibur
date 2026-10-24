// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Boundary.Tests;

/// <summary>Checks source declarations cited by subsystem guarantees, not whether those tests ran.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class ArchitectureEvidenceShould(ITestOutputHelper output)
{
    private static readonly string[] SourceRoots = ["src", "tests"];
    internal sealed record DeclarationSource(string Path, string Owner, string Text);
    internal sealed record DeclaredType(string Name, string QualifiedName, string Identity, HashSet<string> Methods);

    internal static IReadOnlyList<DeclaredType> Index(IEnumerable<DeclarationSource> sources)
    {
        var types = new Dictionary<string, DeclaredType>(StringComparer.Ordinal);
        var count = 0;
        foreach (var source in sources)
        {
            count++;
            foreach (var tree in CSharpSourceInventory.ParseConfigurations(source.Text))
            {
                foreach (var type in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var containing = type.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Reverse().ToArray();
                    var namespaces = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse()
                        .SelectMany(item => item.Name.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                        .Select(item => item.Identifier.ValueText);
                    var prefix = string.Join(".", namespaces);
                    var name = string.Join(".", containing.Select(item => item.Identifier.ValueText));
                    var qualified = prefix.Length == 0 ? name : prefix + "." + name;
                    var signature = string.Join(".", containing.Select(item =>
                        $"{item.Identifier.ValueText}`{item.TypeParameterList?.Parameters.Count ?? 0}"));
                    var scope = containing.Any(item => item.Modifiers.Any(SyntaxKind.FileKeyword)) ? source.Path : source.Owner;
                    var identity = scope + "|" + prefix + "|" + signature;
                    if (!types.TryGetValue(identity, out var entry))
                    {
                        entry = new DeclaredType(type.Identifier.ValueText, qualified, identity, new HashSet<string>(StringComparer.Ordinal));
                        types.Add(identity, entry);
                    }

                    // Only direct declarations count. Calls, comments, local functions and nested types cannot lend methods.
                    entry.Methods.UnionWith(type.Members.OfType<MethodDeclarationSyntax>().Select(method => method.Identifier.ValueText));
                }
            }
        }

        if (count == 0 || types.Count == 0)
        {
            throw new InvalidOperationException("Evidence validation requires a nonempty C# declaration population.");
        }

        return types.Values.ToArray();
    }

    internal static bool Resolves(string citation, IReadOnlyList<DeclaredType> types)
    {
        citation = citation.StartsWith("global::", StringComparison.Ordinal) ? citation[8..] : citation;
        if (!citation.Contains('.') && !IsTypeCitation(citation) &&
            Regex.IsMatch(citation, @"^[A-Z][A-Za-z0-9]*_[A-Za-z0-9_]+$"))
        {
            return types.Any(type => type.Methods.Contains(citation));
        }
        var exactTypes = types.Where(type => type.QualifiedName == citation || type.Name == citation).ToArray();
        if (exactTypes.Length != 0)
        {
            return exactTypes.Length == 1;
        }

        var separator = citation.LastIndexOf('.');
        if (separator < 0)
        {
            // Existing unscoped arm citations assert existence only; they do not identify an implementation.
            return !IsTypeCitation(citation) && Regex.IsMatch(citation, @"^[A-Z][A-Za-z0-9]*_[A-Za-z0-9_]+$") &&
                   types.Any(type => type.Methods.Contains(citation));
        }

        var owner = citation[..separator];
        var candidates = types.Where(type => type.QualifiedName == owner || type.Name == owner).ToArray();
        return candidates.Length == 1 && candidates[0].Methods.Contains(citation[(separator + 1)..]);
    }

    internal static IReadOnlyList<string> Citations(string document, out bool hasEvidence)
    {
        // Regex is limited to Markdown. C# declarations are read exclusively through Roslyn syntax trees.
        var sections = Regex.Matches(document, @"^##\s+Evidence\b[^\r\n]*(?:\r?\n|\z)(?<body>.*?)(?=^##\s|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        hasEvidence = sections.Count != 0;
        return sections.SelectMany(section => Regex.Matches(section.Groups["body"].Value, @"`([^`\r\n]+)`"))
            .Select(match => match.Groups[1].Value.Trim()).Where(IsCitation).ToArray();
    }

    private static bool IsCitation(string token) =>
        Regex.IsMatch(token, @"^[A-Za-z_]\w*\.[A-Za-z_]\w*$") ||
        // Explicit qualification distinguishes symbol citations from dotted package names and settings.
        Regex.IsMatch(token, @"^global::[A-Za-z_]\w*(\.[A-Za-z_]\w*)+$") ||
        IsTypeCitation(token) ||
        Regex.IsMatch(token, @"^[A-Z][A-Za-z0-9]*_[A-Za-z0-9_]+$");

    private static bool IsTypeCitation(string token) => Regex.IsMatch(token, @"^[A-Za-z_]\w*$") &&
        Regex.IsMatch(token, @"(Should|Shoulds|Tests?|Spec|Fixture|Kit|Base|Suite)$");

    internal static IReadOnlyList<string> Validate(IReadOnlyList<SourceFile> documents,
        IReadOnlyList<DeclaredType> types, IReadOnlySet<string> baseline)
    {
        if (documents.Count == 0)
        {
            throw new InvalidOperationException("Evidence validation requires ARCHITECTURE.md documents.");
        }

        var errors = new List<string>();
        var zero = new HashSet<string>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            var citations = Citations(document.Text, out var hasEvidence);
            if (hasEvidence && citations.Count == 0)
            {
                zero.Add(document.Path);
                if (!baseline.Contains(document.Path))
                {
                    errors.Add($"{document.Path}: Evidence section contains no classifiable citation.");
                }
            }

            errors.AddRange(citations.Where(citation => !Resolves(citation, types))
                .Select(citation => $"{document.Path}: `{citation}` is absent, ambiguous, or not declared on the cited type."));
        }

        errors.AddRange(baseline.Except(zero).Select(path => $"{path}: stale zero-citation baseline entry."));
        return errors;
    }

    [Fact]
    public void ResolveEverySubsystemEvidenceCitationToItsDeclaredOwner()
    {
        var root = TestHelpers.GetRepositoryRoot();
        var ownerCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string Owner(string path)
        {
            var directory = Path.GetDirectoryName(path)!;
            if (ownerCache.TryGetValue(directory, out var cached))
            {
                return cached;
            }

            for (var candidate = directory; candidate is not null && candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
                 candidate = Path.GetDirectoryName(candidate))
            {
                var projects = Directory.GetFiles(candidate, "*.csproj");
                if (projects.Length > 1)
                {
                    throw new InvalidOperationException($"Ambiguous source ownership: {path}; {string.Join(", ", projects)}");
                }

                if (projects.Length == 1)
                {
                    ownerCache.Add(directory, projects[0]);
                    return projects[0];
                }
            }

            // Standalone fixture sources have no project; never merge them across files.
            return path;
        }

        var sources = CSharpSourceInventory.ReadSources(SourceRoots
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .Where(CSharpSourceInventory.IsProductionSource));
        var types = Index(sources.Select(source => new DeclarationSource(source.Path, Owner(source.Path), source.Text)));
        var documents = Directory.EnumerateFiles(Path.Combine(root, "src"), "ARCHITECTURE.md", SearchOption.AllDirectories)
            .Where(CSharpSourceInventory.IsProductionSource)
            .Select(path => new SourceFile(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path))).ToArray();
        var baseline = File.ReadLines(Path.Combine(root, "eng", "ci", "architecture-evidence-baseline.txt"))
            .Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')).ToHashSet(StringComparer.Ordinal);
        output.WriteLine($"EXAMINED: {sources.Count} C# sources; {types.Count} type identities; {documents.Length} documents; " +
                         $"{documents.Sum(document => Citations(document.Text, out _).Count)} citations.");
        Validate(documents, types, baseline).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("class Probe { void Arm() {} }", "Probe.Arm", true)]
    [InlineData("class Probe { void Renamed() {} }", "Probe.Arm", false)]
    [InlineData("class Other { void GhostShould() {} }", "GhostShould", false)]
    [InlineData("class Detect_arm {}", "Detect_arm", false)]
    [InlineData("class Other { void Detect_arm() {} }", "Detect_arm", true)]
    [InlineData("namespace @Example . Tests; class Probe { void Arm() {} }", "global::Example.Tests.Probe.Arm", true)]
    [InlineData("class Probe { /* void Arm() {} */ }", "Probe.Arm", false)]
    [InlineData("class Probe { string text = \"void Arm() {}\"; }", "Probe.Arm", false)]
    [InlineData("class Probe { void Other() { Arm(); } }", "Probe.Arm", false)]
    [InlineData("class Probe {} class Other { void Arm() {} }", "Probe.Arm", false)]
    [InlineData("class Probe { class Nested { void Arm() {} } }", "Probe.Arm", false)]
    [InlineData("class Probe { void Other() { void Arm() {} } }", "Probe.Arm", false)]
    [InlineData("class Probe { class Nested { void Arm() {} } }", "Probe.Nested.Arm", true)]
    [InlineData("namespace A { class Probe {} } namespace B { class Probe { void Arm() {} } }", "Probe.Arm", false)]
    [InlineData("namespace A { class Probe {} } namespace B { class Probe { void Arm() {} } }", "B.Probe.Arm", true)]
    [InlineData("namespace A { class Probe {} } namespace B { class Probe {} }", "B.Probe", true)]
    [InlineData("class Probe {} class Probe<T> { void Arm() {} }", "Probe.Arm", false)]
    [InlineData("interface Probe { void Arm(); }", "Probe.Arm", true)]
    [InlineData("record Probe { void Arm() {} }", "Probe.Arm", true)]
    [InlineData("record struct Probe { void Arm() {} }", "Probe.Arm", true)]
    [InlineData("readonly struct Probe { void Arm() {} }", "Probe.Arm", true)]
    [InlineData("partial class Probe { partial void Arm(); }", "Probe.Arm", true)]
    [InlineData("#if FEATURE\nclass Probe { void Arm() {} }\n#endif", "Probe.Arm", true)]
    public void ResolveDeclarationsRatherThanWords(string source, string citation, bool expected) =>
        Resolves(citation, Index([new("fixture.cs", "fixture.csproj", source)])).ShouldBe(expected);

    [Theory]
    [InlineData("partial", "same", true)]
    [InlineData("file", "same", false)]
    [InlineData("partial", "other", false)]
    public void RespectPartialTypeAndFileAndProjectIdentities(string modifier, string secondOwner, bool expected) =>
        Resolves("Probe.Arm", Index([
            new("one.cs", "same", $"{modifier} class Probe {{ }}"),
            new("two.cs", secondOwner, $"{modifier} class Probe {{ void Arm() {{ }} }}")])).ShouldBe(expected);

    [Theory]
    [InlineData("`ProbeShould.Arm`", true)]
    [InlineData("`ProbeShould.Missing`", false)]
    [InlineData("`GhostShould.Arm`", false)]
    [InlineData("`ProbeShould`", true)]
    [InlineData("`GhostShould`", false)]
    [InlineData("`Detect_arm`", true)]
    [InlineData("`Detect_missing`", false)]
    [InlineData("`ProbeShould.Arm`; ordinary `IStore` and `widget_events`.", true)]
    [InlineData("No cited evidence.", false)]
    public void RetainCitationShapesAndRejectUnresolvedEvidence(string body, bool valid)
    {
        var types = Index([new("test.cs", "tests", "class ProbeShould { void Arm() {} void Detect_arm() {} }")]);
        Validate([new("src/ARCHITECTURE.md", "## Evidence\n" + body)], types, new HashSet<string>()).Count.ShouldBe(valid ? 0 : 1);
    }

    [Fact]
    public void RatchetZeroCitationDebtAndRefuseEmptyInputs()
    {
        var types = Index([new("test.cs", "tests", "class ProbeShould {}")]);
        var baseline = new HashSet<string> { "doc" };
        Validate([new("doc", "## Evidence\nNothing cited.")], types, baseline).ShouldBeEmpty();
        Validate([new("doc", "## Evidence\n`ProbeShould`")], types, baseline).Count.ShouldBe(1);
        Validate([new("doc", "No Evidence section.")], types, baseline).Count.ShouldBe(1);
        Validate([new("other", "No Evidence section.")], types, baseline).Count.ShouldBe(1);
        Validate([new("doc", "No Evidence section.")], types, new HashSet<string>()).ShouldBeEmpty();
        Validate([new("doc", "## Evidence")], types, new HashSet<string>()).Count.ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => Validate([], types, baseline));
        Should.Throw<InvalidOperationException>(() => Index([]));
    }

    [Fact]
    public void DistinguishQualifiedCitationsFromPackageNamesAndSettings()
    {
        Citations("## Evidence\n`global::Example.Tests.Probe.Arm` `Excalibur.Testing.Conformance` `index.blocks.write`", out var evidence)
            .ShouldBe(["global::Example.Tests.Probe.Arm"]);
        evidence.ShouldBeTrue();
    }
}
