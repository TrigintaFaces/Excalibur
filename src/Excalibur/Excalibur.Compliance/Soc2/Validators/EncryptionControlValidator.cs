// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0




namespace Excalibur.Compliance.Soc2.Validators;

/// <summary>
/// Validates encryption controls (SEC-001, SEC-002, SEC-003).
/// Maps to CC6 (Logical Access), CC9 (Risk Mitigation).
/// </summary>
public sealed class EncryptionControlValidator : BaseControlValidator
{
	private const string ControlSec001 = "SEC-001"; // Encryption at Rest
	private const string ControlSec002 = "SEC-002"; // Encryption in Transit
	private const string ControlSec003 = "SEC-003"; // Key Management

	private readonly IEncryptionProvider? _encryptionProvider;
	private readonly IKeyManagementProvider? _keyManagementProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionControlValidator"/> class.
	/// </summary>
	/// <param name="encryptionProvider">Optional encryption provider.</param>
	/// <param name="keyManagementProvider">Optional key management provider.</param>
	public EncryptionControlValidator(
		IEncryptionProvider? encryptionProvider = null,
		IKeyManagementProvider? keyManagementProvider = null)
	{
		_encryptionProvider = encryptionProvider;
		_keyManagementProvider = keyManagementProvider;
	}

	/// <inheritdoc />
	public override IReadOnlyList<string> SupportedControls =>
		[ControlSec001, ControlSec002, ControlSec003];

	/// <inheritdoc />
	public override IReadOnlyList<TrustServicesCriterion> SupportedCriteria =>
		[TrustServicesCriterion.CC6_LogicalAccess, TrustServicesCriterion.CC9_RiskMitigation];

	/// <inheritdoc />
	public override async Task<ControlValidationResult> ValidateAsync(
		string controlId,
		CancellationToken cancellationToken)
	{
		return controlId switch
		{
			ControlSec001 => await ValidateEncryptionAtRestAsync(cancellationToken).ConfigureAwait(false),
			ControlSec002 => ValidateEncryptionInTransit(),
			ControlSec003 => await ValidateKeyManagementAsync(cancellationToken).ConfigureAwait(false),
			_ => CreateFailureResult(controlId, [$"Unknown control: {controlId}"])
		};
	}

	/// <inheritdoc />
	public override ControlDescription? GetControlDescription(string controlId)
	{
		return controlId switch
		{
			ControlSec001 => new ControlDescription
			{
				ControlId = ControlSec001,
				Name = "Encryption at Rest",
				Description = "Sensitive data is encrypted using AES-256-GCM when stored",
				Implementation = "Field Encryption with IEncryptionProvider",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.Continuous
			},
			ControlSec002 => new ControlDescription
			{
				ControlId = ControlSec002,
				Name = "Encryption in Transit",
				Description = "Data is encrypted using TLS 1.2+ during transmission",
				Implementation = "Transport layer TLS enforcement",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.Continuous
			},
			ControlSec003 => new ControlDescription
			{
				ControlId = ControlSec003,
				Name = "Key Management",
				Description = "Encryption keys are managed through a key management system",
				Implementation = "Key Management Provider integration",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.Continuous
			},
			_ => null
		};
	}

	private async Task<ControlValidationResult> ValidateEncryptionAtRestAsync(CancellationToken cancellationToken)
	{
		var issues = new List<string>();
		var evidence = new List<EvidenceItem>();

		if (_encryptionProvider == null)
		{
			issues.Add("Encryption provider not configured");
			return CreateFailureResult(ControlSec001, issues);
		}

		// Validate FIPS compliance if available
		try
		{
			var isFipsCompliant = await _encryptionProvider.ValidateFipsComplianceAsync(cancellationToken).ConfigureAwait(false);
			evidence.Add(CreateEvidence(
				EvidenceType.TestResult,
				// A FIPS check returning FALSE used to be recorded as "Not required", which is a different
				// claim entirely and the favourable one. It says what it found.
				$"FIPS 140-2 compliance validation: {(isFipsCompliant ? "Passed" : "Not FIPS 140-2 compliant")}",
				nameof(EncryptionControlValidator)));

			if (!isFipsCompliant)
			{
				issues.Add(
					"The encryption provider reported that it is not FIPS 140-2 compliant.");
			}
		}
		catch (Exception ex)
		{
			evidence.Add(CreateEvidence(
				EvidenceType.TestResult,
				$"FIPS validation check: {ex.Message}",
				nameof(EncryptionControlValidator)));

			// The check THREW. Recording the message as evidence and adding no issue let control reach
			// the success branch below, so a verification that failed to run returned a perfect score.
			issues.Add(
				"The FIPS 140-2 compliance check did not complete, so the encryption provider's compliance "
				+ "is unverified rather than established.");
		}

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"AES-256-GCM encryption provider configured",
			nameof(EncryptionControlValidator)));

		if (issues.Count == 0)
		{
			// "Encryption at rest validation passed" over-claimed what ran. The provider reported its own
			// FIPS compliance and the provider is configured; no stored record was read back and checked
			// for ciphertext. The check is real -- this control stays effective -- but the evidence must
			// say what was checked, because an assessor reads this line as the scope of the test.
			evidence.Add(CreateEvidence(
				EvidenceType.TestResult,
				"Encryption provider is configured and reported FIPS 140-2 compliance when queried; no "
				+ "stored record was read back to confirm data at rest is encrypted",
				nameof(EncryptionControlValidator)));

			return CreateSuccessResult(ControlSec001, evidence);
		}

		// Constructed rather than routed through CreateFailureResult for the same reason as SEC-004:
		// the helper sets IsConfigured = issues.Count == 0, so naming any issue makes the result claim
		// the encryption provider is not configured. It demonstrably is -- the null check above returned
		// long ago. Removable the moment the base gains a configured-but-unverified factory.
		return new ControlValidationResult
		{
			ControlId = ControlSec001,
			IsConfigured = true,
			IsEffective = false,
			EffectivenessScore = 50,
			ConfigurationIssues = issues,
			Evidence = evidence,
			ValidatedAt = DateTimeOffset.UtcNow
		};
	}

	private ControlValidationResult ValidateEncryptionInTransit()
	{
		var evidence = new List<EvidenceItem>();

		// The declared control is TLS enforcement at the transport layer, and nothing here observes it.
		// This method used to record an evidence item reading "TLS 1.2+ enforcement check" and return a
		// PASS, on the stated assumption that TLS is configured in production -- so an auditor received
		// an EFFECTIVE verdict with supporting evidence for a check that never ran. The sibling
		// ValidateKeyManagementAsync below already rejects that reasoning: it fails when the provider it
		// attests is absent rather than assuming one.
		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Transport security is terminated outside this framework and cannot be observed from here",
			nameof(EncryptionControlValidator)));

		// Partial score, matching the other controls whose mechanism may exist and is unverifiable here:
		// transport security MAY be enforced, and this framework cannot confirm it.
		return CreateFailureResult(
			ControlSec002,
			[
				"Transport encryption is enforced by the host and its transports, not by this framework, so it "
				+ "is unverified here; TLS terminated by a gateway, service mesh or load balancer requires "
				+ "independent attestation."
			],
			effectivenessScore: Soc2EffectivenessScore.Unverified,
			evidence);
	}

	private async Task<ControlValidationResult> ValidateKeyManagementAsync(CancellationToken cancellationToken)
	{
		var issues = new List<string>();
		var evidence = new List<EvidenceItem>();

		// Three different facts are reachable below -- an absent provider, a key we examined and found
		// missing or expired, and a check that threw before establishing anything. They were all fed
		// through one complaint count, so a key proven expired scored the same as a lookup that failed.
		var violationDetected = false;

		if (_keyManagementProvider == null)
		{
			issues.Add("Key management provider not configured");
			return CreateFailureResult(
				ControlSec003, issues, Soc2EffectivenessScore.MechanismAbsent);
		}

		try
		{
			// Check that we can get the current active key metadata
			var keyMetadata = await _keyManagementProvider.GetActiveKeyAsync(purpose: null, cancellationToken: cancellationToken).ConfigureAwait(false);

			if (keyMetadata == null)
			{
				issues.Add("No active encryption key available");
				violationDetected = true;
			}
			else
			{
				evidence.Add(CreateEvidence(
					EvidenceType.Configuration,
					$"Active key version: {keyMetadata.Version}, Status: {keyMetadata.Status}",
					nameof(EncryptionControlValidator)));

				// Validate key is not expired
				if (keyMetadata.ExpiresAt.HasValue && keyMetadata.ExpiresAt.Value < DateTimeOffset.UtcNow)
				{
					issues.Add($"Current encryption key expired at {keyMetadata.ExpiresAt.Value:O}");
					violationDetected = true;
				}
			}
		}
		catch (Exception ex)
		{
			issues.Add($"Failed to validate key management: {ex.Message}");
		}

		if (issues.Count == 0)
		{
			evidence.Add(CreateEvidence(
				EvidenceType.TestResult,
				"An active encryption key was retrieved and its expiry was checked against the current time",
				nameof(EncryptionControlValidator)));

			return CreateSuccessResult(ControlSec003, evidence);
		}

		// A check that threw leaves key management UNVERIFIED; a key examined and found missing or
		// expired is a finding, and a finding must never read as better than an open question.
		var score = violationDetected
			? Soc2EffectivenessScore.ViolationDetected
			: Soc2EffectivenessScore.Unverified;

		// The provider null-guard returned above, so key management IS configured; what varies is
		// whether we examined it and what we found.
		return CreateFailureResult(ControlSec003, issues, score, evidence, isConfigured: true);
	}
}
