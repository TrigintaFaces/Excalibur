using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

using Boundary.Tests;

using Shouldly;

using Xunit;

namespace Boundary.Tests.Architecture;

[Trait("Category", "Integration")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Governance")]
public sealed class PackageTestCoverageMappingTests
{
    private static readonly string RepoRoot = TestHelpers.GetRepositoryRoot();
    private static readonly string GovernancePath = Path.Combine(RepoRoot, "eng", "governance", "framework-governance.json");
    private static readonly string ShippingFilterPath = Path.Combine(RepoRoot, "eng", "ci", "shards", "ShippingOnly.slnf");
    private static readonly SolutionInventory Solution = SolutionInventory.ReadSolution(RepoRoot);
    private static readonly HashSet<string> ShardProjects = Directory.GetFiles(Path.Combine(RepoRoot, "eng/ci/shards"), "*.slnf")
        .SelectMany(path => SolutionInventory.ReadFilter(RepoRoot, path, Solution.Projects)).ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void CriticalPackageTestMatrix_ShouldPointToExistingProjectsAndSuites()
    {
        using var doc = LoadGovernanceJson();
        var root = doc.RootElement;
        var missing = new List<string>();
        root.GetProperty("criticalPackageTestMatrix").GetArrayLength().ShouldBeGreaterThan(0);

        foreach (var entry in root.GetProperty("criticalPackageTestMatrix").EnumerateArray())
        {
            var package = entry.GetProperty("package").GetString() ?? "<unknown>";
            var projectPath = ToAbsolutePath(entry.GetProperty("project").GetString());

            if (!File.Exists(projectPath))
            {
                missing.Add($"Missing critical package project '{projectPath}' for {package}.");
            }

            if (!entry.TryGetProperty("suites", out var suites) || suites.ValueKind != JsonValueKind.Object || !suites.EnumerateObject().Any())
            {
                missing.Add($"Critical package '{package}' has no suite mapping.");
                continue;
            }

            foreach (var suite in suites.EnumerateObject())
            {
                if (suite.Value.ValueKind != JsonValueKind.Array || suite.Value.GetArrayLength() == 0)
                {
                    missing.Add($"Critical package '{package}' has empty suite '{suite.Name}'.");
                    continue;
                }

                foreach (var suitePathElement in suite.Value.EnumerateArray())
                {
                    var suitePath = ToAbsolutePath(suitePathElement.GetString());
                    AssertSuiteMembership(suitePathElement.GetString()!, Solution.Projects.Keys, ShardProjects);
                    if (!File.Exists(suitePath))
                    {
                        missing.Add($"Critical package '{package}' suite '{suite.Name}' references missing project '{suitePath}'.");
                    }
                }
            }
        }

        (missing.Count == 0).ShouldBeTrue(string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void PackageTestMappingRules_ShouldCoverAllShippingPackages()
    {
        using var governanceDoc = LoadGovernanceJson();
        using var shippingDoc = JsonDocument.Parse(File.ReadAllText(ShippingFilterPath));
        var root = governanceDoc.RootElement;

        var rules = root.GetProperty("packageTestMappingRules").EnumerateArray().Select(rule =>
        {
            var name = rule.GetProperty("name").GetString() ?? "<unnamed rule>";
            var pattern = rule.GetProperty("packagePattern").GetString() ?? string.Empty;
            var regex = new Regex(pattern, RegexOptions.Compiled);
            rule.GetProperty("suites").EnumerateObject().ShouldNotBeEmpty();

            foreach (var suite in rule.GetProperty("suites").EnumerateObject())
            {
                suite.Value.GetArrayLength().ShouldBeGreaterThan(0);
                foreach (var suitePathElement in suite.Value.EnumerateArray())
                {
                    var suitePath = ToAbsolutePath(suitePathElement.GetString());
                    AssertSuiteMembership(suitePathElement.GetString()!, Solution.Projects.Keys, ShardProjects);
                    if (!File.Exists(suitePath))
                    {
                        throw new InvalidOperationException($"Mapping rule '{name}' references missing suite project '{suitePath}'.");
                    }
                }
            }

            return (name, regex);
        }).ToArray();
        rules.ShouldNotBeEmpty();

        var uncoveredPackages = new List<string>();
        foreach (var project in shippingDoc.RootElement.GetProperty("solution").GetProperty("projects").EnumerateArray())
        {
            var relativePath = project.GetString();
            var absolutePath = ToAbsolutePath(relativePath);
            if (!File.Exists(absolutePath))
            {
                uncoveredPackages.Add($"Missing shipping project: {absolutePath}");
                continue;
            }

            var packageId = ReadPackageId(absolutePath);
            var covered = rules.Any(rule => rule.regex.IsMatch(packageId));
            if (!covered)
            {
                uncoveredPackages.Add($"{packageId} ({relativePath})");
            }
        }

        (uncoveredPackages.Count == 0).ShouldBeTrue(
            "Shipping packages without test mapping rules: " + string.Join(", ", uncoveredPackages));
    }

    [Fact]
    public void ReleaseGateChecklist_ShouldDeclareAllReleaseBlockingTestSuites()
    {
        using var doc = LoadGovernanceJson();
        var releaseChecklist = doc.RootElement.GetProperty("governance").GetProperty("releaseGateChecklist")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();

        releaseChecklist.ShouldContain(
            item => item.Contains("All required test suites pass", StringComparison.OrdinalIgnoreCase));

        var normalized = string.Join(" ", releaseChecklist).ToUpperInvariant();
        normalized.ShouldContain("SMOKE");
        normalized.ShouldContain("UNIT");
        normalized.ShouldContain("INTEGRATION");
        normalized.ShouldContain("FUNCTIONAL");
        normalized.ShouldContain("CONTRACT");
        normalized.ShouldContain("ARCHITECTURE");
        normalized.ShouldContain("CONFORMANCE");
    }

    private static JsonDocument LoadGovernanceJson()
    {
        if (!File.Exists(GovernancePath))
        {
            throw new FileNotFoundException("Framework governance matrix not found.", GovernancePath);
        }

        return JsonDocument.Parse(File.ReadAllText(GovernancePath));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ProviderSuiteOmission_MustNotBeSatisfiedByASameNamedSuite(bool inSolution, bool inShard)
    {
        const string provider = "tests/unit/Provider/Provider.Tests.csproj";
        const string substitute = "tests/unit/Other/Provider.Tests.csproj";
        var solution = new[] { inSolution ? provider : substitute };
        var shards = new[] { inShard ? provider : substitute };
        Should.Throw<InvalidDataException>(() => AssertSuiteMembership(provider, solution, shards));
        AssertSuiteMembership(provider, [provider, substitute], [provider]);
    }

    private static void AssertSuiteMembership(string path, IEnumerable<string> solution, IEnumerable<string> shards)
    {
        var canonical = SolutionInventory.CanonicalPath(path);
        if (!solution.Contains(canonical, StringComparer.Ordinal) || !shards.Contains(canonical, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Mapped test suite is absent from the solution or every CI shard: {canonical}");
        }
    }

    private static string ReadPackageId(string projectPath)
    {
        var content = File.ReadAllText(projectPath);
        var match = Regex.Match(content, @"<PackageId>\s*(?<id>[^<]+)\s*</PackageId>", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var packageId = match.Groups["id"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(packageId) && !packageId.StartsWith("$(", StringComparison.Ordinal))
            {
                return packageId;
            }
        }

        return Path.GetFileNameWithoutExtension(projectPath);
    }

    private static string ToAbsolutePath(string? relativePath)
    {
        var normalized = SolutionInventory.CanonicalPath(relativePath ?? string.Empty);
        if (!Solution.Projects.ContainsKey(normalized))
        {
            throw new InvalidDataException($"Mapped project is absent from the solution: {normalized}");
        }
        return Path.GetFullPath(Path.Combine(RepoRoot, normalized));
    }
}
