// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0





namespace Excalibur.Compliance.Soc2.Validators;

/// <summary>
/// Validates confidentiality controls (CNF-001, CNF-002, CNF-003).
/// Maps to C1 (Data Classification), C2 (Data Protection), C3 (Data Disposal).
/// </summary>
public sealed class ConfidentialityControlValidator : BaseControlValidator
{
	private const string ControlCnf001 = "CNF-001"; // Data Classification
	private const string ControlCnf002 = "CNF-002"; // Data Protection
	private const string ControlCnf003 = "CNF-003"; // Data Disposal

	private readonly IEncryptionProvider? _encryptionProvider;
	private readonly IErasureService? _erasureService;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfidentialityControlValidator"/> class.
	/// </summary>
	/// <param name="encryptionProvider">Optional encryption provider.</param>
	/// <param name="erasureService">Optional erasure service.</param>
	public ConfidentialityControlValidator(
		IEncryptionProvider? encryptionProvider = null,
		IErasureService? erasureService = null)
	{
		_encryptionProvider = encryptionProvider;
		_erasureService = erasureService;
	}

	/// <inheritdoc />
	public override IReadOnlyList<string> SupportedControls =>
		[ControlCnf001, ControlCnf002, ControlCnf003];

	/// <inheritdoc />
	public override IReadOnlyList<TrustServicesCriterion> SupportedCriteria =>
		[
			TrustServicesCriterion.C1_DataClassification,
			TrustServicesCriterion.C2_DataProtection,
			TrustServicesCriterion.C3_DataDisposal
		];

	/// <inheritdoc />
	public override Task<ControlValidationResult> ValidateAsync(
		string controlId,
		CancellationToken cancellationToken)
	{
		_ = cancellationToken; // Reserved for future async operations

		return controlId switch
		{
			ControlCnf001 => Task.FromResult(ValidateDataClassification()),
			ControlCnf002 => Task.FromResult(ValidateDataProtection()),
			ControlCnf003 => Task.FromResult(ValidateDataDisposal()),
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
			ControlCnf001 => new ControlDescription
			{
				ControlId = ControlCnf001,
				Name = "Data Classification",
				Description = "Data is classified according to sensitivity using classification attributes",
				Implementation = "[PersonalData] and [Sensitive] attributes, the latter carrying a DataClassification level (Internal, Confidential or Restricted)",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.Continuous
			},
			ControlCnf002 => new ControlDescription
			{
				ControlId = ControlCnf002,
				Name = "Data Protection",
				Description = "Classified data is protected with field-level encryption",
				Implementation = "Field encryption based on classification level",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.Continuous
			},
			ControlCnf003 => new ControlDescription
			{
				ControlId = ControlCnf003,
				Name = "Data Disposal",
				Description = "Data disposal follows cryptographic erasure procedures",
				Implementation = "GDPR Right to Erasure with cryptographic key destruction",
				Type = ControlType.Corrective,
				Frequency = ControlFrequency.OnDemand
			},
			_ => null
		};
	}

	private ControlValidationResult ValidateDataClassification()
	{
		var evidence = new List<EvidenceItem>();

		// [PersonalData] and [Sensitive] are built in. Confidential is a DataClassification level
		// carried by [Sensitive], not an attribute of its own.

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Attribute-based classification available: [PersonalData], and [Sensitive] with a DataClassification level of Internal, Confidential or Restricted",
			nameof(ConfidentialityControlValidator)));

		// The Configuration evidence above is true: the capability is shipped. What is NOT
		// established is that this deployment operates it, and a TestResult item saying the
		// control was "verified" asserted a check that never ran. Offering a capability is not
		// operating a control.
		return CreateFailureResult(
			ControlCnf001,
			[
				"Classification attributes ship with the framework, but whether the consumer applied them to their personal data is not observable from here, so this control is unverified."
			],
			effectivenessScore: ControlEffectiveness.Unverified,
			evidence,
			// Same shape as the ProcessingIntegrity trio: the classification attributes ship here, so
			// reporting the capability absent contradicts the Configuration evidence just above.
			isConfigured: true);
	}

	private ControlValidationResult ValidateDataProtection()
	{
		var issues = new List<string>();
		var evidence = new List<EvidenceItem>();

		if (_encryptionProvider == null)
		{
			issues.Add("Encryption provider not configured for data protection");
			// No provider is registered, so the mechanism this control depends on is absent rather than
			// merely unexamined.
			return CreateFailureResult(ControlCnf002, issues, ControlEffectiveness.MechanismAbsent);
		}

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Field encryption service configured for protecting classified data",
			nameof(ConfidentialityControlValidator)));

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Field encryption service is available; no classified field was observed to be encrypted in "
			+ "this period",
			nameof(ConfidentialityControlValidator)));

		// The evidence above is true and stays: the seam is configured. What does NOT follow is that
		// the CONTROL operated. Presence of a component is not operation of a control, and this
		// returned effective at a score of 100 to an external assessor.
		return CreateFailureResult(
			ControlCnf002,
			[
				"An encryption service is available, but whether classified data is actually protected by "
				+ "it depends on the consumer applying classification, which is not observable from here, "
				+ "so this control is unverified."
			],
			effectivenessScore: ControlEffectiveness.Unverified,
			evidence,
			isConfigured: true);
	}

	private ControlValidationResult ValidateDataDisposal()
	{
		var issues = new List<string>();
		var evidence = new List<EvidenceItem>();

		if (_erasureService == null)
		{
			// CNF-003's DECLARED disposal control is automated cryptographic erasure. A null
			// IErasureService means that declared mechanism is ABSENT; returning PASS on unverifiable
			// "manual procedures apply" would launder a false-green. Surface the gap with visibility
			// instead (compliance-ASSISTANCE: honesty-of-signal is the value) so an auditor sees the
			// unverified control rather than a green that hides it.
			issues.Add(
				"Automated cryptographic erasure (IErasureService) not configured — the declared disposal "
				+ "control is unverified; manual/compensating procedures require independent attestation.");

			evidence.Add(CreateEvidence(
				EvidenceType.Configuration,
				"IErasureService not configured - the declared cryptographic-erasure disposal control is absent; manual procedures require independent attestation",
				nameof(ConfidentialityControlValidator)));

			// Partial score: a compensating manual procedure MAY exist, but the declared automated
			// control is absent and unverifiable here — not a full failure, not a pass.
			return CreateFailureResult(ControlCnf003, issues, effectivenessScore: ControlEffectiveness.Unverified, evidence);
		}

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Cryptographic erasure available via IErasureService",
			nameof(ConfidentialityControlValidator)));

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Cryptographic erasure infrastructure is available; no erasure was observed to be carried "
			+ "out in this period",
			nameof(ConfidentialityControlValidator)));

		// The evidence above is true and stays: the seam is configured. What does NOT follow is that
		// the CONTROL operated. Presence of a component is not operation of a control, and this
		// returned effective at a score of 100 to an external assessor.
		return CreateFailureResult(
			ControlCnf003,
			[
				"Cryptographic erasure is available, but no disposal was observed in this period, so the "
				+ "disposal control is unverified here and requires independent attestation."
			],
			effectivenessScore: ControlEffectiveness.Unverified,
			evidence,
			isConfigured: true);
	}
}
