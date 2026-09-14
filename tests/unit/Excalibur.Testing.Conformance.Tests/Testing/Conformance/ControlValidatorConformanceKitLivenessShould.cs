// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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

	[Fact]
	public async Task Accept_a_validator_that_reports_the_control_was_not_tested()
	{
		// The safety arm's partner: the check must not reject an honest implementation, or it would
		// simply be a different way of failing everyone.
		var kit = new KitOverHonestValidator();

		await kit.RunTestAsync_UnsupportedControl_ShouldNotFabricateAPass();
	}

	private sealed class KitOverFabricatingValidator : ControlValidatorConformanceTestKit
	{
		protected override IControlValidator CreateValidator() => new FabricatingValidator();
	}

	private sealed class KitOverHonestValidator : ControlValidatorConformanceTestKit
	{
		protected override IControlValidator CreateValidator() => new HonestValidator();
	}

	private abstract class StubValidator : IControlValidator
	{
		public IReadOnlyList<string> SupportedControls => ["CTRL-001"];

		public IReadOnlyList<TrustServicesCriterion> SupportedCriteria =>
			[TrustServicesCriterion.CC6_LogicalAccess];

		public Task<ControlValidationResult> ValidateAsync(
			string controlId, CancellationToken cancellationToken) =>
			Task.FromResult(new ControlValidationResult
			{
				ControlId = controlId,
				IsConfigured = false,
				IsEffective = false,
				EffectivenessScore = 0
			});

		public abstract Task<ControlTestResult> RunTestAsync(
			string controlId, ControlTestParameters parameters, CancellationToken cancellationToken);

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
