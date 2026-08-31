namespace Boundary.Tests;

/// <summary>
/// CI/CD-sync backstop for the EPIC w2zq7d migration-tooling projects
/// (spec FR-17 / AC-12; <c>cicd-sync</c> rule).
///
/// Every migration-tooling project — discovered by the <c>Excalibur.Dispatch.Compat.*</c> and
/// <c>Excalibur.Dispatch.Migration.*</c> naming convention under <c>src/</c> — must be tracked in the
/// build-target lists. <b>The required list depends on how the project SHIPS</b>:
/// <list type="bullet">
///   <item>
///     <b>Packable</b> projects (a real NuGet of their own) MUST be in the solution, the governance
///     manifest, <b>and</b> the CI pack/publish filter (<c>ShippingOnly.slnf</c>).
///   </item>
///   <item>
///     <b>Non-packable analyzer/source-generator</b> projects (<c>&lt;IsPackable&gt;false&lt;/IsPackable&gt;</c>)
///     ship as an <i>analyzer asset</i> packed <i>inside</i> a sibling package (ADR-341 §9 Option B2:
///     the compat generator's DLL is packed into <c>Excalibur.Dispatch.Compat.MediatR.nupkg</c> via the
///     analyzer <c>ProjectReference</c> + <c>Pack="true" PackagePath="analyzers/dotnet/cs"</c> None-glob).
///     They are built/tracked (solution + manifest) but MUST NOT be in <c>ShippingOnly.slnf</c> — the
///     shipping-filter guard treats slnf entries as packable, so listing a non-packable project there is
///     a violation. slnf membership was never the ship mechanism for them.
///   </item>
/// </list>
/// Pattern-based (not a fixed list) so any future compat/migration project is covered automatically.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class MigrationToolingCiSyncTests
{
    /// <summary>The compat source-generator — ships as an analyzer asset (IsPackable=false), not a NuGet.</summary>
    private const string GeneratorProjectName = "Excalibur.Dispatch.Compat.MediatR.SourceGenerators";

    private readonly string _repoRoot = TestHelpers.GetRepositoryRoot();

    private sealed record ToolingProject(string Name, string CsprojPath);

    /// <summary>
    /// Discovers ALL migration-tooling build targets (packable + non-packable) by naming convention
    /// under src/, returning each project's name and .csproj path.
    /// </summary>
    private IReadOnlyList<ToolingProject> DiscoverMigrationToolingProjects()
    {
        return TestHelpers.GetCsprojFiles(_repoRoot, "src")
            .Select(path => new ToolingProject(Path.GetFileNameWithoutExtension(path) ?? string.Empty, path))
            .Where(p =>
                p.Name.StartsWith("Excalibur.Dispatch.Compat.", StringComparison.Ordinal)
                || p.Name.StartsWith("Excalibur.Dispatch.Migration.", StringComparison.Ordinal)
                // The trailing dot above matches sub-projects and nothing else, so the package that
                // actually SHIPS the migration tooling -- named without one -- was invisible here.
                // Everything this file asserts is enumeration: in the solution, in the manifest, in
                // the pack filter. A build target the enumeration cannot see is a build target none
                // of it protects, and this one is the shipping package.
                || string.Equals(p.Name, "Excalibur.Dispatch.Migration", StringComparison.Ordinal))
            .DistinctBy(p => p.Name, StringComparer.Ordinal)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// A project is packable unless it declares <c>&lt;IsPackable&gt;false&lt;/IsPackable&gt;</c> (MSBuild default is true).
    /// </summary>
    private static bool IsPackable(string csprojPath)
    {
        var value = XDocument.Load(csprojPath)
            .Descendants("IsPackable")
            .Select(e => e.Value)
            .FirstOrDefault();

        return !string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MigrationToolingProjects_Exist_NonVacuousGuard()
    {
        // Guards against the pattern silently matching nothing (which would make every
        // enumeration assertion below pass vacuously).
        var projects = DiscoverMigrationToolingProjects();

        // SIX, not five, and the difference is the point. The floor was five against a population of
        // exactly five, so it could not fail: a project disappearing left the count on the boundary
        // and every enumeration below still passed, over a smaller set. A floor calibrated to the
        // population is a floor that cannot fire.
        projects.Count.ShouldBeGreaterThanOrEqualTo(6,
            "Expected the six known migration-tooling build targets (Compat.MediatR, "
            + "Compat.MediatR.SourceGenerators, Compat.MassTransit, Migration, Migration.Analyzers, "
            + "Migration.CodeFixes). Fewer means the discovery pattern has stopped matching something, "
            + "and every assertion in this file would then pass over whatever is left. "
            + $"Discovered: {string.Join(", ", projects.Select(p => p.Name))}");
    }

    [Fact]
    public void AllMigrationToolingProjects_MustBeIn_Solution()
    {
        // Solution tracks ALL build targets (packable + the non-packable generator).
        AssertEnumerated(
            names: DiscoverMigrationToolingProjects().Select(p => p.Name).ToList(),
            relativeFile: "Excalibur.sln",
            siteDescription: "the solution (Excalibur.sln)");
    }

    [Fact]
    public void AllMigrationToolingProjects_MustBeIn_GovernanceManifest()
    {
        // Governance tracks ALL build targets (packable + the non-packable generator).
        AssertEnumerated(
            names: DiscoverMigrationToolingProjects().Select(p => p.Name).ToList(),
            relativeFile: "eng/governance/project-manifest.yaml",
            siteDescription: "the governance manifest (project-manifest.yaml)");
    }

    /// <summary>
    /// Every non-packable analyzer assembly must be carried by exactly one package.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule is already written down, in a comment beside one of the bundling sites: ONE delivery
    /// path, because a second means a consumer receives the same diagnostic twice. The other half is
    /// worse and unwritten -- zero delivery paths. A project marked non-packable that nothing bundles
    /// does not fail to build, does not fail to pack, and does not fail any test. It simply stops
    /// reaching consumers, and the analyzer it contains silently never runs again.
    /// </para>
    /// <para>
    /// That is the shape this repository keeps finding by other means: something advertised, wired to
    /// nothing, behind a passing build. Making a project non-packable is one line, and the line that
    /// carries its output into a package lives in a different file.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryNonPackableAnalyzer_IsCarriedBy_ExactlyOnePackage()
    {
        var all = DiscoverMigrationToolingProjects();
        var nonPackable = all.Where(p => !IsPackable(p.CsprojPath)).ToList();

        nonPackable.ShouldNotBeEmpty(
            "No non-packable migration-tooling project was found, so this test is asserting nothing. "
            + "Either the convention changed or the discovery pattern has stopped matching.");

        var allCsproj = TestHelpers.GetCsprojFiles(_repoRoot, "src").ToList();

        foreach (var orphan in nonPackable)
        {
            // A carrier names the assembly in a Pack=true item. Searching the raw text rather than the
            // XML because the reference is a path, assembled from properties, and the point is only
            // whether some packable project mentions this assembly on a packing item at all.
            var carriers = allCsproj
                .Where(c => !string.Equals(
                    Path.GetFileNameWithoutExtension(c), orphan.Name, StringComparison.Ordinal))
                .Where(c => IsPackable(c))
                .Where(c =>
                {
                    var text = File.ReadAllText(c);
                    return text.Contains(orphan.Name + ".dll", StringComparison.Ordinal)
                        && text.Contains("PackagePath=\"analyzers/dotnet/cs\"", StringComparison.Ordinal);
                })
                .Select(c => Path.GetFileNameWithoutExtension(c))
                .ToList();

            carriers.Count.ShouldBe(
                1,
                $"{orphan.Name} is IsPackable=false and is carried by {carriers.Count} package(s): "
                + $"[{string.Join(", ", carriers)}]. Zero means the assembly ships nowhere -- it builds, "
                + "it packs nothing, no test fails, and the analyzer inside it simply stops reaching "
                + "consumers. More than one means a consumer receives the same diagnostic twice. "
                + "Exactly one package must carry it.");
        }
    }

    [Fact]
    public void PackablePackages_MustBeIn_ShippingPackFilter_NonPackableGeneratorExcluded()
    {
        var all = DiscoverMigrationToolingProjects();

        // Non-vacuity anchor: the generator IS discovered as a build target, and it IS non-packable.
        var generator = all.FirstOrDefault(p => p.Name == GeneratorProjectName);
        generator.ShouldNotBeNull($"{GeneratorProjectName} must be discovered as a migration-tooling build target.");
        IsPackable(generator!.CsprojPath).ShouldBeFalse(
            $"{GeneratorProjectName} must be <IsPackable>false</IsPackable> — it ships as an analyzer asset "
            + "inside Compat.MediatR (ADR-341 §9 B2), not as its own NuGet package.");

        var packable = all.Where(p => IsPackable(p.CsprojPath)).Select(p => p.Name).ToList();
        packable.ShouldNotBeEmpty("Expected at least one packable migration-tooling package.");
        packable.ShouldNotContain(GeneratorProjectName,
            "The non-packable generator must be excluded from the packable shipping set.");

        var slnfPath = Path.Combine(_repoRoot, "eng", "ci", "shards", "ShippingOnly.slnf");
        File.Exists(slnfPath).ShouldBeTrue("ShippingOnly.slnf not found.");
        var slnf = File.ReadAllText(slnfPath);

        // Every PACKABLE migration package must be in the CI pack/publish filter.
        var missing = packable.Where(n => !slnf.Contains(n, StringComparison.OrdinalIgnoreCase)).ToList();
        missing.ShouldBeEmpty(
            "FR-17 / AC-12 CI-SYNC VIOLATION: packable migration package(s) NOT in the CI pack/publish "
            + $"filter (ShippingOnly.slnf) — they would be silently skipped by pack/publish. Missing: "
            + string.Join(", ", missing));

        // The NON-PACKABLE generator must NOT be in the slnf: the shipping-filter guard treats slnf
        // entries as packable, so listing an IsPackable=false project there is itself a violation.
        slnf.Contains(GeneratorProjectName, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
            $"{GeneratorProjectName} (IsPackable=false) must NOT be listed in ShippingOnly.slnf — it ships "
            + "via the analyzer Pack-glob inside Compat.MediatR, and the shipping-filter guard rejects "
            + "non-packable entries. slnf membership was never its ship mechanism.");
    }

    [Fact]
    public void Generator_ShipsAsAnalyzerAsset_InsideCompatMediatRPackage()
    {
        // Positive assertion of the REAL ship mechanism (ground truth, not a proxy): the generator
        // reaches consumers because Compat.MediatR references it as an analyzer AND packs its DLL into
        // the NuGet under analyzers/dotnet/cs (ADR-341 §9 Option B2).
        var compatCsproj = Path.Combine(
            _repoRoot, "src", "Dispatch", "Excalibur.Dispatch.Compat.MediatR",
            "Excalibur.Dispatch.Compat.MediatR.csproj");
        File.Exists(compatCsproj).ShouldBeTrue("Compat.MediatR.csproj not found.");

        var doc = XDocument.Load(compatCsproj);

        var analyzerRef = doc.Descendants("ProjectReference")
            .Any(e =>
                (e.Attribute("Include")?.Value ?? string.Empty).Contains(GeneratorProjectName, StringComparison.Ordinal)
                && string.Equals(e.Attribute("OutputItemType")?.Value, "Analyzer", StringComparison.OrdinalIgnoreCase));
        analyzerRef.ShouldBeTrue(
            $"Compat.MediatR.csproj must reference {GeneratorProjectName} with OutputItemType=\"Analyzer\" "
            + "so the generator runs and is packable as an analyzer asset.");

        var packsAnalyzerDll = doc.Descendants("None")
            .Any(e =>
                (e.Attribute("Include")?.Value ?? string.Empty).Contains(GeneratorProjectName, StringComparison.Ordinal)
                && string.Equals(e.Attribute("Pack")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                && (e.Attribute("PackagePath")?.Value ?? string.Empty).Replace('\\', '/')
                    .Contains("analyzers/dotnet/cs", StringComparison.OrdinalIgnoreCase));
        packsAnalyzerDll.ShouldBeTrue(
            $"Compat.MediatR.csproj must pack the {GeneratorProjectName} DLL via a None item with "
            + "Pack=\"true\" PackagePath=\"analyzers/dotnet/cs\" — this is the generator's ship mechanism "
            + "(ADR-341 §9 B2), which is why it is intentionally absent from ShippingOnly.slnf.");
    }

    private void AssertEnumerated(IReadOnlyList<string> names, string relativeFile, string siteDescription)
    {
        var fullPath = Path.Combine(_repoRoot, relativeFile.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).ShouldBeTrue($"Enumeration file not found: {relativeFile}");

        var content = File.ReadAllText(fullPath);

        var missing = names
            .Where(name => !content.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        missing.ShouldBeEmpty(
            $"FR-17 / AC-12 CI-SYNC VIOLATION: the following migration-tooling project(s) are NOT "
            + $"tracked in {siteDescription}. An un-enumerated build target is silently skipped by CI "
            + "build or governance. Add it (cicd-sync, same change as project creation). "
            + $"Missing: {string.Join(", ", missing)}");
    }
}
