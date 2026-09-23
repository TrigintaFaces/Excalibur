// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// Structural guard for the SoftwareArchitect ruling on Excalibur_Dispatch-sln5bq: the data-processing task
/// queue is a global, type-wide sweep with no tenant dimension because there is no API by which tenant
/// ownership could enter it -- not because nobody got around to adding one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a structural arm rather than a behavioural one.</b> There is no framework behaviour to
/// break today: the five <c>[NoTenantTerm(TenantConfinement.NoTenantDimension, ...)]</c> requests in
/// <c>Excalibur.Data.DataProcessing</c> are correct as written, and a behavioural test proving a queue with
/// no tenant column cannot filter by tenant would be proving a tautology. What can go stale silently is the
/// PREMISE those attributes rest on -- that the enqueue surface never accepts a tenant identity. The day
/// someone adds one, the disposition, its five reason strings and the README consumer obligation all need
/// to be revisited together, and this arm is what forces that instead of letting the attributes quietly
/// outlive the fact they assert (enforce-invariants-structurally).
/// </para>
/// <para>
/// <b>Two independent checks, because either alone is escapable.</b> A tenant identity could enter the
/// package either by the package resolving <c>ITenantContext</c> itself, or by
/// <see cref="Excalibur.Data.DataProcessing.IDataOrchestrationManager" /> growing a caller-supplied tenant
/// parameter that a consumer could pass without the package resolving anything ambiently. Both are checked;
/// either one going red is sufficient to force the re-triage this arm exists to force.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> <see cref="CatchesAPlantedTenantContextReference" /> and
/// <see cref="CatchesAPlantedTenantParameter" /> run the same regexes this arm uses against in-memory text
/// carrying the exact violation each check exists to catch, proving the pattern matches before the absence
/// on the real tree is trusted as a finding rather than an under-search.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class DataProcessingQueueStaysTenantlessShould
{
	private static readonly Regex TenantContextReference = new(@"\bITenantContext\b", RegexOptions.Compiled);

	private static readonly Regex TenantParameterName =
		new(@"\btenant\w*\s*,?\s*(CancellationToken|cancellationToken)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static string PackageRoot => Path.Combine(
		TestHelpers.GetRepositoryRoot(),
		"src",
		"Excalibur",
		"Excalibur.Data.DataProcessing");

	[Fact]
	public void ReferenceNoITenantContextAnywhereInThePackage()
	{
		var offenders = new List<string>();

		foreach (var path in Directory.EnumerateFiles(PackageRoot, "*.cs", SearchOption.AllDirectories))
		{
			var text = File.ReadAllText(path);
			if (TenantContextReference.IsMatch(text))
			{
				offenders.Add(Path.GetRelativePath(PackageRoot, path));
			}
		}

		offenders.ShouldBeEmpty(
			"Excalibur.Data.DataProcessing is confirmed DeliberatelyGlobal/NoTenantDimension (sln5bq) precisely "
			+ "because it resolves no ambient tenant identity. A reference here means the premise moved and the "
			+ "five [NoTenantTerm] attributes, their reason strings, and README.md must be re-triaged together.");
	}

	[Fact]
	public void ExposeNoTenantParameterOnTheOrchestrationManagerInterface()
	{
		var interfacePath = Path.Combine(PackageRoot, "IDataOrchestrationManager.cs");
		File.Exists(interfacePath).ShouldBeTrue($"expected {interfacePath} to exist");

		var offenders = new List<string>();
		foreach (var line in File.ReadLines(interfacePath))
		{
			// Only parameter-list lines can carry a tenant argument; skip doc comments and signatures with
			// no parentheses so a prose mention of "tenant" (there is deliberately one, in this very guard's
			// own doc comment on the source file were it ever copied here) cannot false-positive.
			if (!line.Contains('(', StringComparison.Ordinal) || !line.Contains(')', StringComparison.Ordinal))
			{
				continue;
			}

			if (TenantParameterName.IsMatch(line))
			{
				offenders.Add(line.Trim());
			}
		}

		offenders.ShouldBeEmpty(
			"IDataOrchestrationManager's enqueue/drain surface takes a record type only. A tenant-shaped "
			+ "parameter here is exactly the API sln5bq's ruling says does not exist -- confirm the ruling "
			+ "still holds before adding one.");
	}

	[Fact]
	public void CatchesAPlantedTenantContextReference()
	{
		const string planted = "internal sealed class Rogue { private readonly ITenantContext _tenant; }";

		TenantContextReference.IsMatch(planted).ShouldBeTrue(
			"non-vacuity control: the pattern must match a planted ITenantContext reference before its "
			+ "absence on the real tree counts as evidence.");
	}

	[Fact]
	public void CatchesAPlantedTenantParameter()
	{
		const string planted = "Task<Guid> AddDataTaskForRecordTypeAsync(string recordType, string tenantId, CancellationToken cancellationToken);";

		TenantParameterName.IsMatch(planted).ShouldBeTrue(
			"non-vacuity control: the pattern must match a planted tenant parameter before its absence on "
			+ "the real interface counts as evidence.");
	}
}
