// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Excalibur.Dispatch.Tests.CodeQuality;

/// <summary>
/// Holds the tenant context at its floor: no production type may accept an <c>ITenantContext</c> as an
/// optional or nullable parameter.
/// </summary>
/// <remarks>
/// <para>
/// A store that partitions rows by tenant resolves that partition from its <c>ITenantContext</c>. When the
/// parameter is optional, the same store answers the question two different ways depending on whether a
/// context happens to be registered in the container -- and a consumer can flip that state without touching
/// their own code, merely by referencing a package whose registration helper supplies a default. The two
/// answers name different partitions, so rows written in one state stop being visible in the other. Nothing
/// errors; the data simply goes dark.
/// </para>
/// <para>
/// Making the parameter required removes the second answer rather than reconciling it: with no absent state
/// there is no pair of terms left to disagree. This test is what holds that property, because the compiler
/// will not -- re-adding <c>ITenantContext? tenantContext = null</c> to any constructor restores the defect
/// silently and builds clean.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "CodeQuality")]
public sealed class OptionalTenantContextParameterShould
{
	/// <summary>
	/// Matches an <c>ITenantContext</c> PARAMETER declared nullable -- <c>ITenantContext? name</c> followed
	/// by a default, a further parameter, or the close of the parameter list.
	/// </summary>
	/// <remarks>
	/// Deliberately does not match a nullable FIELD (<c>private readonly ITenantContext? _x;</c>), a generic
	/// argument, or a documentation reference. The parameter is the seam that decides the contract; a field
	/// only follows from it.
	/// </remarks>
	private static readonly Regex OptionalParameter = new(
		@"ITenantContext\?\s+\w+\s*(=|,|\))",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>
	/// Production files still permitted to declare an optional <c>ITenantContext</c> parameter.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Empty, and it must stay that way. The last four entries were the compliance stores that resolved
	/// <c>_tenantContext is null ? Untenanted : FromContext(...)</c> unconditionally, so the term they bound
	/// depended on whether a context happened to be registered -- by them or by any unrelated feature. They
	/// now read the deployment mode from <c>TenantContextOptions.RequireTenant</c>, exactly as their erasure
	/// and legal-hold siblings do, and take both the context and the options as required parameters.
	/// </para>
	/// <para>
	/// Do not add to this list -- an entry here is a partition that can still drift.
	/// </para>
	/// </remarks>
	private static readonly string[] CarvedComplianceFiles = [];

	/// <summary>
	/// SAFETY: no production file outside the carved compliance set declares an optional tenant context.
	/// </summary>
	[Fact]
	public void FindNoOptionalTenantContextParameterOutsideTheCarvedComplianceFiles()
	{
		var violations = ScanSource()
			.Where(hit => !CarvedComplianceFiles.Any(carved => hit.RelativePath.EndsWith(carved, StringComparison.Ordinal)))
			.ToList();

		violations.ShouldBeEmpty(
			$"{violations.Count} production parameter(s) accept ITenantContext optionally. A store that can be "
			+ "built without a tenant context resolves one partition when a context is registered and a "
			+ "different one when it is not, so rows written in either state go dark in the other. Make the "
			+ "parameter required (ITenantContext, no default) and have the registering extension call "
			+ "AddDefaultTenantContext() so resolution always succeeds:\n"
			+ string.Join("\n", violations.Select(v => $"  {v.RelativePath}:{v.Line}  {v.Text}")));
	}

	/// <summary>
	/// LIVENESS: the carved compliance files are still exactly the ones listed, so the allowlist shrinks to
	/// nothing rather than quietly outliving the work it records.
	/// </summary>
	/// <remarks>
	/// Asserting the allowlist is not larger than the real population is what stops it becoming a parking
	/// space. An entry that no longer corresponds to a violation is stale and must be deleted -- otherwise a
	/// future file could be added under a carved path and inherit an exemption nobody granted it.
	/// </remarks>
	[Fact]
	public void NotCarryAStaleAllowlistEntry()
	{
		var hits = ScanSource().Select(h => h.RelativePath).ToList();

		var stale = CarvedComplianceFiles
			.Where(carved => !hits.Any(hit => hit.EndsWith(carved, StringComparison.Ordinal)))
			.ToList();

		stale.ShouldBeEmpty(
			"These files are on the carved-compliance allowlist but no longer declare an optional "
			+ "ITenantContext parameter. The work is done -- delete the entries so the allowlist keeps "
			+ "measuring what is left:\n" + string.Join("\n", stale.Select(s => "  " + s)));
	}

	/// <summary>
	/// LIVENESS: the scan reaches the source tree and the detector actually detects.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety test above is satisfied by a scan that reads no files at all -- a wrong
	/// path, a renamed directory, or a regex that can never match all report "no violations" in exactly the
	/// same words as a clean tree. This arm fails loud instead of returning early, and it proves the detector
	/// is non-vacuous by running it against a planted violation.
	/// </remarks>
	[Fact]
	public void ScanTheSourceTreeAndDetectAPlantedViolation()
	{
		var root = FindSourceRoot();
		root.ShouldNotBeNull(
			"The source tree was not found from the test's working directory. Returning early here would "
			+ "make every other arm in this file pass without reading a single line of production code.");

		var scanned = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
			.Count(f => !IsGeneratedOrBuildOutput(f));

		scanned.ShouldBeGreaterThan(
			1000,
			$"Only {scanned} production files were scanned. The source tree is far larger than that, so the "
			+ "scan is not reaching it and a clean result would be meaningless.");

		// The detector must fire on the exact shape this test forbids, in each form it can take.
		OptionalParameter.IsMatch("public Store(ILogger<Store> logger, ITenantContext? tenantContext = null)")
			.ShouldBeTrue("the detector must catch an optional parameter with a null default");
		OptionalParameter.IsMatch("public Store(ITenantContext? tenantContext, ILogger<Store> logger)")
			.ShouldBeTrue("the detector must catch a nullable parameter even without a default");
		OptionalParameter.IsMatch("public Store(ILogger<Store> logger, ITenantContext? tenantContext)")
			.ShouldBeTrue("the detector must catch a nullable parameter closing the list");

		// ...and must not fire on the required form, or on a field, which would make the gate unsatisfiable.
		OptionalParameter.IsMatch("public Store(ILogger<Store> logger, ITenantContext tenantContext)")
			.ShouldBeFalse("a required parameter is the target state and must not be reported");
		OptionalParameter.IsMatch("private readonly ITenantContext? _tenantContext;")
			.ShouldBeFalse("a field declaration is not the parameter seam this gate governs");
	}

	/// <summary>
	/// Matches an <c>IOptions&lt;TenantContextOptions&gt;</c> PARAMETER that carries a DEFAULT, so a caller
	/// may omit it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Two shapes let the deployment mode be selected without anyone choosing it, and this detector forbids
	/// both. A parameter with <c>= null</c> lets a registration that simply forgot the options binding be
	/// indistinguishable from one that deliberately declared single-tenancy, and those two registrations get
	/// different DATA: on SQLite the omitted form drives a convergence UPDATE that rewrites stored tenant
	/// identifiers at schema init.
	/// </para>
	/// <para>
	/// The nullable-without-a-default form was previously PERMITTED here, on the reasoning that a caller who
	/// does not want to supply it must write <c>null</c> -- a statement someone made rather than a value that
	/// arrived by omission. That reasoning held only while nothing downstream folded the null into a mode,
	/// and it did not hold: every one of those constructors read
	/// <c>tenantContextOptions?.Value.RequireTenant ?? false</c>, so writing <c>null</c> silently selected
	/// the mode that applies no tenant predicate. The registration extensions then supplied that null
	/// themselves via <c>GetService</c>, so it arrived on a path nobody chose after all. Both halves are now
	/// gone -- the parameter is required and the extensions use <c>GetRequiredService</c> -- so the nullable
	/// form has no remaining legitimate meaning and is reported.
	/// </para>
	/// <para>
	/// This is the property the compiler enforces once the nullability and the default are gone, and this arm
	/// is what keeps them gone. Re-adding either restores the silent selection and builds clean.
	/// </para>
	/// </remarks>
	/// <remarks>
	/// The leading context is an ALTERNATION with start-of-line, and that is load-bearing rather than
	/// defensive. The scan tests one line at a time, so a parameter declared on its own line -- which is how
	/// every constructor in this population is formatted -- has its comma on the PREVIOUS line. An anchor of
	/// <c>[(,]</c> alone therefore could not match a single real declaration, while the self-test below fed
	/// it single-line strings and passed. The detector was healthy, its self-test was honest, and the scan
	/// returned zero over a tree that had four violations in it: a correct answer to a different question.
	/// The own-line arm in the self-test is what holds this open.
	/// </remarks>
	private static readonly Regex OmissibleDeploymentMode = new(
		@"(?:[(,]|^)\s*IOptions<TenantContextOptions>(?:\?\s+\w+\s*(?:[,)]|=)|\s+\w+\s*=\s*(?:null|default))",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>
	/// SAFETY: no production constructor lets the deployment mode be selected by omitting a parameter.
	/// </summary>
	[Fact]
	public void FindNoOmissibleDeploymentModeParameter()
	{
		var violations = ScanSource(OmissibleDeploymentMode);

		violations.ShouldBeEmpty(
			$"{violations.Count} production parameter(s) let IOptions<TenantContextOptions> be omitted or passed as null. "
			+ "Omitting it selects single-tenant mode, so a registration that forgot the options binding is "
			+ "indistinguishable from one that declared single-tenancy on purpose -- and the two write "
			+ "different tenant terms. Drop the default AND the '?' so both are compile errors, and have the "
			+ "registering extension call AddDefaultTenantContext() so resolution always succeeds:\n"
			+ string.Join("\n", violations.Select(v => $"  {v.RelativePath}:{v.Line}  {v.Text}")));
	}

	/// <summary>
	/// LIVENESS: the deployment-mode detector fires on the shape it forbids and not on the target state.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety test above passes against a regex that can never match -- the same words
	/// as a clean tree. The negative cases matter as much as the positive ones: a detector that also
	/// reported the required form would make the gate unsatisfiable, and the usual response to an
	/// unsatisfiable gate is to delete it.
	/// </remarks>
	[Fact]
	public void DetectAnOmissibleDeploymentModeParameterAndNothingElse()
	{
		OmissibleDeploymentMode.IsMatch(
			"public Store(ITenantContext ctx, IOptions<TenantContextOptions>? tenantContextOptions = null)")
			.ShouldBeTrue("the nullable-with-default form is the exact shape that selects single-tenant silently");
		OmissibleDeploymentMode.IsMatch(
			"public Store(ITenantContext ctx, IOptions<TenantContextOptions> tenantContextOptions = null!)")
			.ShouldBeTrue("a null-forgiving default is the same omission wearing an operator");

		// The shape the real constructors use: one parameter per line, so the comma is on the PREVIOUS line
		// and never reaches this matcher. Every single-line case above passed while this one could not match,
		// which is how the scan reported a clean tree that had four violations in it.
		OmissibleDeploymentMode.IsMatch("		IOptions<TenantContextOptions>? tenantContextOptions)")
			.ShouldBeTrue("a parameter declared on its own line is the shape this population is written in");
		OmissibleDeploymentMode.IsMatch("		IOptions<TenantContextOptions>? tenantContextOptions = null)")
			.ShouldBeTrue("the same, carrying a default");
		OmissibleDeploymentMode.IsMatch("		IOptions<TenantContextOptions>? tenantContextOptions,")
			.ShouldBeTrue("the same, mid-list");
		OmissibleDeploymentMode.IsMatch("		IOptions<TenantContextOptions> tenantContextOptions)")
			.ShouldBeFalse("the required form on its own line is the target state and must not be reported");

		OmissibleDeploymentMode.IsMatch(
			"public Store(ITenantContext ctx, IOptions<TenantContextOptions> tenantContextOptions)")
			.ShouldBeFalse("the required form is the target state and must not be reported");
		OmissibleDeploymentMode.IsMatch(
			"public Store(ITenantContext ctx, IOptions<TenantContextOptions>? tenantContextOptions)")
			.ShouldBeTrue(
				"nullable without a default is now reported too: the store bodies that folded a written null "
				+ "onto single-tenant are gone, so the form has no remaining legitimate meaning");
		OmissibleDeploymentMode.IsMatch("private readonly IOptions<TenantContextOptions>? _options = null;")
			.ShouldBeFalse("a field initializer is not the parameter seam this gate governs");
	}

	private static IReadOnlyList<(string RelativePath, int Line, string Text)> ScanSource() =>
		ScanSource(OptionalParameter);

	private static IReadOnlyList<(string RelativePath, int Line, string Text)> ScanSource(Regex detector)
	{
		var root = FindSourceRoot();
		if (root is null)
		{
			return [];
		}

		var hits = new List<(string, int, string)>();

		foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
		{
			if (IsGeneratedOrBuildOutput(file))
			{
				continue;
			}

			var lines = File.ReadAllLines(file);
			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				var trimmed = line.TrimStart();

				// Documentation and commentary describe the contract; they do not declare it.
				if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*'))
				{
					continue;
				}

				if (detector.IsMatch(line))
				{
					hits.Add((Relative(root, file), i + 1, trimmed));
				}
			}
		}

		return hits;
	}

	private static string Relative(string root, string file) =>
		Path.GetRelativePath(root, file).Replace('\\', '/');

	private static bool IsGeneratedOrBuildOutput(string path)
	{
		var p = path.Replace('\\', '/');
		return p.Contains("/bin/", StringComparison.Ordinal)
			|| p.Contains("/obj/", StringComparison.Ordinal)
			|| p.EndsWith(".g.cs", StringComparison.Ordinal)
			|| p.EndsWith(".Designer.cs", StringComparison.Ordinal);
	}

	private static string? FindSourceRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);

		while (dir is not null)
		{
			var candidate = Path.Combine(dir.FullName, "src");
			if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(candidate, "Dispatch")))
			{
				return candidate;
			}

			dir = dir.Parent;
		}

		return null;
	}
}
