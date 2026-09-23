using Excalibur.Compliance;
using Excalibur.Compliance.Soc2;
using Excalibur.Compliance.Soc2.Validators;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// Three properties an aggregate over a set of controls must keep: it must never improve because we
/// verified <b>less</b>, it must be sensitive to <b>every</b> control it claims to cover, and it must
/// never sink to a <b>proven absence</b> on the strength of controls nobody examined.
/// </summary>
/// <remarks>
/// <para>
/// MONOTONICITY. The control bands are ordered so that a proven violation scores BELOW a control
/// nobody could verify — correct at the control level, because a finding is knowledge and an open
/// question is not. A mean inverts that ordering: replacing a control we examined and found violating
/// with one we never examined RAISES the criterion's number, and the gap severity printed beside it
/// softens. The incentive runs backwards, and the auditor reads the softer number. The aggregate now
/// takes the worst band rather than the mean, and these arms hold it there.
/// </para>
/// <para>
/// SENSITIVITY. A criterion's verdict is derived from a SET of controls, so every member of that set
/// must be able to change it. If some control's verdict can flip with no change in what the criterion
/// reports, the aggregate did not fold the set — it sampled it, and the value is a sample wearing a
/// verdict's costume. Returning the first element satisfies every monotonicity arm above while being
/// blind to controls two onward, which is why sensitivity is checked separately and per position.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class CriterionAggregateMonotonicityShould
{
	private readonly IControlValidationService _controlValidation = A.Fake<IControlValidationService>();

	private readonly Soc2Options _options = new()
	{
		EnabledCategories = [TrustServicesCategory.Security],
		MinimumTypeIIPeriodDays = 90,
		DefaultTestSampleSize = 25
	};

	[Fact]
	public async Task Not_improve_a_criterion_because_one_of_its_controls_went_unverified()
	{
		// Both controls examined, both found violating.
		var bothExamined = await ScoreFor(
			Violating("CC6.1"),
			Violating("CC6.2"));

		// Same criterion, but the second control was never examined. Strictly less is known.
		var oneUnverified = await ScoreFor(
			Violating("CC6.1"),
			Unverified("CC6.2"));

		// EQUALITY, not a bound, and the stronger assertion of the two. The aggregation rule is that a
		// control nobody examined is excluded from the aggregate — so swapping one in must change the
		// number NOT AT ALL. A ceiling ("must not rise") is satisfied by a score that DROPS, and dropping
		// is exactly what a read site does when it cannot carry "not examined" forward and substitutes a
		// number for it. Equality refuses both the drop and the rise.
		//
		// Note the asymmetry, so nobody inherits a stronger claim than this arm makes: the aggregate is a
		// Min, which clamps from above, so substituting a HIGH number here is absorbed (min(20, 100) is
		// still 20) and this arm stays green. It is one-sided, and the side it guards is the one that
		// turns an unexamined control into a finding.
		oneUnverified.ShouldBe(
			bothExamined,
			"a control nobody examined is excluded from the aggregate, so adding one in place of an "
			+ "examined control must leave the criterion's number unchanged. If it dropped, some read "
			+ "site substituted a number for 'not examined' and the report now states a finding nobody "
			+ "made. If it rose, the report rewards not looking.");
	}

	[Fact]
	public async Task Not_improve_a_criterion_when_a_control_nobody_examined_is_ADDED()
	{
		// The requirements owner's arm, which is stronger than the one above: that one SWAPPED a
		// control, this one ADDS one. A criterion gains a control the framework never examined -- so
		// strictly less is known about strictly more -- and none of the three things an assessor reads
		// may improve. Less evidence may make a verdict less confident; never more favourable.
		var before = await AssessmentFor(Violating("CC6.1"));
		var after = await AssessmentFor(Violating("CC6.1"), Unverified("CC6.2"));

		after.Score.ShouldBeLessThanOrEqualTo(
			before.Score,
			"adding a control nobody looked at must not raise the criterion score.");

		// GapSeverity ascends Low(0) Medium(1) High(2) Critical(3) -- read from the enum, not assumed --
		// so "not improving" means not becoming a LOWER value. Compared as ints because an enum does
		// not satisfy the IComparable<T> constraint the fluent comparison needs.
		((int)after.WorstSeverity).ShouldBeGreaterThanOrEqualTo(
			(int)before.WorstSeverity,
			"adding an unexamined control must not soften the severity reported beside the gap.");

		if (before.Outcome != CriterionOutcome.Met)
		{
			after.Outcome.ShouldNotBe(
				CriterionOutcome.Met,
				"a criterion that was not met cannot become met by acquiring a control nobody assessed.");
		}
	}

	[Fact]
	public async Task Not_improve_a_generated_report_when_a_control_nobody_examined_is_added()
	{
		// The report generator runs its OWN aggregation, parallel to the compliance service and not
		// shared with it. The criterion must hold on the surface the auditor actually reads, so this
		// arm exists to check the second path rather than assume the first one covers it.
		var before = await SectionFor(Violating("CC6.1"));
		var after = await SectionFor(Violating("CC6.1"), Unverified("CC6.2"));

		if (before != CriterionOutcome.Met)
		{
			after.ShouldNotBe(
				CriterionOutcome.Met,
				"the generated report must not report a criterion as met because it acquired a control "
				+ "nobody examined.");
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task Reflect_a_violation_in_ANY_of_its_controls_and_not_merely_the_first(int violatingPosition)
	{
		// SENSITIVITY, checked per position. Three controls support the criterion; all are effective
		// except the one at `violatingPosition`. Whichever position that is, the criterion must report
		// it -- because the criterion's verdict is a claim about the SET, and a claim about a set that
		// cannot see past its first member is not a verdict, it is a sample.
		//
		// This is what separates a fold from a reduction that was never stated. An aggregate that
		// returns results[0] is monotone, is never wrong about the control it did look at, and passes
		// every arm above. It fails only here, and only for positions one and two -- which is exactly
		// the shape of the defect, so the arm is parameterised by position rather than asserting once.
		var ids = new[] { "CC6.1", "CC6.2", "CC6.3" };
		var controls = ids
			.Select((id, i) => i == violatingPosition ? ViolatingNamed(id) : Effective(id))
			.ToArray();

		var allEffective = await AssessmentFor([.. ids.Select(Effective)]).ConfigureAwait(false);
		var oneViolating = await AssessmentFor(controls).ConfigureAwait(false);

		// The baseline must be a PASS, or "it changed" would be satisfied by a criterion that was
		// already failing for some other reason and the arm would prove nothing.
		allEffective.Outcome.ShouldBe(
			CriterionOutcome.Met,
			"the all-effective baseline must pass, or this arm cannot attribute the change below.");

		oneViolating.Score.ShouldBeLessThan(
			allEffective.Score,
			$"a violation in the control at position {violatingPosition} must lower the criterion score. "
			+ "If it does not, the score was taken from one control rather than folded over all of them.");

		oneViolating.Outcome.ShouldNotBe(
			CriterionOutcome.Met,
			$"a criterion cannot be met while the control at position {violatingPosition} is violating.");

		var status = await StatusFor(controls).ConfigureAwait(false);
		status.CriterionStatuses[TrustServicesCriterion.CC6_LogicalAccess].Gaps.ShouldContain(
			$"A violation was detected in {ids[violatingPosition]}.",
			"the violating control's own issue must reach the criterion's gaps, named, so the auditor "
			+ "can see WHICH control failed rather than only that something did.");
	}

	/// <summary>
	/// The FLOOR. A criterion whose every control went unexamined must not be graded as a proven absence.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every other arm in this class asserts a CEILING — that the number does not RISE when we know less.
	/// All of them are satisfied by an aggregate that reports zero, because zero never rises. So the whole
	/// class is blind in the one direction where an unexamined control is turned into an accusation, and
	/// that is the direction a careless read produces: the control-level score is the only value that says
	/// "not examined", and every site that fails to carry that state forward reaches for a number, which
	/// in this scale is the bottom of it.
	/// </para>
	/// <para>
	/// Zero is not a neutral default here. It is the mechanism-absent band — the claim that the control was
	/// examined and nothing was there — and it grades as a Critical gap. Between a criterion nobody assessed
	/// and a criterion assessed and found empty there is an auditor, and only one of those is a finding
	/// against the consumer.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_grade_a_criterion_of_wholly_unexamined_controls_as_a_proven_absence()
	{
		// Nobody examined either control. The aggregate has no evidence of absence -- only absence of
		// evidence -- so it must not report the band that asserts the first one.
		var unexamined = await ScoreFor(Unverified("CC6.1"), Unverified("CC6.2"));

		((int)unexamined).ShouldBeGreaterThanOrEqualTo(
			(int)ControlEffectiveness.ViolationDetected,
			"no control in this criterion was examined, so the aggregate cannot rank it at or below the "
			+ "band reserved for violations we actually detected. Scoring it lower states that we looked "
			+ "and found the mechanism missing, which is a finding nobody made and the one an assessor "
			+ "acts on. The likeliest way to break this is a read site that cannot carry 'not examined' "
			+ "forward and substitutes a number for it.");

		// LIVENESS. Without this, the assertion above is satisfied by a service that returns a constant
		// high score and never grades anything down -- the cheapest way to be safe and the most expensive
		// way to be wrong. This also binds, at the AGGREGATE, the ordering that is otherwise bound only at
		// the control level: a deficiency we proved must rank below a question we never opened.
		var proven = await ScoreFor(Violating("CC6.1"), Violating("CC6.2"));

		proven.ShouldBeLessThan(
			unexamined,
			"a criterion whose controls were examined and found violating must score BELOW one whose "
			+ "controls were never examined. If these are equal the aggregate is not grading at all, and "
			+ "the arm above proves nothing; if they are inverted, the report rewards not looking.");
	}

	/// <summary>
	/// COMPLETENESS. A criterion cannot be reported met while one of its controls was never examined —
	/// even when every control that WAS examined came back perfect.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The report arm above starts from a criterion that is already failing, so it can only ever assert
	/// that a failure stays a failure. This one starts from a criterion that is <b>passing</b>, which is
	/// the only baseline where "met" is reachable and therefore the only baseline where losing it can be
	/// detected. A rule that says "do not improve" is silent about a case that was already at the top.
	/// </para>
	/// <para>
	/// The property is completeness, not severity: met asserts that EVERY control is at or above the
	/// threshold, and that claim is unavailable while one of them was never looked at. It is not that the
	/// unexamined control scored badly — it is that nobody can say whether it scored at all.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_report_a_criterion_as_met_when_one_of_its_controls_was_never_examined()
	{
		// LIVENESS first, because it establishes that "met" is reachable here at all. Without it, the
		// assertion below is satisfied by a generator that can never report met for any input.
		var allExamined = await SectionFor(Effective("CC6.1"), Effective("CC6.2"));

		allExamined.ShouldBe(
			CriterionOutcome.Met,
			"a criterion whose controls were all examined and all found operating must be reportable as "
			+ "met, or the arm below proves only that this generator never says met.");

		// SAFETY. Same two perfect controls, plus one nobody examined. The examined evidence is unchanged
		// and unimpeachable; what changed is that the criterion is no longer fully covered.
		var oneNeverExamined = await SectionFor(
			Effective("CC6.1"), Effective("CC6.2"), Unverified("CC6.3"));

		oneNeverExamined.ShouldNotBe(
			CriterionOutcome.Met,
			"two controls were examined and found operating, and a third was never examined. 'Met' claims "
			+ "every control in this criterion is at or above the threshold, and that claim cannot be made "
			+ "about a control nobody looked at. The auditor reads met as complete coverage.");
	}

	private async Task<CriterionOutcome> SectionFor(params ControlValidationResult[] results)
	{
		// The generator reaches the validation service through ValidateCriterionAsync, NOT through
		// GetControlsForCriterion + ValidateControlAsync the way the compliance service does. Faking
		// the latter left validationResults EMPTY, so every criterion came back NotAssessed and this
		// arm passed while testing nothing -- which a mutation of the aggregation exposed.
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(
			TrustServicesCriterion.CC6_LogicalAccess, A<CancellationToken>._))
			.Returns(results.ToList());

		var generator = new Soc2ReportGenerator(
			Microsoft.Extensions.Options.Options.Create(_options),
			_controlValidation,
			NullLogger<Soc2ReportGenerator>.Instance);

		var report = await generator.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		return report.ControlSections
			.First(x => x.Criterion == TrustServicesCriterion.CC6_LogicalAccess)
			.Outcome;
	}

	private sealed record Assessment(int Score, GapSeverity WorstSeverity, CriterionOutcome Outcome);

	private async Task<Assessment> AssessmentFor(params ControlValidationResult[] results)
	{
		var status = await StatusFor(results).ConfigureAwait(false);
		var criterion = status.CriterionStatuses[TrustServicesCriterion.CC6_LogicalAccess];

		var worst = status.ActiveGaps.Count > 0
			? status.ActiveGaps.Max(g => g.Severity)
			: GapSeverity.Low;

		return new Assessment(RequireScore(criterion), worst, criterion.Outcome);
	}

	private async Task<int> ScoreFor(params ControlValidationResult[] results)
	{
		var status = await StatusFor(results).ConfigureAwait(false);
		return RequireScore(status.CriterionStatuses[TrustServicesCriterion.CC6_LogicalAccess]);
	}

	/// <summary>
	/// Reads the criterion's score, REFUSING a missing one rather than coalescing it.
	/// </summary>
	/// <remarks>
	/// This was <c>?? 0</c>, and the zero is not a neutral placeholder: it is the mechanism-absent band,
	/// the strongest negative the report can state, and it means "we examined this and there is nothing
	/// there". Coalescing to it would silently grade a criterion nobody assessed as a proven absence —
	/// and every arm in this class would stay GREEN while it happened, because they all assert that the
	/// score does not RISE, and a fabricated 0 only ever makes it fall. That is the defect these arms
	/// exist to prevent, reproduced inside the instrument that measures it.
	/// </remarks>
	private static int RequireScore(CriterionStatus criterion) =>
		criterion.EffectivenessScore
		?? throw new InvalidOperationException(
			"The criterion reported no effectiveness score, and these arms compare scores numerically. "
			+ "A missing score must not be coalesced — 0 is the mechanism-absent band and would assert "
			+ "a finding nobody made. If the aggregate can now legitimately report 'not assessed', bind "
			+ "that state with its own arm instead of giving it a number here.");

	private async Task<ComplianceStatus> StatusFor(params ControlValidationResult[] results)
	{
		var ids = results.Select(r => r.ControlId).ToList();

		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(ids);

		foreach (var r in results)
		{
			A.CallTo(() => _controlValidation.ValidateControlAsync(r.ControlId, A<CancellationToken>._))
				.Returns(r);
		}

		var sut = new Soc2ComplianceService(
			Microsoft.Extensions.Options.Options.Create(_options), _controlValidation);

		return await sut.GetComplianceStatusAsync(null, CancellationToken.None)
			.ConfigureAwait(false);
	}

	// Examined, and a violation was found: the worst band that still means the mechanism is there.
	private static ControlValidationResult Violating(string id) =>
		new()
		{
			ControlId = id,
			IsConfigured = true,
			EffectivenessScore = ControlEffectiveness.ViolationDetected,
			ConfigurationIssues = ["A violation was detected in this control."],
			ValidatedAt = DateTimeOffset.UtcNow
		};

	// Not examined at all. Nothing is known either way.
	private static ControlValidationResult Unverified(string id) =>
		new()
		{
			ControlId = id,
			IsConfigured = true,
			EffectivenessScore = ControlEffectiveness.Unverified,
			ConfigurationIssues = ["This control was not verified in this period."],
			ValidatedAt = DateTimeOffset.UtcNow
		};

	// Examined, and nothing was found wrong.
	private static ControlValidationResult Effective(string id) =>
		new()
		{
			ControlId = id,
			IsConfigured = true,
			EffectivenessScore = ControlEffectiveness.Effective,
			ConfigurationIssues = [],
			ValidatedAt = DateTimeOffset.UtcNow
		};

	// Violating, and the issue text names WHICH control it came from, so the assertion below cannot be
	// satisfied by some other control's issue happening to be present.
	private static ControlValidationResult ViolatingNamed(string id) =>
		new()
		{
			ControlId = id,
			IsConfigured = true,
			EffectivenessScore = ControlEffectiveness.ViolationDetected,
			ConfigurationIssues = [$"A violation was detected in {id}."],
			ValidatedAt = DateTimeOffset.UtcNow
		};
}
