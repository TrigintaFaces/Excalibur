// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// The PRODUCER side of the annotation-scan establishment flag: the real reflection source must actually
/// emit it, not merely have somewhere to put it.
/// </summary>
/// <remarks>
/// <para>
/// The coverage-gate arms in <c>ErasureCoverageGateShould</c> drive the gate through a test double, so
/// they prove what the gate does <em>given</em> an unestablished scan and say nothing about whether
/// anything ever produces one. A flag no producer sets is worse than no flag at all: the type now
/// advertises that "unknown" is expressible, so a reader infers that a non-null value was measured.
/// This class is the other half.
/// </para>
/// <para>
/// <b>What this host can and cannot prove, stated rather than implied.</b> A JIT test host can establish
/// the positive direction — dynamic code is supported, nothing was skipped, so the scan reports itself
/// established. The negative directions are not reachable from here: an ahead-of-time host is a different
/// publish mode, and the two skip paths need an assembly whose types deliberately fail to load. Those are
/// real gaps and are named here rather than papered over with an arm that pretends to cover them.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Priority", "1")]
public sealed class ReflectionAnnotationScanReportsEstablishmentShould
{
	[Fact]
	public void ReportEstablished_WhenReflectionIsAvailableAndNothingWasSkipped()
	{
		// CONTROL FIRST: this arm is only meaningful in a host where dynamic code is supported. If that
		// ever stops being true here, the assertion below would be asserting the wrong thing rather than
		// failing honestly, so the precondition is checked instead of assumed.
		RuntimeFeature.IsDynamicCodeSupported.ShouldBeTrue(
			"this test host must support dynamic code for the established-scan arm to mean anything");

		var scan = new ReflectionPersonalDataAnnotationSource().GetAnnotatedCategories();

		scan.ScanEstablished.ShouldBeTrue(
			"a JIT host with no load failures has examined everything the scan set out to examine, so the "
			+ "absence of a category from the result is real information and the gate may act on it");
	}

	[Fact]
	public void FindTheAnnotatedCategoriesPresentInThisAssembly_SoTheScanIsNotVacuouslyEmpty()
	{
		// The establishment flag is worthless if the scan returns nothing regardless. This assembly carries
		// [PersonalData]-annotated fixture properties, so a working scan must see at least one category --
		// which makes "established and empty" a claim the scan could actually have contradicted.
		var scan = new ReflectionPersonalDataAnnotationSource().GetAnnotatedCategories();

		scan.Categories.ShouldNotBeEmpty(
			"this assembly declares [PersonalData]-annotated fixture properties, so a scan that returns "
			+ "nothing is not reading them -- and an establishment flag on a scan that never finds "
			+ "anything would certify an emptiness it did not earn");
	}
}
