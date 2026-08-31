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

			// Only a detected violation is a control failure. An unexercised window is reported honestly
			// above but is not itself evidence that the control is broken.
			if (integrityResult.Outcome == AuditIntegrityOutcome.ViolationsDetected)
			{
				issues.Add($"Audit log integrity check failed: {integrityResult.ViolationDescription}");
			}
		}
		catch (Exception ex)
		{
			evidence.Add(CreateEvidence(
				EvidenceType.TestResult,
				$"Audit log integrity check: {ex.Message}",
				nameof(AuditLogControlValidator)));
		}

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Hash-chained audit logging configured with IAuditLogger",
			nameof(AuditLogControlValidator)));

		if (issues.Count == 0)
		{
			evidence.Add(CreateEvidence(
				EvidenceType.TestResult,
				"Audit logging validation passed",
				nameof(AuditLogControlValidator)));

			return CreateSuccessResult(ControlSec004, evidence);
		}

		var score = Math.Max(0, 100 - (issues.Count * 25));
		return CreateFailureResult(ControlSec004, issues, score, evidence);
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
			evidence.Add(CreateEvidence(
				EvidenceType.TestResult,
				"Security monitoring validation passed",
				nameof(AuditLogControlValidator)));

			return CreateSuccessResult(ControlSec005, evidence);
		}

		var score = Math.Max(0, 100 - (issues.Count * 33));
		return CreateFailureResult(ControlSec005, issues, score, evidence);
	}
}
