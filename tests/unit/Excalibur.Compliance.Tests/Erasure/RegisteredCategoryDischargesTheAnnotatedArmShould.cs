// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Locks the annotated-category coverage arm against the defect where the gate prescribed a remedy that
/// could not discharge it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> The arm asked whether an annotated <c>[PersonalData]</c> category is represented by
/// any location, and built its covered set from DISCOVERED locations alone. A category the consumer had
/// already registered — but whose rows no contributor happened to discover — was therefore reported as an
/// uncovered annotation, and the accompanying error text told them to call the registration API. Doing so
/// could not change the arm that printed the message, because the arm never read registrations. The
/// consumer spends effort and concludes the gate is broken.
/// </para>
/// <para>
/// <b>Why the fix is a sibling collection and not a member of <see cref="DataLocationKey"/>.</b> The
/// declared-versus-discharged gate matches on table-and-field pairs deliberately: a contributor reporting
/// that it erased <c>Customers.Email</c> cannot know which category a registration filed it under. Folding
/// the category into the key would make the two sides stop matching and report a false outstanding
/// obligation on every pair whose category the contributor could not name — trading this defect for a
/// worse one.
/// </para>
/// <para>
/// <b>The liveness arm is not optional here.</b> "No category is reported uncovered" is satisfied by an
/// arm that reports nothing at all, which is precisely how this class of defect hides. The second test
/// pins that a genuinely absent category IS still reported, so an implementation that simply stopped
/// flagging would fail rather than look fixed.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RegisteredCategoryDischargesTheAnnotatedArmShould
{
	/// <summary>Builds an annotation scan asserting the supplied categories were established.</summary>
	/// <param name="categories">The annotated categories the scan found.</param>
	/// <returns>An established scan over those categories.</returns>
	private static PersonalDataAnnotationScan ScanOf(params PersonalDataCategory[] categories) =>
		new(new HashSet<PersonalDataCategory>(categories), ScanEstablished: true);

	/// <summary>Evaluates coverage over an inventory carrying only declared data, nothing discovered.</summary>
	/// <param name="inventory">The inventory under test.</param>
	/// <param name="scan">The annotation scan under test.</param>
	/// <returns>The coverage outcome.</returns>
	private static CoverageOutcome EvaluateWithNothingDiscovered(
		DataInventory? inventory,
		PersonalDataAnnotationScan scan) =>
		ErasureCoverageEvaluator.Evaluate(
			locations: [],
			deletedKeyIds: [],
			contributors: [],
			annotatedScan: scan,
			inventory: inventory,
			contributorResults: []);

	[Fact]
	public void Treat_a_registered_category_as_covered_even_when_nothing_was_discovered()
	{
		// The consumer HAS registered Identity. Nothing was discovered for it — which is the ordinary
		// case for a subject with no rows in that table, and is exactly the state the old arm misread.
		var inventory = new DataInventory
		{
			DataSubjectId = "abc123hash",
			Locations = [],
			DeclaredLocations = [new DataLocationKey("Customers", "Email")],
			DeclaredCategories = [nameof(PersonalDataCategory.Identity)],
		};

		var outcome = EvaluateWithNothingDiscovered(inventory, ScanOf(PersonalDataCategory.Identity));

		outcome.UncoveredAnnotatedCategories.ShouldBeEmpty(
			"a category the consumer already registered must not be reported as an uncovered annotation — "
			+ "the remedy the gate prints for that finding is the very registration they already performed");
	}

	[Fact]
	public void Still_report_a_category_that_is_neither_registered_nor_discovered()
	{
		// LIVENESS. Without this arm, an implementation that reported NO uncovered category ever would
		// satisfy the test above and look fixed. This is the arm that fails for such an implementation.
		var inventory = new DataInventory
		{
			DataSubjectId = "abc123hash",
			Locations = [],
			DeclaredLocations = [new DataLocationKey("Customers", "Email")],
			DeclaredCategories = [nameof(PersonalDataCategory.Identity)],
		};

		var outcome = EvaluateWithNothingDiscovered(inventory, ScanOf(PersonalDataCategory.ContactInfo));

		outcome.UncoveredAnnotatedCategories.ShouldContain(
			nameof(PersonalDataCategory.ContactInfo),
			"an annotated category that is neither registered nor discovered is annotated personal data the "
			+ "inventory never located, and it must still block a Completed certificate");
	}

	[Fact]
	public void Match_a_registered_category_case_insensitively()
	{
		// The registration carries whatever casing the consumer wrote; the annotation carries the enum's.
		// Comparing them ordinally would reintroduce the defect for every consumer who typed "identity".
		var inventory = new DataInventory
		{
			DataSubjectId = "abc123hash",
			Locations = [],
			DeclaredLocations = [new DataLocationKey("Customers", "Email")],
			DeclaredCategories = ["identity"],
		};

		var outcome = EvaluateWithNothingDiscovered(inventory, ScanOf(PersonalDataCategory.Identity));

		outcome.UncoveredAnnotatedCategories.ShouldBeEmpty(
			"category matching is case-insensitive, so a registration written in a different case still "
			+ "discharges the annotated arm");
	}

	[Fact]
	public void Report_an_annotated_category_when_no_inventory_exists_at_all()
	{
		// A null inventory must not be read as "everything is registered". The declared set being absent
		// is the absence of evidence, and the arm has to keep failing closed over it.
		var outcome = EvaluateWithNothingDiscovered(inventory: null, ScanOf(PersonalDataCategory.Identity));

		outcome.UncoveredAnnotatedCategories.ShouldContain(
			nameof(PersonalDataCategory.Identity),
			"a missing inventory is not a clean bill of health; with nothing registered and nothing "
			+ "discovered the annotated category is uncovered");
	}
}
