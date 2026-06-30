namespace Boundary.Tests;

/// <summary>
/// Enforces API isolation for the MediatR compatibility surface
/// (EPIC w2zq7d; ADR-341 §1/§3; spec FR-9 / NFR-4).
///
/// The compat shim (<c>Excalibur.Dispatch.Compat.MediatR</c>) is an <b>additive, isolated</b> package:
/// it depends one-way on canonical <c>Excalibur.Dispatch</c> and MUST NOT
/// <list type="number">
///   <item>leak any type into the canonical <c>Excalibur.Dispatch</c> /
///   <c>Excalibur.Dispatch.Abstractions</c> public-API baselines, nor</item>
///   <item>be referenced by the canonical packages (one-way dependency direction).</item>
/// </list>
/// These are file-based structural guards (no assembly loading required) so they hold even before
/// the compat package is added to the solution.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class CompatSurfaceIsolationTests
{
    /// <summary>The compat namespace prefix that must never appear in a canonical baseline.</summary>
    private const string CompatNamespacePrefix = "Excalibur.Dispatch.Compat";

    /// <summary>The compat package/assembly name the canonical projects must never reference.</summary>
    private const string CompatPackageName = "Excalibur.Dispatch.Compat.MediatR";

    /// <summary>The incumbent trademark token; no canonical public symbol should ever carry it.</summary>
    private const string IncumbentToken = "MediatR";

    private static readonly string[] CanonicalBaselineFiles =
    [
        "src/Dispatch/Excalibur.Dispatch/PublicAPI.Shipped.txt",
        "src/Dispatch/Excalibur.Dispatch/PublicAPI.Unshipped.txt",
        "src/Dispatch/Excalibur.Dispatch.Abstractions/PublicAPI.Shipped.txt",
        "src/Dispatch/Excalibur.Dispatch.Abstractions/PublicAPI.Unshipped.txt",
    ];

    private static readonly string[] CanonicalCsprojFiles =
    [
        "src/Dispatch/Excalibur.Dispatch/Excalibur.Dispatch.csproj",
        "src/Dispatch/Excalibur.Dispatch.Abstractions/Excalibur.Dispatch.Abstractions.csproj",
    ];

    private readonly string _repoRoot = TestHelpers.GetRepositoryRoot();

    /// <summary>
    /// FR-9 / NFR-4: the compat surface MUST NOT leak into the canonical PublicAPI baselines.
    /// Asserts no canonical baseline line references the compat namespace or the incumbent token.
    /// </summary>
    [Fact]
    public void CanonicalPublicApiBaselines_MustNotLeak_CompatSurface()
    {
        var leaks = new List<string>();

        foreach (var relativePath in CanonicalBaselineFiles)
        {
            var fullPath = Path.Combine(_repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(fullPath).ShouldBeTrue($"Canonical PublicAPI baseline not found: {relativePath}");

            var lines = File.ReadAllLines(fullPath);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.Contains(CompatNamespacePrefix, StringComparison.Ordinal)
                    || line.Contains(IncumbentToken, StringComparison.Ordinal))
                {
                    leaks.Add($"{relativePath}:{i + 1}: {line.Trim()}");
                }
            }
        }

        leaks.ShouldBeEmpty(
            "FR-9 / NFR-4 VIOLATION: the MediatR compat surface leaked into the canonical "
            + "Excalibur.Dispatch public-API baselines. Compat types MUST live only in the "
            + $"'{CompatPackageName}' package and carry their own baseline; the canonical baselines "
            + "must remain unchanged by the compat work. "
            + $"Leak(s):{Environment.NewLine}{string.Join(Environment.NewLine, leaks)}");
    }

    /// <summary>
    /// ADR-341 §1: the dependency direction is compat -> canonical, never the reverse.
    /// Asserts the canonical Dispatch projects do not reference the compat package.
    /// </summary>
    [Fact]
    public void CanonicalDispatchProjects_MustNotReference_CompatPackage()
    {
        var violations = new List<string>();

        foreach (var relativePath in CanonicalCsprojFiles)
        {
            var fullPath = Path.Combine(_repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(fullPath).ShouldBeTrue($"Canonical project not found: {relativePath}");

            var content = File.ReadAllText(fullPath);
            if (content.Contains(CompatPackageName, StringComparison.Ordinal))
            {
                violations.Add(relativePath);
            }
        }

        violations.ShouldBeEmpty(
            "ADR-341 §1 VIOLATION (one-way dependency): canonical Excalibur.Dispatch packages MUST NOT "
            + $"reference the compat package '{CompatPackageName}'. The dependency direction is "
            + "compat -> canonical, never the reverse. "
            + $"Offending project(s): {string.Join(", ", violations)}");
    }
}
