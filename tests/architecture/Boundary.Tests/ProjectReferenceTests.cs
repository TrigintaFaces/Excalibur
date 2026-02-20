namespace Boundary.Tests;

/// <summary>
/// Validates csproj-level project reference boundaries.
/// Uses System.Xml.Linq to parse .csproj files and assert that
/// Dispatch projects do not reference Excalibur projects (and vice versa where required).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class ProjectReferenceTests
{
    private readonly string _repoRoot;

    public ProjectReferenceTests()
    {
        _repoRoot = TestHelpers.GetRepositoryRoot();
    }

    /// <summary>
    /// CRITICAL BOUNDARY: No src/Dispatch/**/*.csproj may contain a ProjectReference
    /// to any Excalibur.* project. Dispatch is the lower-level messaging framework
    /// and must not depend on the higher-level application framework.
    /// </summary>
    [Fact]
    public void DispatchProjects_MustNotReference_ExcaliburProjects()
    {
        // Arrange
        var dispatchCsprojs = TestHelpers.GetCsprojFiles(_repoRoot, Path.Combine("src", "Dispatch"));
        dispatchCsprojs.ShouldNotBeEmpty("No Dispatch .csproj files found under src/Dispatch/");

        var violations = new List<string>();

        // Act
        foreach (var csproj in dispatchCsprojs)
        {
            var refs = TestHelpers.GetProjectReferences(csproj);
            var excaliburRefs = refs
                .Where(r =>
                {
                    var name = TestHelpers.ExtractProjectName(r);
                    // Flag references to Excalibur application-framework projects,
                    // but allow Excalibur.Dispatch.* (same framework after rename)
                    return name.StartsWith("Excalibur", StringComparison.OrdinalIgnoreCase) &&
                           !name.StartsWith("Excalibur.Dispatch", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (excaliburRefs.Count > 0)
            {
                var projectName = Path.GetFileNameWithoutExtension(csproj);
                var refNames = string.Join(", ", excaliburRefs.Select(TestHelpers.ExtractProjectName));

                violations.Add($"{projectName} -> {refNames}");
            }
        }

        // Assert — hard fail on any Dispatch -> Excalibur reference
        // S507 resolved all known violations (saga code moved to Excalibur)
        violations.ShouldBeEmpty(
            "Dispatch projects must not reference Excalibur projects. " +
            $"Violations ({violations.Count}): {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Excalibur.Dispatch.Abstractions must not reference the concrete Dispatch project.
    /// Abstractions define contracts; implementations depend on contracts, not vice versa.
    /// </summary>
    [Fact]
    public void DispatchAbstractions_MustNotReference_DispatchCore()
    {
        // Arrange
        var abstractionsCsproj = Path.Combine(
            _repoRoot, "src", "Dispatch", "Excalibur.Dispatch.Abstractions", "Excalibur.Dispatch.Abstractions.csproj");

        File.Exists(abstractionsCsproj).ShouldBeTrue(
            "Excalibur.Dispatch.Abstractions.csproj not found at expected path");

        // Act
        var refs = TestHelpers.GetProjectReferences(abstractionsCsproj);
        var dispatchCoreRefs = refs
            .Where(r =>
            {
                var name = TestHelpers.ExtractProjectName(r);
                return name.Equals("Excalibur.Dispatch", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        // Assert
        dispatchCoreRefs.ShouldBeEmpty(
            "Excalibur.Dispatch.Abstractions must not reference Dispatch (core). " +
            "Abstractions are pure contracts with no implementation dependency.");
    }

    /// <summary>
    /// Excalibur.Domain must not reference concrete Excalibur projects.
    /// It MAY reference Excalibur.Dispatch.Abstractions (for IDomainEvent), but must not reference
    /// Excalibur.Dispatch (core), Excalibur.Dispatch.Patterns, or any transport/hosting package.
    /// </summary>
    [Fact]
    public void ExcaliburDomain_MustNotReference_ConcreteDispatchProjects()
    {
        // Arrange
        var domainDir = Path.Combine(_repoRoot, "src", "Excalibur");
        var domainCsprojs = Directory.GetFiles(domainDir, "*.csproj", SearchOption.AllDirectories)
            .Where(c => Path.GetFileNameWithoutExtension(c).Equals("Excalibur.Domain", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (domainCsprojs.Count == 0)
        {
            // Domain project not found — skip
            return;
        }

        // Excalibur.Dispatch.Abstractions is ALLOWED (provides IDomainEvent, IIntegrationEvent)
        var allowedDispatchProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Excalibur.Dispatch.Abstractions",
        };

        // Act & Assert
        foreach (var csproj in domainCsprojs)
        {
            var refs = TestHelpers.GetProjectReferences(csproj);
            var forbiddenRefs = refs
                .Where(r =>
                {
                    var name = TestHelpers.ExtractProjectName(r);
                    return name.StartsWith("Excalibur.Dispatch", StringComparison.OrdinalIgnoreCase) &&
                           !allowedDispatchProjects.Contains(name);
                })
                .ToList();

            forbiddenRefs.ShouldBeEmpty(
                "Excalibur.Domain must not reference concrete Dispatch projects (only Excalibur.Dispatch.Abstractions is allowed). " +
                $"Found: {string.Join(", ", forbiddenRefs.Select(TestHelpers.ExtractProjectName))}");
        }
    }

    /// <summary>
    /// Shipping projects (src/**) must not reference test projects (tests/**).
    /// Production code should never depend on test infrastructure.
    /// </summary>
    [Fact]
    public void ShippingProjects_MustNotReference_TestProjects()
    {
        // Arrange
        var srcCsprojs = TestHelpers.GetCsprojFiles(_repoRoot, "src");
        srcCsprojs.ShouldNotBeEmpty("No .csproj files found under src/");

        var violations = new List<string>();

        // Act
        foreach (var csproj in srcCsprojs)
        {
            var refs = TestHelpers.GetProjectReferences(csproj);

            foreach (var refPath in refs)
            {
                // Normalize the reference path and check if it points to tests/
                var normalizedRef = refPath.Replace('\\', '/');

                if (normalizedRef.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
                    normalizedRef.Contains("Tests.Shared", StringComparison.OrdinalIgnoreCase) ||
                    TestHelpers.ExtractProjectName(refPath).EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
                {
                    var projectName = Path.GetFileNameWithoutExtension(csproj);
                    var refName = TestHelpers.ExtractProjectName(refPath);
                    violations.Add($"{projectName} -> {refName}");
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty(
            "Shipping projects (src/**) must not reference test projects. " +
            $"Violations: {string.Join("; ", violations)}");
    }
}
