namespace Boundary.Tests;

/// <summary>
/// Validates that all governed projects are included in the solution file,
/// cross-checking the solution against the project manifest.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class SolutionMembershipTests
{
    private readonly string _repoRoot;

    public SolutionMembershipTests()
    {
        _repoRoot = TestHelpers.GetRepositoryRoot();
    }

    /// <summary>
    /// All shipping projects (src/**/*.csproj) must be registered in the solution file,
    /// unless explicitly excluded in the project manifest.
    /// </summary>
    [Fact]
    public void AllShippingProjects_MustBeInSolution()
    {
        // Arrange
        var slnPath = Path.Combine(_repoRoot, "Excalibur.sln");
        File.Exists(slnPath).ShouldBeTrue("Solution file not found");

        var slnContent = File.ReadAllText(slnPath);
        var srcCsprojs = TestHelpers.GetCsprojFiles(_repoRoot, "src");
        var exclusions = GetManifestExclusions();

        var missing = new List<string>();

        // Act
        foreach (var csproj in srcCsprojs)
        {
            var relativePath = Path.GetRelativePath(_repoRoot, csproj).Replace('\\', '/');
            var projectName = Path.GetFileNameWithoutExtension(csproj);

            // Skip excluded projects
            if (IsExcluded(relativePath, exclusions))
            {
                continue;
            }

            // Check if the project name appears in the solution
            // sln files reference projects by path with backslashes on Windows
            var slnRelativePath = relativePath.Replace('/', '\\');

            if (!slnContent.Contains(projectName, StringComparison.OrdinalIgnoreCase))
            {
                missing.Add(relativePath);
            }
        }

        // Assert
        missing.ShouldBeEmpty(
            "All shipping projects must be in the solution. " +
            $"Missing: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// All unit test projects (tests/**/*.csproj) must be registered in the solution file,
    /// unless explicitly excluded in the project manifest.
    /// </summary>
    [Fact]
    public void AllTestProjects_MustBeInSolution()
    {
        // Arrange
        var slnPath = Path.Combine(_repoRoot, "Excalibur.sln");
        File.Exists(slnPath).ShouldBeTrue("Solution file not found");

        var slnContent = File.ReadAllText(slnPath);
        var testCsprojs = TestHelpers.GetCsprojFiles(_repoRoot, "tests");
        var exclusions = GetManifestExclusions();

        var missing = new List<string>();

        // Act
        foreach (var csproj in testCsprojs)
        {
            var relativePath = Path.GetRelativePath(_repoRoot, csproj).Replace('\\', '/');
            var projectName = Path.GetFileNameWithoutExtension(csproj);

            if (IsExcluded(relativePath, exclusions))
            {
                continue;
            }

            if (!slnContent.Contains(projectName, StringComparison.OrdinalIgnoreCase))
            {
                missing.Add(relativePath);
            }
        }

        // Assert
        missing.ShouldBeEmpty(
            "All test projects must be in the solution. " +
            $"Missing: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// All benchmark projects must be in the solution.
    /// </summary>
    [Fact]
    public void AllBenchmarkProjects_MustBeInSolution()
    {
        // Arrange
        var slnPath = Path.Combine(_repoRoot, "Excalibur.sln");
        File.Exists(slnPath).ShouldBeTrue("Solution file not found");

        var slnContent = File.ReadAllText(slnPath);
        var benchCsprojs = TestHelpers.GetCsprojFiles(_repoRoot, "benchmarks");
        var exclusions = GetManifestExclusions();

        var missing = new List<string>();

        // Act
        foreach (var csproj in benchCsprojs)
        {
            var relativePath = Path.GetRelativePath(_repoRoot, csproj).Replace('\\', '/');
            var projectName = Path.GetFileNameWithoutExtension(csproj);

            if (IsExcluded(relativePath, exclusions))
            {
                continue;
            }

            if (!slnContent.Contains(projectName, StringComparison.OrdinalIgnoreCase))
            {
                missing.Add(relativePath);
            }
        }

        // Assert
        missing.ShouldBeEmpty(
            "All benchmark projects must be in the solution. " +
            $"Missing: {string.Join(", ", missing)}");
    }

    private IReadOnlyList<string> GetManifestExclusions()
    {
        var manifestPath = Path.Combine(_repoRoot, "management", "governance", "project-manifest.yaml");

        if (!File.Exists(manifestPath))
        {
            return Array.Empty<string>();
        }

        // Parse exclusion patterns from manifest YAML
        // Simple line-based parsing for exclusion paths
        var exclusions = new List<string>();
        var lines = File.ReadAllLines(manifestPath);
        var inExclusions = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("exclusions:"))
            {
                inExclusions = true;
                continue;
            }

            if (inExclusions && trimmed.StartsWith("- path:"))
            {
                var path = trimmed["- path:".Length..].Trim().Trim('"');
                exclusions.Add(path);
            }
            else if (inExclusions && !trimmed.StartsWith("reason:", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith('-') && !trimmed.StartsWith('#'))
            {
                inExclusions = false;
            }
        }

        return exclusions;
    }

    private static bool IsExcluded(string relativePath, IReadOnlyList<string> exclusions)
    {
        foreach (var pattern in exclusions)
        {
            var normalizedPattern = pattern.Replace("**", "").TrimEnd('/');

            if (relativePath.StartsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
