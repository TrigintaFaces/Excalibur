// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Boundary.Tests;

/// <summary>
/// Every packable <c>src/</c> project that carries SQL schema scripts packs them, so a consumer who
/// installs the package can obtain them.
/// </summary>
/// <remarks>
/// <para>
/// The consumer this protects is the one whose schema is owned centrally -- a DBA, a migration tool, a
/// reviewed change set -- and who therefore never reaches the runtime table initializer. For that
/// deployment the shipped script is the <b>only</b> remedy, and several subsystem guarantees name one
/// explicitly. A script that exists in the repository but not in the package is a remedy the reader it
/// was written for cannot reach, which is worse than a known gap: the reader stops looking.
/// </para>
/// <para>
/// This binds the project's <b>declared</b> packing, which is the part that can regress on an ordinary
/// edit. It does not open the produced <c>.nupkg</c> -- packing every project is a CI-scale operation,
/// not a unit-test one. The produced artifact is checked when a packaging change lands, and the two
/// together are the evidence: the declaration here, the artifact at the change.
/// </para>
/// <para>
/// Keyed on the <b>files</b>, not on a folder name. Most projects keep their scripts in <c>Scripts/</c>,
/// but not all do, and a check keyed on the folder would silently skip every project that chose another
/// name -- the population it reports would be one naming convention's, not the tree's.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ShippedSchemaScriptsArePackagedShould
{
	/// <summary>
	/// The number of packable projects carrying SQL when this arm was written. A floor, not an exact
	/// count: projects may be added. It exists so that a discovery which silently matches nothing -- a
	/// moved directory, a changed extension -- fails here instead of passing every assertion below.
	/// </summary>
	private const int KnownPopulationFloor = 28;

	private readonly string _repoRoot = TestHelpers.GetRepositoryRoot();

	[Fact]
	public void DiscoverTheScriptCarryingPopulation_NotAnEmptySet()
	{
		var population = DiscoverScriptCarryingProjects();

		population.Count.ShouldBeGreaterThanOrEqualTo(
			KnownPopulationFloor,
			"discovery found fewer SQL-carrying projects than already exist, so the packing assertion below "
				+ "would pass over a population it cannot see");
	}

	[Fact]
	public void PackEverySchemaScriptTheProjectCarries()
	{
		var unpacked = DiscoverScriptCarryingProjects()
			.Where(static project => !DeclaresPackedSql(project.CsprojPath))
			.Select(static project => project.Name)
			.OrderBy(static name => name, StringComparer.Ordinal)
			.ToList();

		unpacked.ShouldBeEmpty(
			"these packages ship without the SQL scripts that sit beside their source, so a consumer whose "
				+ "schema is managed centrally cannot obtain the DDL: " + string.Join(", ", unpacked));
	}

	private sealed record ScriptCarryingProject(string Name, string CsprojPath);

	private List<ScriptCarryingProject> DiscoverScriptCarryingProjects() =>
		[.. TestHelpers.GetCsprojFiles(_repoRoot, "src")
			.Where(IsPackable)
			.Where(static path => CarriesSql(Path.GetDirectoryName(path)!))
			.Select(static path => new ScriptCarryingProject(Path.GetFileNameWithoutExtension(path), path))];

	private static bool CarriesSql(string projectDirectory) =>
		Directory.EnumerateFiles(projectDirectory, "*.sql", SearchOption.AllDirectories)
			.Any(static file => !IsBuildOutput(file));

	private static bool IsBuildOutput(string file)
	{
		var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
		var segments = file.Split(separators, StringSplitOptions.RemoveEmptyEntries);
		return segments.Contains("bin", StringComparer.OrdinalIgnoreCase)
			|| segments.Contains("obj", StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// True when the project declares an item that packs <c>.sql</c> content. Reads the MSBuild XML rather
	/// than matching text, so an attribute order, a quote style or a line break cannot hide a declaration.
	/// </summary>
	private static bool DeclaresPackedSql(string csprojPath) =>
		XDocument.Load(csprojPath)
			.Descendants()
			.Where(static element => element.Name.LocalName is "None" or "Content")
			.Any(static element =>
				string.Equals((string?)element.Attribute("Pack"), "true", StringComparison.OrdinalIgnoreCase)
				&& ((string?)element.Attribute("Include") ?? string.Empty)
					.EndsWith(".sql", StringComparison.OrdinalIgnoreCase));

	private static bool IsPackable(string csprojPath)
	{
		var value = XDocument.Load(csprojPath)
			.Descendants()
			.Where(static element => element.Name.LocalName == "IsPackable")
			.Select(static element => element.Value)
			.FirstOrDefault();

		return !string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);
	}
}
