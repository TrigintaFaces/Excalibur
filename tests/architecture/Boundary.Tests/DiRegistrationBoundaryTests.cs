namespace Boundary.Tests;

/// <summary>
/// Validates DI registration boundaries at the csproj level.
/// Ensures Dispatch DI extension classes don't register Excalibur services,
/// and that the Dispatch project doesn't depend on Excalibur assemblies.
///
/// Note: Runtime DI boundary tests (calling AddDispatchPipeline() and inspecting
/// the service collection) exist in tests/ArchitectureTests/ using NetArchTest.
/// These tests complement those by validating at the source/project reference level.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class DiRegistrationBoundaryTests
{
    private readonly string _repoRoot;

    public DiRegistrationBoundaryTests()
    {
        _repoRoot = TestHelpers.GetRepositoryRoot();
    }

    /// <summary>
    /// Dispatch DI extension files must not contain "using Excalibur" statements.
    /// If the DI registration code references Excalibur namespaces, it means
    /// Dispatch is registering Excalibur services, violating the boundary.
    /// </summary>
    [Fact]
    public void DispatchDiExtensions_MustNotReference_ExcaliburNamespaces()
    {
        // Arrange — find all DI extension files in Dispatch
        var dispatchDir = Path.Combine(_repoRoot, "src", "Dispatch");
        var extensionFiles = Directory.GetFiles(dispatchDir, "*ServiceCollectionExtensions.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(dispatchDir, "*ServiceCollectionExtensions.cs", SearchOption.AllDirectories))
            .Distinct()
            .ToList();

        extensionFiles.ShouldNotBeEmpty(
            "No DI extension files found in src/Dispatch/");

        var violations = new List<string>();

        // Act
        foreach (var file in extensionFiles)
        {
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Check for using directives that reference Excalibur application-framework namespaces
                // (Excalibur.Domain, Excalibur.EventSourcing, etc.) but allow Excalibur.Dispatch.*
                if (line.StartsWith("using Excalibur", StringComparison.Ordinal) &&
                    !line.StartsWith("using Excalibur.Dispatch", StringComparison.Ordinal) &&
                    !line.StartsWith("//", StringComparison.Ordinal))
                {
                    var relativePath = Path.GetRelativePath(_repoRoot, file).Replace('\\', '/');
                    violations.Add($"{relativePath}:{i + 1} — {line}");
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty(
            "Dispatch DI extension files must not reference Excalibur namespaces. " +
            "AddDispatch*() methods should only register Dispatch services. " +
            $"Violations: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Dispatch DI extension files must not register types from Excalibur assemblies.
    /// Scans for service registration patterns (AddSingleton, AddScoped, etc.) that
    /// reference Excalibur types.
    /// </summary>
    [Fact]
    public void DispatchDiExtensions_MustNotRegister_ExcaliburTypes()
    {
        // Arrange
        var dispatchDir = Path.Combine(_repoRoot, "src", "Dispatch");
        var extensionFiles = Directory.GetFiles(dispatchDir, "*ServiceCollectionExtensions.cs", SearchOption.AllDirectories)
            .ToList();

        var violations = new List<string>();

        // DI registration patterns to scan for
        var registrationPatterns = new[]
        {
            "AddSingleton<Excalibur",
            "AddScoped<Excalibur",
            "AddTransient<Excalibur",
            "TryAddSingleton<Excalibur",
            "TryAddScoped<Excalibur",
            "TryAddTransient<Excalibur",
            "AddSingleton(typeof(Excalibur",
            "AddScoped(typeof(Excalibur",
            "AddTransient(typeof(Excalibur",
        };

        // Act
        foreach (var file in extensionFiles)
        {
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                foreach (var pattern in registrationPatterns)
                {
                    if (line.Contains(pattern, StringComparison.Ordinal) &&
                        !line.Contains("Excalibur.Dispatch", StringComparison.Ordinal))
                    {
                        var relativePath = Path.GetRelativePath(_repoRoot, file).Replace('\\', '/');
                        violations.Add($"{relativePath}:{i + 1} — {line}");
                    }
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty(
            "Dispatch DI extensions must not register Excalibur types. " +
            "Event sourcing, saga, and outbox services belong in Excalibur DI extensions. " +
            $"Violations: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// The core Excalibur.Dispatch.csproj must not have PackageReference to any Excalibur package.
    /// This prevents accidental NuGet-level coupling.
    /// </summary>
    [Fact]
    public void DispatchCsproj_MustNotHave_ExcaliburPackageReferences()
    {
        // Arrange
        var dispatchCsprojs = TestHelpers.GetCsprojFiles(_repoRoot, Path.Combine("src", "Dispatch"));
        var violations = new List<string>();

        // Act
        foreach (var csproj in dispatchCsprojs)
        {
            var doc = XDocument.Load(csproj);
            var excaliburPackageRefs = doc.Descendants("PackageReference")
                .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
                .Where(v => v.StartsWith("Excalibur", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (excaliburPackageRefs.Count > 0)
            {
                var projectName = Path.GetFileNameWithoutExtension(csproj);
                violations.Add($"{projectName}: {string.Join(", ", excaliburPackageRefs)}");
            }
        }

        // Assert
        violations.ShouldBeEmpty(
            "Dispatch projects must not have PackageReference to Excalibur packages. " +
            $"Violations: {string.Join("; ", violations)}");
    }
}
