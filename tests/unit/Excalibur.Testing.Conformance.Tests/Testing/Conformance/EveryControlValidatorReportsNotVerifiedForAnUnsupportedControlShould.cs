// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Soc2.Validators;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Every shipped control validator reports <see cref="ControlOutcome.NotVerified"/> for a control it does
/// not support — never <see cref="ControlOutcome.Deficient"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why this exists separately from the conformance kit.</strong> The kit binds this property, but it
/// is derived by exactly ONE deriver, which constructs exactly ONE validator. Five validators ship. So the
/// kit's arm proved the property for one of five, and the other four were corrected with nothing binding
/// them — a fix whose regression would be silent on four fifths of the population.
/// </para>
/// <para>
/// <strong>The property, and why it is the severe one.</strong> A validator that does not support a control
/// never examined it, so neither verdict is available to it. <c>Effective</c> is an assurance nobody earned;
/// <c>Deficient</c> is an accusation nobody earned, and it is the more damaging, because a deficiency
/// travels into the SOC 2 report a consumer hands an external assessor as a finding against them. The type's
/// own ordering says so: <c>Deficient</c> is worse than <c>NotVerified</c>.
/// </para>
/// <para>
/// <strong>The defect this RED-detects.</strong> Each validator's unknown-control arm calls
/// <c>CreateFailureResult</c>, whose <c>effectivenessScore</c> parameter defaults to <c>0</c> —
/// <c>MechanismAbsent</c> — which maps to <c>Deficient</c>. Omitting one optional argument therefore
/// produced the worst available verdict about work that never happened. Restore any of those five omissions
/// and the corresponding case here goes RED.
/// </para>
/// <para>
/// This asserts the PROPERTY over the whole population rather than re-testing the kit. It is deliberately
/// not a fifth kit deriver: whether all five validators should be held to the full conformance contract is
/// a larger question than this defect, and answering it here would bundle an unrelated decision into a
/// regression lock.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "VALIDATOR")]
public sealed class EveryControlValidatorReportsNotVerifiedForAnUnsupportedControlShould
{
	/// <summary>An identifier no shipped validator declares, so every one of them must decline it.</summary>
	private const string UnsupportedControlId = "UNKNOWN-001";

	/// <summary>Every shipped validator, constructed as its DI registration constructs it.</summary>
	/// <returns>One case per validator, named so a failure identifies the validator without a debugger.</returns>
	public static TheoryData<string, IControlValidator> AllShippedValidators() =>
		new()
		{
			{ nameof(AuditLogControlValidator), new AuditLogControlValidator() },
			{ nameof(AvailabilityControlValidator), new AvailabilityControlValidator() },
			{ nameof(ConfidentialityControlValidator), new ConfidentialityControlValidator() },
			{ nameof(EncryptionControlValidator), new EncryptionControlValidator() },
			{ nameof(ProcessingIntegrityControlValidator), new ProcessingIntegrityControlValidator() },
		};

	[Theory]
	[MemberData(nameof(AllShippedValidators))]
	public async Task ReportNotVerified_NeverDeficient_ForAControlItDoesNotSupport(
		string validatorName,
		IControlValidator validator)
	{
		// Guard the premise before judging the outcome. If a validator ever declares this identifier, the
		// case below stops being about an UNSUPPORTED control and silently starts asserting something else.
		validator.SupportedControls.ShouldNotContain(
			UnsupportedControlId,
			$"{validatorName} declares {UnsupportedControlId}, so this case no longer exercises an "
			+ "unsupported control and proves nothing about the property it was written for");

		var result = await validator.ValidateAsync(UnsupportedControlId, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldNotBeNull($"{validatorName} returned no result for an unsupported control");

		result.Outcome.ShouldBe(
			ControlOutcome.NotVerified,
			$"{validatorName} reported {result.Outcome} for a control it does not support. It never examined "
			+ "that control, so it may claim neither verdict: Effective is an assurance nobody earned, and "
			+ "Deficient is an accusation nobody earned that reaches the assessor as a finding against the "
			+ "consumer. NotVerified is the only outcome available here.");
	}
}
