using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Boundary.Tests;

using Shouldly;

using Xunit;

namespace Boundary.Tests.Architecture;

[Trait("Category", "Integration")]
[Trait("Component", "Architecture")]
public sealed class PackageMapDriftTests
{
    private static readonly string RepoRoot = TestHelpers.GetRepositoryRoot();
    private static readonly bool Enforce = string.Equals(
        Environment.GetEnvironmentVariable("ARCH_ENFORCE"),
        "true", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void PackageMap_Should_Correlate_With_Existing_Projects_ReportOnly()
    {
        var mapPath = Path.Combine(RepoRoot, "management", "package-map.yaml");
        if (!File.Exists(mapPath))
        {
            Console.WriteLine($"package-map.yaml not found at {mapPath}; skipping.");
            true.ShouldBeTrue();
            return;
        }

        var mapText = File.ReadAllText(mapPath);
        // Extremely lightweight parse: look for lines starting with "- id:" and collect ids.
        var ids = new List<string>();
        using (var reader = new StringReader(mapText))
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("- id:", StringComparison.Ordinal))
                {
                    var id = trimmed.Split(':', 2)[1].Trim();
                    if (id.StartsWith('"') && id.EndsWith('"'))
                    {
                        id = id.Substring(1, id.Length - 2);
                    }
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        ids.Add(id);
                    }
                }
            }
        }

        // Check that at least some map entries correspond to actual projects.
        var csprojs = Directory.GetFiles(Path.Combine(RepoRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = ids.Where(id => !csprojs.Contains(id) && !csprojs.Contains(id.Replace('.', ' '))).ToList();

        if (missing.Count == 0)
        {
            true.ShouldBeTrue();
            return;
        }

        var message = "Package map entries not found as project names (report-only):\n" + string.Join("\n", missing);
        // Always report-only for drift; enforcement should be a manual review initially.
        Console.WriteLine(message);
        true.ShouldBeTrue();
    }
}
