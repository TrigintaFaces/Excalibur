// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance.Soc2.Validators;

/// <summary>
/// Validates processing integrity controls (INT-001, INT-002, INT-003).
/// Maps to PI1 (Input Validation), PI2 (Processing Accuracy), PI3 (Output Completeness).
/// </summary>
public sealed class ProcessingIntegrityControlValidator : BaseControlValidator
{
	private const string ControlInt001 = "INT-001"; // Input Validation
	private const string ControlInt002 = "INT-002"; // Idempotency
	private const string ControlInt003 = "INT-003"; // Delivery Confirmation

	/// <inheritdoc />
	public override IReadOnlyList<string> SupportedControls =>
		[ControlInt001, ControlInt002, ControlInt003];

	/// <inheritdoc />
	public override IReadOnlyList<TrustServicesCriterion> SupportedCriteria =>
		[
			TrustServicesCriterion.PI1_InputValidation,
			TrustServicesCriterion.PI2_ProcessingAccuracy,
			TrustServicesCriterion.PI3_OutputCompleteness
		];

	/// <inheritdoc />
	public override Task<ControlValidationResult> ValidateAsync(
		string controlId,
		CancellationToken cancellationToken)
	{
		return controlId switch
		{
			ControlInt001 => Task.FromResult(ValidateInputValidation()),
			ControlInt002 => Task.FromResult(ValidateIdempotency()),
			ControlInt003 => Task.FromResult(ValidateDeliveryConfirmation()),
			// NotVerified, never the default score. A control this validator does not support was never
			// examined, so the honest outcome is "not assessed" -- Deficient means examined-and-failing and
			// reaches the assessor as a finding against the consumer.
			_ => Task.FromResult(
				CreateFailureResult(
					controlId,
					[$"Unknown control: {controlId}"],
					effectivenessScore: ControlEffectiveness.Unverified))
		};
	}

	/// <inheritdoc />
	public override ControlDescription? GetControlDescription(string controlId)
	{
		return controlId switch
		{
			ControlInt001 => new ControlDescription
			{
				ControlId = ControlInt001,
				Name = "Input Validation",
				Description = "All inputs are validated through the message pipeline before processing",
				Implementation = "Dispatch message validation pipeline with IDispatchMiddleware",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.PerTransaction
			},
			ControlInt002 => new ControlDescription
			{
				ControlId = ControlInt002,
				Name = "Idempotency",
				Description = "Message processing is idempotent to prevent duplicate processing",
				Implementation = "Outbox deduplication with message ID tracking",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.PerTransaction
			},
			ControlInt003 => new ControlDescription
			{
				ControlId = ControlInt003,
				Name = "Delivery Confirmation",
				Description = "Message delivery is confirmed with proof of delivery",
				Implementation = "Outbox pattern with delivery confirmation callbacks",
				Type = ControlType.Detective,
				Frequency = ControlFrequency.PerTransaction
			},
			_ => null
		};
	}

	private ControlValidationResult ValidateInputValidation()
	{
		var evidence = new List<EvidenceItem>();

		// The Excalibur framework provides input validation through the pipeline
		// This is a built-in feature when using the framework correctly

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Message validation pipeline available via IDispatchMiddleware",
			nameof(ProcessingIntegrityControlValidator)));

		// The Configuration evidence above is true: the capability is shipped. What is NOT
		// established is that this deployment operates it, and a TestResult item saying the
		// control was "verified" asserted a check that never ran. Offering a capability is not
		// operating a control.
		return CreateFailureResult(
			ControlInt001,
			[
				"The message validation pipeline ships with the framework but runs only where the consumer registered it, so its operation is unverified here and requires independent attestation."
			],
			effectivenessScore: ControlEffectiveness.Unverified,
			evidence,
			// The capability ships in this framework -- the Configuration evidence above says so. What is
			// unknown is whether the consumer wired it, and that is the OUTCOME, not the configuration.
			isConfigured: true);
	}

	private ControlValidationResult ValidateIdempotency()
	{
		var evidence = new List<EvidenceItem>();

		// The Outbox pattern provides idempotency guarantees
		// Message IDs are tracked to prevent duplicate processing

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Outbox pattern with message ID deduplication provides idempotency",
			nameof(ProcessingIntegrityControlValidator)));

		// The Configuration evidence above is true: the capability is shipped. What is NOT
		// established is that this deployment operates it, and a TestResult item saying the
		// control was "verified" asserted a check that never ran. Offering a capability is not
		// operating a control.
		return CreateFailureResult(
			ControlInt002,
			[
				"Outbox deduplication provides idempotency where an outbox is configured; the outbox is optional and a deployment dispatching in process has none, so this is unverified here."
			],
			effectivenessScore: ControlEffectiveness.Unverified,
			evidence,
			// The capability ships in this framework -- the Configuration evidence above says so. What is
			// unknown is whether the consumer wired it, and that is the OUTCOME, not the configuration.
			isConfigured: true);
	}

	private ControlValidationResult ValidateDeliveryConfirmation()
	{
		var evidence = new List<EvidenceItem>();

		// The Outbox pattern provides delivery confirmation
		// Messages are marked as delivered only after successful acknowledgment

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Outbox pattern tracks message delivery status with confirmation",
			nameof(ProcessingIntegrityControlValidator)));

		// The Configuration evidence above is true: the capability is shipped. What is NOT
		// established is that this deployment operates it, and a TestResult item saying the
		// control was "verified" asserted a check that never ran. Offering a capability is not
		// operating a control.
		return CreateFailureResult(
			ControlInt003,
			[
				"Delivery confirmation is tracked by the outbox where one is configured; the outbox is optional, so this control is unverified here and requires independent attestation."
			],
			effectivenessScore: ControlEffectiveness.Unverified,
			evidence,
			// The capability ships in this framework -- the Configuration evidence above says so. What is
			// unknown is whether the consumer wired it, and that is the OUTCOME, not the configuration.
			isConfigured: true);
	}
}
