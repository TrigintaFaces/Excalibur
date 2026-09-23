// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Compliance.Soc2.Validators;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// Pins the properties that make a self-contradictory control verdict unwritable rather than merely
/// discouraged.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was wrong.</b> A validator reported two facts side by side - a verdict and a 0-100 score -
/// and nothing tied them together outside one factory method. A consumer implementing
/// <c>IControlValidator</c> could return <c>Outcome = Effective</c> with a score of <c>0</c>, and the
/// conformance kit we ship them to check their work with certified it: its only check on the score was
/// that it fell between 0 and 100.
/// </para>
/// <para>
/// <b>Why the numbers were the hazard and not just the pairing.</b> An integer on a 0-100 scale reads as
/// a percentage, so a consumer writes <c>75</c> meaning "good, minor notes". The framework read
/// everything below 80 that was not exactly 40 as a finding, so that 75 arrived in the document handed
/// to an external assessor as a deficiency against the consumer. The superseded scheme did this to
/// itself: it scored a <b>confirmed tampered audit trail at 75</b>, above a control that was merely
/// unverifiable at 40, because the arithmetic counted remarks rather than severity.
/// </para>
/// <para>
/// <b>The arms below are deliberately of two kinds.</b> Some assert behaviour; others assert the SHAPE
/// of the type, by reflection, and those exist because the guarantee is structural. The mutant they are
/// written against is a future edit re-adding a settable <c>Outcome</c> or restoring the band
/// parameter default - changes that break no behaviour on the day they are made and silently reopen the
/// hole.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ControlEffectivenessBandShould
{
	/// <summary>
	/// SAFETY, structural. A verdict that contradicts its own band must be unconstructible, not rejected.
	/// </summary>
	/// <remarks>
	/// The compiler already enforces this - every call site that used to set <c>Outcome</c> stopped
	/// compiling. This arm exists so that re-adding the setter fails a TEST as well, rather than only
	/// failing whichever call sites happen to exist at the time. A structural guarantee with no arm is
	/// one refactor away from being a convention.
	/// </remarks>
	[Fact]
	public void Refuse_to_expose_any_way_of_setting_the_outcome_independently_of_the_band()
	{
		var outcome = typeof(ControlValidationResult).GetProperty(nameof(ControlValidationResult.Outcome));

		outcome.ShouldNotBeNull();
		outcome!.CanWrite.ShouldBeFalse(
			"Outcome must be derived from EffectivenessScore, never settable beside it. A settable "
			+ "Outcome is the exact state this change removed: it lets a validator assert a verdict its "
			+ "own band contradicts, and the shipped conformance kit certified one.");
		outcome.SetMethod.ShouldBeNull(
			"an init accessor is a set accessor for this purpose - it is enough to construct the "
			+ "disagreeing pair in an object initializer, which is how every affected fixture did it.");
	}

	/// <summary>
	/// SAFETY, structural. The band parameter must have no default, so "I did not say" cannot compile.
	/// </summary>
	/// <remarks>
	/// The default used to be <c>0</c>, which is <see cref="ControlEffectiveness.MechanismAbsent" />. So
	/// the shortest call a consumer could write - control id and issues - silently asserted that the
	/// mechanism the control depends on does not exist, when the caller meant only to record an issue.
	/// </remarks>
	[Fact]
	public void Refuse_to_let_a_caller_omit_the_band_and_inherit_the_worst_one()
	{
		var band = typeof(BaseControlValidator)
			.GetMethod("CreateFailureResult", BindingFlags.NonPublic | BindingFlags.Static)!
			.GetParameters()
			.Single(p => p.ParameterType == typeof(ControlEffectiveness));

		band.HasDefaultValue.ShouldBeFalse(
			"the band must be stated. Its previous default was MechanismAbsent, so the shortest and most "
			+ "natural call asserted the harshest fact about the control -- in a document that goes to an "
			+ "external assessor.");
	}

	/// <summary>
	/// ORDERING. The declared order is the contract every aggregate and threshold relies on.
	/// </summary>
	[Theory]
	[InlineData(ControlEffectiveness.MechanismAbsent, ControlEffectiveness.ViolationDetected)]
	[InlineData(ControlEffectiveness.ViolationDetected, ControlEffectiveness.Unverified)]
	[InlineData(ControlEffectiveness.Unverified, ControlEffectiveness.Effective)]
	public void Rank_a_worse_established_fact_below_a_better_one(
		ControlEffectiveness worse,
		ControlEffectiveness better)
	{
		// Cast to the underlying value deliberately: the ordering contract IS over the declared
		// values, and Shouldly's generic comparison needs IComparable<T>, which an enum does not
		// implement. Comparing the values asserts exactly the claim without inventing a comparer.
		((int)worse).ShouldBeLessThan(
			(int)better,
			"the ordering is the contract, not the particular numbers. The criterion aggregate takes the "
			+ "worst band in the set, so an inverted pair reports the friendlier fact -- which is exactly "
			+ "how a confirmed tampered audit trail came to outrank a merely unverifiable control.");
	}

	/// <summary>
	/// ORDERING, the specific inversion this type exists to prevent.
	/// </summary>
	/// <remarks>
	/// A finding is knowledge; an absence of examination is not. A control we examined and found broken
	/// must never read as better than one nobody examined, or examining less produces the friendlier
	/// number and the softer gap severity beside it.
	/// </remarks>
	[Fact]
	public void Rank_a_proven_violation_below_a_control_nobody_examined()
	{
		((int)ControlEffectiveness.ViolationDetected)
			.ShouldBeLessThan((int)ControlEffectiveness.Unverified);
	}

	/// <summary>
	/// DERIVATION. Each band asserts exactly one verdict, and Deficient covers two bands jointly.
	/// </summary>
	/// <remarks>
	/// The many-to-one direction is why the derivation runs band-to-outcome and not the reverse: a
	/// factory deriving the band from the outcome would have to invent one for <c>Deficient</c>, and
	/// would make the report say "mechanism absent" about a control we examined and found broken.
	/// </remarks>
	[Theory]
	[InlineData(ControlEffectiveness.MechanismAbsent, ControlOutcome.Deficient)]
	[InlineData(ControlEffectiveness.ViolationDetected, ControlOutcome.Deficient)]
	[InlineData(ControlEffectiveness.Unverified, ControlOutcome.NotVerified)]
	[InlineData(ControlEffectiveness.Effective, ControlOutcome.Effective)]
	public void Derive_the_verdict_the_band_asserts(ControlEffectiveness band, ControlOutcome expected)
	{
		var result = new ControlValidationResult
		{
			ControlId = "CTRL-001",
			IsConfigured = true,
			EffectivenessScore = band
		};

		result.Outcome.ShouldBe(expected);
	}

	/// <summary>
	/// SAFETY. A value outside the declared set is refused at construction.
	/// </summary>
	/// <remarks>
	/// An enum does not close its own domain - <c>(ControlEffectiveness)75</c> is legal C#, and 75 is
	/// precisely the number the superseded scheme produced. The ordering the report relies on is defined
	/// only over the declared members, so an undeclared value is refused where it enters rather than
	/// ranked against members it does not belong beside.
	/// </remarks>
	[Theory]
	[InlineData(75)]
	[InlineData(50)]
	[InlineData(-1)]
	[InlineData(101)]
	public void Refuse_an_undeclared_band_at_construction(int undeclared)
	{
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new ControlValidationResult
		{
			ControlId = "CTRL-002",
			IsConfigured = true,
			EffectivenessScore = (ControlEffectiveness)undeclared
		});
	}

	/// <summary>
	/// LIVENESS, and it is the arm that stops the one above from passing vacuously.
	/// </summary>
	/// <remarks>
	/// A constructor that refused EVERY band would satisfy the refusal arm completely, and would be
	/// indistinguishable from a correct one under it. Every declared band must still construct.
	/// </remarks>
	[Theory]
	[InlineData(ControlEffectiveness.MechanismAbsent)]
	[InlineData(ControlEffectiveness.ViolationDetected)]
	[InlineData(ControlEffectiveness.Unverified)]
	[InlineData(ControlEffectiveness.Effective)]
	public void Still_accept_every_declared_band(ControlEffectiveness band)
	{
		var result = new ControlValidationResult
		{
			ControlId = "CTRL-003",
			IsConfigured = true,
			EffectivenessScore = band
		};

		result.EffectivenessScore.ShouldBe(band);
	}

	/// <summary>
	/// LIVENESS. A copied result keeps the band it was constructed with.
	/// </summary>
	/// <remarks>
	/// The band is held in a field so the record copy constructor carries it directly rather than
	/// re-entering the init accessor. That is correct - an already-validated value is not re-validated -
	/// but it is worth pinning, because a copy that silently lost the band would report
	/// <c>MechanismAbsent</c> by default and turn every <c>with</c> expression into an accusation.
	/// </remarks>
	[Fact]
	public void Carry_the_band_and_its_verdict_through_a_copy()
	{
		var original = new ControlValidationResult
		{
			ControlId = "CTRL-004",
			IsConfigured = true,
			EffectivenessScore = ControlEffectiveness.Effective
		};

		var copied = original with { ControlId = "CTRL-005" };

		copied.EffectivenessScore.ShouldBe(ControlEffectiveness.Effective);
		copied.Outcome.ShouldBe(ControlOutcome.Effective);
	}
}
