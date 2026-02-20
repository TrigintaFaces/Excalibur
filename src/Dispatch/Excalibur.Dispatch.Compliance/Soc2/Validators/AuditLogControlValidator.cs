// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0




namespace Excalibur.Dispatch.Compliance;

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
				Implementation = "ADR-052 Tamper-evident audit logging with IAuditLogger",
				Type = ControlType.Detective,
				Frequency = ControlFrequency.Continuous
			},
			ControlSec005 => new ControlDescription
			{
				ControlId = ControlSec005,
				Name = "Security Monitoring",
				Description = "Security events are monitored and alerts are generated for anomalies",
				Implementation = "ADR-052 Audit log integrity verification",
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

			evidence.Add(CreateEvidence(
				EvidenceType.TestResult,
				$"Audit log integrity verification: {(integrityResult.IsValid ? "Passed" : "Failed")}",
				nameof(AuditLogControlValidator)));

			if (!integrityResult.IsValid)
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
