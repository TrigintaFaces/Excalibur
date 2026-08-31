// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Excalibur.Dispatch.Options.Middleware;

namespace Boundary.Tests;

/// <summary>
/// Binds the tenant identifier length accepted at the inbound boundary to the width of the tenant column
/// every shipped artifact declares, so the two cannot drift apart.
/// </summary>
/// <remarks>
/// <para>
/// The bound and the column are enforced in different places by different people: the bound lives on a
/// middleware options type, the column lives in schema scripts, in interpolated auto-provisioning DDL, in
/// the schema blocks published on the documentation site, and in the sample projects consumers copy from.
/// Nothing connects them except the intent that they agree. When they stopped agreeing, an identifier
/// longer than the column but shorter than the bound was accepted at the boundary and then failed — or,
/// on a provider that truncates rather than rejects, silently collided with another tenant — at the store,
/// far from the call that supplied it.
/// </para>
/// <para>
/// This guard is deliberately NOT an assertion that the bound equals a literal. Such an assertion restates
/// a constant against itself and stays green while a schema script drifts to any other width. It parses the
/// width each shipped artifact actually declares and compares it to the bound the options type actually
/// defaults to, so moving either side alone turns it red.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantIdWidthMatchesShippedDdlShould
{
	/// <summary>
	/// A tenant identifier column declaration in any dialect this repository ships: the column name, then a
	/// character type, then the declared width. The bounded gap tolerates alignment padding and a collation
	/// or length qualifier without spanning into the next column of a CREATE TABLE.
	/// </summary>
	private static readonly Regex TenantColumnDeclaration = new(
		@"(?<![A-Za-z0-9_])\[?[Tt]enant_?[Ii]d\]?(?![A-Za-z0-9_])[^,;()]{0,40}?\b(?:N?VARCHAR2?|NVARCHAR|NCHAR|CHAR)\s*\(\s*(?<width>\d+)",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
		TimeSpan.FromSeconds(5));

	/// <summary>
	/// Directories whose content is shipped to, or copied by, a consumer. <c>tests/</c> is excluded on
	/// purpose: a test fixture may provision a deliberately wrong width in order to prove a store rejects it.
	/// </summary>
	private static readonly string[] ShippedRoots = ["src", "samples", "docs-site/docs"];

	private static readonly string[] ShippedExtensions = [".sql", ".cs", ".md"];

	/// <summary>
	/// The floor below which the scan is presumed broken rather than the tree presumed clean. Chosen well
	/// under the population measured when this guard was written, so ordinary churn does not trip it, but
	/// far enough above zero that a regex that stops matching, or a root that stops resolving, fails here
	/// instead of reporting a clean sweep over nothing.
	/// </summary>
	private const int MinimumExpectedDeclarations = 60;

	[Fact]
	public void Bind_TheInboundBound_ToEveryShippedTenantColumnWidth()
	{
		var bound = new TenantIdentityOptions().MaxTenantIdLength;
		var declarations = ScanShippedTenantColumnDeclarations();

		declarations.Count.ShouldBeGreaterThanOrEqualTo(
			MinimumExpectedDeclarations,
			$"only {declarations.Count} tenant column declarations were found across {string.Join(", ", ShippedRoots)}. " +
			"That is below the floor this guard trusts, so the scan is presumed broken rather than the tree presumed " +
			"clean — check the pattern and the roots before treating this as a pass.");

		var divergent = declarations
			.Where(declaration => declaration.Width != bound)
			.OrderBy(declaration => declaration.Location, StringComparer.Ordinal)
			.ToList();

		divergent.ShouldBeEmpty(
			$"TenantIdentityOptions.MaxTenantIdLength accepts {bound} characters, but these shipped tenant columns " +
			"declare a different width. An identifier longer than the narrowest column is accepted at the boundary " +
			"and then fails, or silently truncates and collides, at the store:" +
			Environment.NewLine +
			string.Join(
				Environment.NewLine,
				divergent.Select(declaration => $"  {declaration.Location}: width {declaration.Width} -> {declaration.Line}")));
	}

	/// <summary>
	/// The inbound bound must equal the ceiling the tenant identifier and scope types enforce with no
	/// configuration knob at all. Those types reject a longer identifier outright, so a boundary that
	/// accepted one would advertise an acceptance the framework does not have.
	/// </summary>
	[Fact]
	public void Default_ToTheCeiling_TheTenantIdentifierTypeEnforcesWithoutAKnob() =>
		new TenantIdentityOptions().MaxTenantIdLength.ShouldBe(Excalibur.Dispatch.TenantId.MaxLength);

	private static IReadOnlyList<TenantColumn> ScanShippedTenantColumnDeclarations()
	{
		var repositoryRoot = TestHelpers.GetRepositoryRoot();
		var found = new List<TenantColumn>();

		foreach (var relativeRoot in ShippedRoots)
		{
			var root = Path.Combine(repositoryRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
			if (!Directory.Exists(root))
			{
				throw new InvalidOperationException(
					$"Shipped root '{relativeRoot}' does not exist under {repositoryRoot}. This guard cannot report a " +
					"clean sweep over a root it never read.");
			}

			var files = Directory
				.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
				.Where(path => ShippedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
				.Where(path => !IsGeneratedArtifact(path));

			foreach (var file in files)
			{
				CollectFrom(repositoryRoot, file, found);
			}
		}

		return found;
	}

	private static void CollectFrom(string repositoryRoot, string file, List<TenantColumn> found)
	{
		var lines = File.ReadAllLines(file);

		for (var index = 0; index < lines.Length; index++)
		{
			var line = lines[index];
			var candidate = StripDocCommentPrefix(line);

			// An ordinary comment narrates history — what a column WAS before a migration narrowed it. Those
			// statements are accurate as history and asserting on them would force the tree to lie about its
			// own past. A DOC comment is different: the schema blocks published in XML documentation are how a
			// consumer provisions those tables, so they are load-bearing and are stripped to their content
			// above rather than skipped here.
			if (IsHistoricalCommentary(candidate))
			{
				continue;
			}

			// Prose that mentions a width is not a declaration of one. Requiring the line to be shaped like
			// DDL keeps a sentence such as "a tenant_id declared VARCHAR(255) is wider than the store writes"
			// — which is true, and about a legacy table — out of the population.
			if (!IsDeclarationShaped(candidate))
			{
				continue;
			}

			foreach (var match in TenantColumnDeclaration.Matches(candidate).Cast<Match>())
			{
				found.Add(new TenantColumn(
					Location: $"{Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/')}:{index + 1}",
					Width: int.Parse(match.Groups["width"].Value, System.Globalization.CultureInfo.InvariantCulture),
					Line: line.Trim()));
			}
		}
	}

	/// <summary>
	/// Removes an XML doc-comment prefix so the schema block inside it is read as the DDL it publishes.
	/// </summary>
	private static string StripDocCommentPrefix(string line)
	{
		var trimmed = line.TrimStart();
		return trimmed.StartsWith("///", StringComparison.Ordinal) ? trimmed[3..] : line;
	}

	private static bool IsHistoricalCommentary(string line)
	{
		var trimmed = line.TrimStart();
		return trimmed.StartsWith("--", StringComparison.Ordinal)
			   || trimmed.StartsWith("//", StringComparison.Ordinal)
			   || trimmed.StartsWith('*');
	}

	/// <summary>
	/// True when the line declares a column rather than describing one: a column line inside a CREATE TABLE
	/// begins with the column name, and an alteration names the operation.
	/// </summary>
	private static bool IsDeclarationShaped(string line)
	{
		var trimmed = line.TrimStart().TrimStart('[', '`', '"');

		if (trimmed.StartsWith("tenant", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return line.Contains("ADD COLUMN", StringComparison.OrdinalIgnoreCase)
			   || line.Contains("ALTER COLUMN", StringComparison.OrdinalIgnoreCase)
			   || line.Contains("ADD [", StringComparison.OrdinalIgnoreCase)
			   || line.Contains("ADD (", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsGeneratedArtifact(string path)
	{
		var normalized = path.Replace('\\', '/');
		return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
			   || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
			   || normalized.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase);
	}

	private sealed record TenantColumn(string Location, int Width, string Line);
}
