using Excalibur.Compliance;
using Excalibur.Compliance.Soc2;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// Two properties an aggregate over a set of controls must keep: it must never improve because we
/// verified <b>less</b>, and it must be sensitive to <b>every</b> control it claims to cover.
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

		oneUnverified.ShouldBeLessThanOrEqualTo(
			bothExamined,
			"knowing LESS about a criterion must never make it score higher. The second case examined "
			+ "one control instead of two and found nothing good; if its number is larger, the report "
			+ "rewards not looking.");
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

		return new Assessment(criterion.EffectivenessScore ?? 0, worst, criterion.Outcome);
	}

	private async Task<int> ScoreFor(params ControlValidationResult[] results)
	{
		var status = await StatusFor(results).ConfigureAwait(false);
		return status.CriterionStatuses[TrustServicesCriterion.CC6_LogicalAccess].EffectivenessScore ?? 0;
	}

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
			IsEffective = false,
			EffectivenessScore = 20,
			ConfigurationIssues = ["A violation was detected in this control."],
			ValidatedAt = DateTimeOffset.UtcNow
		};

	// Not examined at all. Nothing is known either way.
	private static ControlValidationResult Unverified(string id) =>
		new()
		{
			ControlId = id,
			IsConfigured = true,
			IsEffective = false,
			EffectivenessScore = 40,
			ConfigurationIssues = ["This control was not verified in this period."],
			ValidatedAt = DateTimeOffset.UtcNow
		};

	// Examined, and nothing was found wrong.
	private static ControlValidationResult Effective(string id) =>
		new()
		{
			ControlId = id,
			IsConfigured = true,
			IsEffective = true,
			EffectivenessScore = 100,
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
			IsEffective = false,
			EffectivenessScore = 20,
			ConfigurationIssues = [$"A violation was detected in {id}."],
			ValidatedAt = DateTimeOffset.UtcNow
		};
}
