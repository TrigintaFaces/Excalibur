using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ArchitectureTests.Architecture;

public sealed class PackageTestCoverageMappingTests
{
    private static readonly string RepoRoot = GetRepoRoot();
    private static readonly string GovernancePath = Path.Combine(RepoRoot, "management", "governance", "framework-governance.json");
    private static readonly string ShippingFilterPath = Path.Combine(RepoRoot, "eng", "ci", "shards", "ShippingOnly.slnf");

    [Fact]
    public void CriticalPackageTestMatrix_ShouldPointToExistingProjectsAndSuites()
    {
        using var doc = LoadGovernanceJson();
        var root = doc.RootElement;
        var missing = new List<string>();

        foreach (var entry in root.GetProperty("criticalPackageTestMatrix").EnumerateArray())
        {
            var package = entry.GetProperty("package").GetString() ?? "<unknown>";
            var projectPath = ToAbsolutePath(entry.GetProperty("project").GetString());

            if (!File.Exists(projectPath))
            {
                missing.Add($"Missing critical package project '{projectPath}' for {package}.");
            }

            if (!entry.TryGetProperty("suites", out var suites))
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
                    if (!File.Exists(suitePath))
                    {
                        missing.Add($"Critical package '{package}' suite '{suite.Name}' references missing project '{suitePath}'.");
                    }
                }
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
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

            foreach (var suite in rule.GetProperty("suites").EnumerateObject())
            {
                foreach (var suitePathElement in suite.Value.EnumerateArray())
                {
                    var suitePath = ToAbsolutePath(suitePathElement.GetString());
                    if (!File.Exists(suitePath))
                    {
                        throw new InvalidOperationException($"Mapping rule '{name}' references missing suite project '{suitePath}'.");
                    }
                }
            }

            return (name, regex);
        }).ToArray();

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

        Assert.True(
            uncoveredPackages.Count == 0,
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

        Assert.Contains(
            releaseChecklist,
            item => item.Contains("All required test suites pass", StringComparison.OrdinalIgnoreCase));

        var normalized = string.Join(" ", releaseChecklist).ToLowerInvariant();
        Assert.Contains("smoke", normalized);
        Assert.Contains("unit", normalized);
        Assert.Contains("integration", normalized);
        Assert.Contains("functional", normalized);
        Assert.Contains("contract", normalized);
        Assert.Contains("architecture", normalized);
        Assert.Contains("conformance", normalized);
    }

    private static JsonDocument LoadGovernanceJson()
    {
        if (!File.Exists(GovernancePath))
        {
            throw new FileNotFoundException("Framework governance matrix not found.", GovernancePath);
        }

        return JsonDocument.Parse(File.ReadAllText(GovernancePath));
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
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var normalized = relativePath.Replace('\\', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(RepoRoot, normalized));
    }

    private static string GetRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
        {
            if (File.Exists(Path.Combine(dir, "Excalibur.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return Directory.GetCurrentDirectory();
    }
}
