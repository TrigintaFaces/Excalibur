// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0




namespace Excalibur.Compliance.Soc2.Validators;

/// <summary>
/// Validates audit logging controls (SEC-004, SEC-005).
/// Maps to CC1 (Control Environment), CC4 (Monitoring).
/// </summary>
public sealed class AuditLogControlValidator : BaseControlValidator
{
	private const string ControlSec004 = "SEC-004"; // Audit Logging
	private const string ControlSec005 = "SEC-005"; // Security Monitoring

	private readonly IAuditLogger? _auditLogger;
	private readonly IAuditStore? _auditStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditLogControlValidator"/> class.
	/// </summary>
	/// <param name="auditLogger">Optional audit logger.</param>
	/// <param name="auditStore">Optional audit store.</param>
	public AuditLogControlValidator(
		IAuditLogger? auditLogger = null,
		IAuditStore? auditStore = null)
	{
		_auditLogger = auditLogger;
		_auditStore = auditStore;
	}

	/// <inheritdoc />
	public override IReadOnlyList<string> SupportedControls =>
		[ControlSec004, ControlSec005];

	/// <inheritdoc />
	public override IReadOnlyList<TrustServicesCriterion> SupportedCriteria =>
		[TrustServicesCriterion.CC1_ControlEnvironment, TrustServicesCriterion.CC4_Monitoring];

	/// <inheritdoc />
	public override async Task<ControlValidationResult> ValidateAsync(
		string controlId,
		CancellationToken cancellationToken)
	{
		return controlId switch
		{
			ControlSec004 => await ValidateAuditLoggingAsync(cancellationToken).ConfigureAwait(false),
			ControlSec005 => ValidateSecurityMonitoring(),
			_ => CreateFailureResult(controlId, [$"Unknown control: {controlId}"])
		};
	}

	/// <inheritdoc />
	public override ControlDescription? GetControlDescription(string controlId)
	{
		return controlId switch
		{
			ControlSec004 => new ControlDescription
			{
				ControlId = ControlSec004,
				Name = "Audit Logging",
				Description = "Security-relevant events are logged with tamper-evident hash chains",
				Implementation = "Tamper-evident audit logging with IAuditLogger",
				Type = ControlType.Detective,
				Frequency = ControlFrequency.Continuous
			},
			ControlSec005 => new ControlDescription
			{
				ControlId = ControlSec005,
				Name = "Security Monitoring",
				Description = "Security events are monitored and alerts are generated for anomalies",
				Implementation = "Audit log integrity verification",
				Type = ControlType.Detective,
				Frequency = ControlFrequency.Continuous
			},
			_ => null
		};
	}

	private async Task<ControlValidationResult> ValidateAuditLoggingAsync(CancellationToken cancellationToken)
	{
		var issues = new List<string>();
		// Set when the integrity check did not run, did not interpret, or had nothing to examine.
		var unverified = false;
		var evidence = new List<EvidenceItem>();

		if (_auditLogger == null)
		{
			issues.Add("Audit logger not configured");
			return CreateFailureResult(ControlSec004, issues);
		}

		// Verify integrity of recent audit logs
		try
		{
			var endDate = DateTimeOffset.UtcNow;
			var startDate = endDate.AddDays(-1); // Check last 24 hours

			var integrityResult = await _auditLogger.VerifyIntegrityAsync(startDate, endDate, cancellationToken).ConfigureAwait(false);

			// Integrity verification has three outcomes and the evidence record must distinguish all three.
			// A window that contained no audit events establishes nothing about the log; reporting it as
			// "Passed" would put an unearned assurance in front of an external auditor.
			//
			// The pass and failure lines split again on whether the trail was hash-chained, because the
			// quantity reported is in a different unit in each case and the auditor cannot see the store
			// setting that decides it. An unchained trail is reported in its own vocabulary rather than
			// through the chained one: it establishes each record's own content integrity and says nothing
			// about deletion, insertion or reordering, and "0 compromised chains" would read as evidence
			// against exactly the tampering that was never tested.
			var integrityEvidence = integrityResult.Outcome switch
			{
				AuditIntegrityOutcome.Verified when integrityResult.IsHashChained =>
					$"Audit log integrity verification: Passed ({integrityResult.EventsVerified} events verified; "
					+ "the trail is hash-chained, so deletion, insertion and reordering were tested)",

				AuditIntegrityOutcome.Verified =>
					$"Audit log integrity verification: Partially exercised. Each of "
					+ $"{integrityResult.EventsVerified} records verified against its own stored signature, so "
					+ "record contents were not altered. The trail is NOT hash-chained, so deletion, insertion "
					+ "and reordering were not tested and this period provides no evidence against them.",

				AuditIntegrityOutcome.ViolationsDetected when integrityResult.IsHashChained =>
					$"Audit log integrity verification: Failed. {integrityResult.CompromisedChainCount} audit "
					+ $"chain(s) compromised across {integrityResult.EventsVerified} records verified. The "
					+ $"earliest altered record is {integrityResult.FirstViolationEventId} "
					+ $"({integrityResult.ViolationDescription}). Records following a break within a "
					+ "compromised chain cannot be independently verified, so the number of chains is not the "
					+ "number of altered records.",

				AuditIntegrityOutcome.ViolationsDetected =>
					$"Audit log integrity verification: Failed. {integrityResult.CompromisedChainCount} "
					+ $"record(s) failed content verification across {integrityResult.EventsVerified} records "
					+ $"verified. The earliest altered record is {integrityResult.FirstViolationEventId} "
					+ $"({integrityResult.ViolationDescription}). The trail is NOT hash-chained, so deletion, "
					+ "insertion and reordering were not tested in addition.",

				AuditIntegrityOutcome.NoEventsInScope =>
					"Audit log integrity verification: Not exercised. No audit events were recorded in the "
					+ "verification window, so this period provides no evidence of audit log integrity. An "
					+ "unexpectedly empty window may indicate that audit events are not reaching the store.",

				_ => "Audit log integrity verification: Not interpretable. The verification returned an "
					+ "unrecognized outcome and no conclusion about audit log integrity follows from it."
			};

			evidence.Add(CreateEvidence(
				EvidenceType.TestResult,
				integrityEvidence,
				nameof(AuditLogControlValidator)));

			// Only a detected violation is a control FAILURE -- that reasoning was already here and is
			// kept. What it missed is that "not a failure" is not "a pass". An unexercised window left
			// the issue list empty and fell through to the success branch below, which reports the
			// control effective at a score of 100 with "Audit logging validation passed" -- directly
			// contradicting the evidence line above it, which says the period provides no evidence.
			if (integrityResult.Outcome == AuditIntegrityOutcome.ViolationsDetected)
			{
				issues.Add($"Audit log integrity check failed: {integrityResult.ViolationDescription}");
			}
			else if (integrityResult.Outcome != AuditIntegrityOutcome.Verified)
			{
				unverified = true;
			}
		}
		catch (Exception ex)
		{
			evidence.Add(CreateEvidence(
				EvidenceType.TestResult,
				$"Audit log integrity check: {ex.Message}",
				nameof(AuditLogControlValidator)));

			// A check that threw recorded its message as evidence and added no issue, so it also
			// reached the pass below. The integrity of the trail is unknown here, not intact.
			unverified = true;
		}

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Hash-chained audit logging configured with IAuditLogger",
			nameof(AuditLogControlValidator)));

		if (issues.Count == 0 && unverified)
		{
			// Not a failure and not a pass. The configured mechanism may well be intact; this run did
			// not establish that it is, and an auditor must be able to tell the two apart.
			//
			// This was hand-built rather than routed through the shared helper, because the helper set
			// IsConfigured from the complaint count and so turned any reason given here into a claim that
			// the audit logger is absent -- which it demonstrably is not, the null check above having
			// returned long ago. The helper now takes that fact from the score band instead, so the
			// workaround is gone and this site says the same three things through the ordinary path.
			return CreateFailureResult(
				ControlSec004,
				[
					"Audit log integrity was not verified in this window, so this period evidences that audit "
					+ "logging is configured but not that the trail is intact."
				],
				effectivenessScore: Soc2EffectivenessScore.Unverified,
				evidence,
				isConfigured: true);
		}

		if (issues.Count == 0)
		{
			evidence.Add(CreateEvidence(
				EvidenceType.TestResult,
				"Audit log integrity was verified over the reporting window and the hash chain was intact",
				nameof(AuditLogControlValidator)));

			return CreateSuccessResult(ControlSec004, evidence);
		}

		// The only issue reachable here is a DETECTED integrity violation -- the trail was examined and
		// found tampered. Counting the remark gave that 75, which outranked a control nobody could check.
		return CreateFailureResult(
			ControlSec004, issues, Soc2EffectivenessScore.ViolationDetected, evidence, isConfigured: true);
	}

	private ControlValidationResult ValidateSecurityMonitoring()
	{
		var issues = new List<string>();
		var evidence = new List<EvidenceItem>();

		if (_auditStore == null)
		{
			// Audit store is optional - can use logger-based monitoring
			evidence.Add(CreateEvidence(
				EvidenceType.Configuration,
				"Audit store not configured - using logger-based monitoring",
				nameof(AuditLogControlValidator)));
		}
		else
		{
			evidence.Add(CreateEvidence(
				EvidenceType.Configuration,
				"Audit store configured for security monitoring queries",
				nameof(AuditLogControlValidator)));
		}

		// Check that basic monitoring infrastructure exists
		if (_auditLogger == null && _auditStore == null)
		{
			issues.Add("No audit infrastructure configured for security monitoring");
		}

		if (issues.Count == 0)
		{
			// The only thing established above is that a logger or a store is non-null. "Security
			// monitoring validation passed" claimed an exercise that never ran -- no alert was raised, no
			// query was issued, nothing was monitored. Presence of the seam is not operation of it.
			evidence.Add(CreateEvidence(
				EvidenceType.Configuration,
				"Audit infrastructure is present for security monitoring; no monitoring activity was "
				+ "exercised in this period",
				nameof(AuditLogControlValidator)));

			return CreateFailureResult(
				ControlSec005,
				[
					"Audit infrastructure is present, but no security-monitoring activity was exercised in "
					+ "this period, so this control is unverified and requires independent attestation."
				],
				effectivenessScore: Soc2EffectivenessScore.Unverified,
				evidence,
				isConfigured: true);
		}

		// The sole issue reachable here is that NO audit infrastructure is configured. That is an absent
		// mechanism, not a partial one, and it was scoring 67 of 100.
		return CreateFailureResult(
			ControlSec005, issues, Soc2EffectivenessScore.MechanismAbsent, evidence);
	}
}
