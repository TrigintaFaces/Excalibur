// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Boundary.Tests;

/// <summary>
/// Keeps the capability-exemption marker to a single definition, so a CI gate reading test results can
/// trust it.
/// </summary>
/// <remarks>
/// <para>
/// Two CI checks classify a skipped test by matching a literal marker in its reason: the transport
/// conformance assertion and the inline result check in the main build workflow. A skip carrying the
/// marker is a fact that cannot apply to the implementation under test; a skip without it is a test
/// somebody silenced, which needs an owner and an expiry. The gates match the string literally,
/// because a gate that infers intent from prose reclassifies a suppression the day somebody rewords it.
/// </para>
/// <para>
/// Which makes a hand-typed marker a silent single point of failure. A typo turns a declared exemption
/// into an undeclared skip and reddens a correct suite, or — the direction that actually costs
/// something — a reworded suppression acquires the marker and is waved through as a permanent fact. The
/// helper exists so the string has one definition; this test is what keeps the helper from being merely
/// a suggestion. A helper nobody is obliged to call is a convention, not a control.
/// </para>
/// </remarks>
public sealed class CapabilityMarkerIsNotHandTypedShould
{
	/// <summary>
	/// The marker, assembled rather than written, so this detector is not itself a hand-typed copy of
	/// the string it forbids. Boundary.Tests references only <c>src/**</c>, so the constant in
	/// Tests.Shared is out of reach here and cannot be used directly.
	/// </summary>
	private static readonly string Marker = "[capability-" + "not-applicable]";

	/// <summary>
	/// The one file permitted to contain the literal: the helper that defines it.
	/// </summary>
	private const string DefinitionPath = "tests/Shared/Tests.Shared/ConformanceSkip.cs";

	/// <summary>
	/// The call every skip site must route through, named here so a failure says what to do.
	/// </summary>
	private const string HelperCall = "ConformanceSkip.CapabilityNotApplicable";

	[Fact]
	[Trait("Category", "Unit")]
	public void OnlyTheHelperDefinesTheMarker()
	{
		var repositoryRoot = TestHelpers.GetRepositoryRoot();
		var testsRoot = Path.Combine(repositoryRoot, "tests");

		Directory.Exists(testsRoot).ShouldBeTrue(
			$"the test tree was not found at '{testsRoot}', so this guard measured nothing. An absent "
			+ "search root and a clean result are the same output otherwise.");

		var offenders = new List<string>();
		var definitionSeen = false;

		foreach (var file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
		{
			var relative = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');

			// Build output under bin/ and obj/ is a copy of source, not a second author of it.
			if (relative.Contains("/bin/", StringComparison.Ordinal)
				|| relative.Contains("/obj/", StringComparison.Ordinal))
			{
				continue;
			}

			if (!File.ReadAllText(file).Contains(Marker, StringComparison.Ordinal))
			{
				continue;
			}

			if (string.Equals(relative, DefinitionPath, StringComparison.OrdinalIgnoreCase))
			{
				definitionSeen = true;
				continue;
			}

			offenders.Add(relative);
		}

		// LIVENESS. Without this the test passes when the helper is deleted, when the marker is renamed,
		// and when the search finds no files at all -- every one of which reads as "nobody hand-typed it".
		// A guard whose clean result is also its broken result is the defect it was written to prevent.
		definitionSeen.ShouldBeTrue(
			$"'{DefinitionPath}' does not contain the marker, so there is nothing for the CI gates to "
			+ "match and this guard has no subject. Either the helper moved, or the marker was renamed "
			+ "there without updating the gates that compare it literally.");

		// SAFETY.
		offenders.ShouldBeEmpty(
			$"these files hand-type the capability marker instead of calling {HelperCall}(reason): "
			+ $"{string.Join(", ", offenders)}. Route each skip through the helper so the string keeps a "
			+ "single definition; a typo in a hand-typed copy silently reclassifies the skip and no gate "
			+ "can report it.");
	}
}
