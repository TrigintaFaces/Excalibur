// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Soc2;

using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Proves the unsupported-control conformance arm can REJECT, by running it against a validator built
/// to violate exactly the property it certifies.
/// </summary>
/// <remarks>
/// <para>
/// The arm previously read <c>if (result.Outcome == TestOutcome.NoExceptions &amp;&amp;
/// result.ExceptionsFound == 0) { }</c> — it tested the negation of its own stated requirement and did
/// nothing either way, so it could not fail for any input. A kit whose purpose is assurance was
/// certifying a property nobody checked.
/// </para>
/// <para>
/// <b>Why this test exists and an inverted-assertion check would not do.</b> Inverting the assertion
/// proves only that the arm executes. It does not prove the arm rejects a validator a consumer could
/// actually write. The fake below IS that validator — it answers a control it does not support with a
/// clean bill of health — and the arm must throw on it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ControlValidatorConformanceKitLivenessShould
{
	[Fact]
	public async Task Reject_a_validator_that_reports_a_clean_pass_for_a_control_it_does_not_support()
	{
		var kit = new KitOverFabricatingValidator();

		_ = await Should.ThrowAsync<TestFixtureAssertionException>(
			kit.RunTestAsync_UnsupportedControl_ShouldNotFabricateAPass());
	}

	/// <summary>
	/// The half the arm was missing. Tightening it to reject <c>NoExceptions</c> closed fabricated
	/// ASSURANCE and left fabricated ACCUSATION open: a validator could report <c>ControlFailure</c> about
	/// a control it never examined and pass conformance. That is the more damaging direction, because
	/// <c>Soc2ReportGenerator</c> collects every non-<c>NoExceptions</c> outcome into the report findings,
	/// so it reaches an external assessor as a finding against the consumer. RED against the pre-fix
	/// predicate (<c>== TestOutcome.NoExceptions</c>), which this validator does not trip.
	/// </summary>
	[Fact]
	public async Task Reject_a_validator_that_accuses_an_unsupported_control_of_failing_a_test_it_never_ran()
	{
		var kit = new KitOverAccusingValidator();

		_ = await Should.ThrowAsync<TestFixtureAssertionException>(
			kit.RunTestAsync_UnsupportedControl_ShouldNotFabricateAPass());
	}

	/// <summary>
	/// The same accusation one notch softer. <c>SignificantExceptions</c> is also collected into the
	/// report findings, so excluding only <c>ControlFailure</c> would leave the same defect reachable by
	/// a different enum member — the predicate must require <c>NotTested</c>, not exclude a blocklist.
	/// </summary>
	[Fact]
	public async Task Reject_a_validator_that_reports_exceptions_for_a_control_it_does_not_support()
	{
		var kit = new KitOverExceptionReportingValidator();

		_ = await Should.ThrowAsync<TestFixtureAssertionException>(
			kit.RunTestAsync_UnsupportedControl_ShouldNotFabricateAPass());
	}

	[Fact]
	public async Task Accept_a_validator_that_reports_the_control_was_not_tested()
	{
		// The safety arm's partner: the check must not reject an honest implementation, or it would
		// simply be a different way of failing everyone.
		var kit = new KitOverHonestValidator();

		await kit.RunTestAsync_UnsupportedControl_ShouldNotFabricateAPass();
	}

	/// <summary>
	/// The safety half for the OTHER unsupported-control arm. <c>Deficient</c> is the damaging direction:
	/// it reaches an assessor as a finding against the consumer for a control nobody examined.
	/// </summary>
	[Fact]
	public async Task Reject_a_validator_that_accuses_an_unsupported_control_of_being_deficient()
	{
		var kit = new KitOverDeficientOnUnsupportedValidator();

		var ex = await Should.ThrowAsync<TestFixtureAssertionException>(
			kit.ValidateAsync_UnsupportedControl_ShouldReturnFailure());

		// Bind the CAUSE, not merely the throw. This arm throws in two places - a null result and a wrong
		// outcome - so an unbound assertion stays green against a stub that returns null, and the outcome
		// predicate this test exists to certify would silently stop being covered.
		ex.Message.ShouldContain(
			nameof(ControlOutcome.Deficient),
			Case.Sensitive,
			"the refusal must name the outcome it rejected, or this case cannot distinguish 'the arm caught "
			+ "a Deficient verdict' from 'the arm tripped over a null result'.");
	}

	/// <summary>
	/// The pre-existing arm already failed on <c>Effective</c>. Keeping a case for it means a future edit
	/// that narrows the predicate to Deficient alone cannot pass unnoticed.
	/// </summary>
	[Fact]
	public async Task Reject_a_validator_that_certifies_an_unsupported_control_as_effective()
	{
		var kit = new KitOverEffectiveOnUnsupportedValidator();

		var ex = await Should.ThrowAsync<TestFixtureAssertionException>(
			kit.ValidateAsync_UnsupportedControl_ShouldReturnFailure());

		ex.Message.ShouldContain(nameof(ControlOutcome.Effective), Case.Sensitive);
	}

	/// <summary>
	/// The liveness partner. Without it the arm could be satisfied by a predicate that rejects everything,
	/// which certifies nothing and fails every honest implementor.
	/// </summary>
	[Fact]
	public async Task Accept_a_validator_that_reports_an_unsupported_control_as_not_verified() =>
		await new KitOverNotVerifiedOnUnsupportedValidator()
			.ValidateAsync_UnsupportedControl_ShouldReturnFailure();

	private sealed class KitOverDeficientOnUnsupportedValidator : ControlValidatorConformanceTestKit
	{
		protected override IControlValidator CreateValidator() => new DeficientOnUnsupportedValidator();
	}

	private sealed class KitOverEffectiveOnUnsupportedValidator : ControlValidatorConformanceTestKit
	{
		protected override IControlValidator CreateValidator() => new EffectiveOnUnsupportedValidator();
	}

	private sealed class KitOverNotVerifiedOnUnsupportedValidator : ControlValidatorConformanceTestKit
	{
		protected override IControlValidator CreateValidator() => new NotVerifiedOnUnsupportedValidator();
	}

	/// <summary>Accuses a control it never examined. Inherits the base defaults deliberately.</summary>
	private sealed class DeficientOnUnsupportedValidator : StubValidator;

	/// <summary>Certifies a control it never examined.</summary>
	private sealed class EffectiveOnUnsupportedValidator : StubValidator
	{
		protected override ControlEffectiveness UnsupportedBand => ControlEffectiveness.Effective;
	}

	/// <summary>Declines to claim either verdict - the only conforming answer.</summary>
	private sealed class NotVerifiedOnUnsupportedValidator : StubValidator
	{
		protected override ControlEffectiveness UnsupportedBand => ControlEffectiveness.Unverified;
	}

	private sealed class KitOverFabricatingValidator : ControlValidatorConformanceTestKit
	{
		protected override IControlValidator CreateValidator() => new FabricatingValidator();
	}

	private sealed class KitOverHonestValidator : ControlValidatorConformanceTestKit
	{
		protected override IControlValidator CreateValidator() => new HonestValidator();
	}

	private sealed class KitOverAccusingValidator : ControlValidatorConformanceTestKit
	{
		protected override IControlValidator CreateValidator() => new AccusingValidator();
	}

	private sealed class KitOverExceptionReportingValidator : ControlValidatorConformanceTestKit
	{
		protected override IControlValidator CreateValidator() => new ExceptionReportingValidator();
	}

	private abstract class StubValidator : IControlValidator
	{
		public IReadOnlyList<string> SupportedControls => ["CTRL-001"];

		public IReadOnlyList<TrustServicesCriterion> SupportedCriteria =>
			[TrustServicesCriterion.CC6_LogicalAccess];

		/// <summary>
		/// What this stub reports for a control it does not support. The default reproduces the defect the
		/// arm exists to catch, so a stub that says nothing is a NON-conforming one - the same
		/// silence-maps-to-the-worst-verdict shape the production validators were fixed for.
		/// </summary>
		/// <remarks>
		/// <b>There is no companion outcome property any more, and its absence is the fix.</b> This stub
		/// used to declare the band AND the verdict separately, which is precisely how a validator states
		/// two facts that contradict each other - the defect the kit is meant to catch, reproduced inside
		/// the kit's own harness. The verdict is now derived from the band, so a disagreeing pair cannot
		/// be written here either.
		/// <para>
		/// The band values are also no longer restated as local literals. They used to be, because the
		/// production constants were <c>internal</c> to Excalibur.Compliance and this assembly is not on
		/// its friend list - the same wall a CONSUMER deriving this shipped kit hit. Naming
		/// <see cref="ControlEffectiveness" /> directly is the evidence that the wall is gone: what this
		/// harness can now say, a consumer can say.
		/// </para>
		/// </remarks>
		protected virtual ControlEffectiveness UnsupportedBand => ControlEffectiveness.MechanismAbsent;

		public Task<ControlValidationResult> ValidateAsync(
			string controlId, CancellationToken cancellationToken) =>
			Task.FromResult(
				SupportedControls.Contains(controlId, StringComparer.Ordinal)
					? new ControlValidationResult
					{
						ControlId = controlId,
						IsConfigured = true,
						EffectivenessScore = ControlEffectiveness.Effective
					}
					: new ControlValidationResult
					{
						ControlId = controlId,
						IsConfigured = false,
						EffectivenessScore = UnsupportedBand
					});

		public virtual Task<ControlTestResult> RunTestAsync(
			string controlId, ControlTestParameters parameters, CancellationToken cancellationToken) =>
			Task.FromResult(new ControlTestResult
			{
				ControlId = controlId,
				Parameters = parameters,
				ItemsTested = 0,
				ExceptionsFound = null,
				Outcome = TestOutcome.NotTested,
				Notes = $"No validator supports {controlId}."
			});

		public ControlDescription? GetControlDescription(string controlId) => null;
	}

	/// <summary>Answers a control it does not support with a clean pass — the thing the arm must catch.</summary>
	private sealed class FabricatingValidator : StubValidator
	{
		public override Task<ControlTestResult> RunTestAsync(
			string controlId, ControlTestParameters parameters, CancellationToken cancellationToken) =>
			Task.FromResult(new ControlTestResult
			{
				ControlId = controlId,
				Parameters = parameters,
				ItemsTested = parameters.SampleSize,
				ExceptionsFound = 0,
				Outcome = TestOutcome.NoExceptions
			});
	}

	/// <summary>
	/// Answers a control it does not support with "tested, and it failed" — the bead's counterexample
	/// verbatim: ItemsTested = 0 (nothing was examined) while ExceptionsFound = 3 and the outcome is a
	/// control failure. Every field is internally consistent and the whole result is a fabrication.
	/// </summary>
	private sealed class AccusingValidator : StubValidator
	{
		public override Task<ControlTestResult> RunTestAsync(
			string controlId, ControlTestParameters parameters, CancellationToken cancellationToken) =>
			Task.FromResult(new ControlTestResult
			{
				ControlId = controlId,
				Parameters = parameters,
				ItemsTested = 0,
				ExceptionsFound = 3,
				Outcome = TestOutcome.ControlFailure
			});
	}

	/// <summary>The same fabrication reported as exceptions rather than an outright failure.</summary>
	private sealed class ExceptionReportingValidator : StubValidator
	{
		public override Task<ControlTestResult> RunTestAsync(
			string controlId, ControlTestParameters parameters, CancellationToken cancellationToken) =>
			Task.FromResult(new ControlTestResult
			{
				ControlId = controlId,
				Parameters = parameters,
				ItemsTested = 0,
				ExceptionsFound = 1,
				Outcome = TestOutcome.SignificantExceptions
			});
	}

	private sealed class HonestValidator : StubValidator
	{
		public override Task<ControlTestResult> RunTestAsync(
			string controlId, ControlTestParameters parameters, CancellationToken cancellationToken) =>
			Task.FromResult(new ControlTestResult
			{
				ControlId = controlId,
				Parameters = parameters,
				ItemsTested = 0,
				ExceptionsFound = null,
				Outcome = TestOutcome.NotTested,
				Notes = $"No validator supports {controlId}."
			});
	}
}
