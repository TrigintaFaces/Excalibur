// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Soc2;
using Excalibur.Compliance.Soc2.Validators;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// Pins the property the three-valued outcome exists for: a control the framework could not examine is
/// distinguishable from one it examined and found broken, and from one it examined and found operating.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the arm the previous type could not host.</b> With a boolean verdict, "not examined" and
/// "examined and deficient" were the same value, so no assertion could separate them and the report
/// handed an auditor a finding nobody had made. Every arm below is RED against a boolean verdict, because
/// under one there are only two reachable states to assert.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class UnverifiedControlIsDistinguishableShould
{
	[Fact]
	public void Separate_all_three_outcomes_so_no_two_facts_share_a_value()
	{
		var distinct = new[]
		{
			ControlOutcome.Effective,
			ControlOutcome.NotVerified,
			ControlOutcome.Deficient,
		}.Distinct().Count();

		distinct.ShouldBe(
			3,
			"three facts need three values. A verdict with only two collapses 'we could not examine this' "
			+ "into either a pass nobody established or a finding nobody made.");
	}

	/// <summary>
	/// The ordering is the contract, and it is the half most likely to be broken by a later edit that
	/// renumbers the enum for tidiness.
	/// </summary>
	[Fact]
	public void Rank_a_proven_deficiency_below_an_open_question_below_a_clean_examination()
	{
		((int)ControlOutcome.Deficient).ShouldBeLessThan(
			(int)ControlOutcome.NotVerified,
			"a proven deficiency is knowledge; an unexamined control is not, and must never rank worse.");

		((int)ControlOutcome.NotVerified).ShouldBeLessThan(
			(int)ControlOutcome.Effective,
			"an open question must never rank as well as a control that was examined and found operating.");
	}

	/// <summary>
	/// A control with no registered validator was never examined. Reporting it as deficient sends an
	/// auditor looking for a defect that may not exist, and is a finding the framework never made.
	/// </summary>
	[Fact]
	public async Task Report_a_control_with_no_registered_validator_as_not_verified()
	{
		var service = new ControlValidationService([]);

		var result = await service.ValidateControlAsync("SEC-999", CancellationToken.None);

		result.Outcome.ShouldBe(
			ControlOutcome.NotVerified,
			"no validator registered means this framework did not look. That is not a deficiency, and "
			+ "reporting one states a finding nobody made.");

		result.Outcome.ShouldNotBe(ControlOutcome.Deficient);
		result.Outcome.ShouldNotBe(ControlOutcome.Effective);
	}

	/// <summary>
	/// The verdict and the band cannot disagree, because only the factories set them and the verdict is
	/// derived from the band. This arm is what makes that derivation load-bearing rather than incidental.
	/// </summary>
	[Theory]
	[InlineData(ControlEffectiveness.MechanismAbsent, ControlOutcome.Deficient)]
	[InlineData(ControlEffectiveness.ViolationDetected, ControlOutcome.Deficient)]
	[InlineData(ControlEffectiveness.Unverified, ControlOutcome.NotVerified)]
	[InlineData(ControlEffectiveness.Effective, ControlOutcome.Effective)]
	public void Agree_with_the_band_it_was_derived_from(
		ControlEffectiveness band,
		ControlOutcome expected)
	{
		var validator = new ProbeValidator();

		validator.Produce("CTL-1", band).Outcome.ShouldBe(
			expected,
			$"band {band} and the reported outcome must state the same fact. A result whose verdict "
			+ "contradicts its own score is a worse artifact than either value alone.");
	}

	/// <summary>
	/// An unverified control must not be able to satisfy the met threshold. This is the arm that stops an
	/// unexamined control sitting inside a passing criterion.
	/// </summary>
	[Fact]
	public void Keep_an_unverified_control_below_the_met_threshold() =>
		((int)ControlEffectiveness.Unverified).ShouldBeLessThan(
			Soc2EffectivenessScore.MetThreshold,
			"a criterion containing a control nobody examined cannot be reported as met.");

	/// <summary>
	/// Reaches the protected factories so the derivation is exercised through the real production path
	/// rather than a reimplementation of it.
	/// </summary>
	private sealed class ProbeValidator : BaseControlValidator
	{
		public override IReadOnlyList<string> SupportedControls => ["CTL-1"];

		public ControlValidationResult Produce(string controlId, ControlEffectiveness band) =>
			band == ControlEffectiveness.Effective
				? CreateSuccessResult(controlId)
				: CreateFailureResult(controlId, ["probe"], band);

		public override IReadOnlyList<TrustServicesCriterion> SupportedCriteria =>
			[TrustServicesCriterion.CC6_LogicalAccess];

		public override ControlDescription? GetControlDescription(string controlId) => null;

		public override Task<ControlValidationResult> ValidateAsync(
			string controlId,
			CancellationToken cancellationToken) =>
			Task.FromResult(Produce(controlId, ControlEffectiveness.Unverified));
	}
}
