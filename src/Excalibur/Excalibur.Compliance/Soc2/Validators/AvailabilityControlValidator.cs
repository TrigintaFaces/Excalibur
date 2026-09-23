// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance.Soc2.Validators;

/// <summary>
/// Validates availability controls (AVL-001, AVL-002, AVL-003).
/// Maps to A1 (Infrastructure Management), A2 (Capacity Management), A3 (Backup/Recovery).
/// </summary>
public sealed class AvailabilityControlValidator : BaseControlValidator
{
	private const string ControlAvl001 = "AVL-001"; // Health Monitoring
	private const string ControlAvl002 = "AVL-002"; // Performance Metrics
	private const string ControlAvl003 = "AVL-003"; // Backup Verification

	private readonly IComplianceMetrics? _complianceMetrics;
	private readonly IBackupConfigurationProvider? _backupConfigProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="AvailabilityControlValidator"/> class.
	/// </summary>
	/// <param name="complianceMetrics">Optional compliance metrics service.</param>
	/// <param name="backupConfigProvider">Optional backup configuration provider for backup verification.</param>
	public AvailabilityControlValidator(
		IComplianceMetrics? complianceMetrics = null,
		IBackupConfigurationProvider? backupConfigProvider = null)
	{
		_complianceMetrics = complianceMetrics;
		_backupConfigProvider = backupConfigProvider;
	}

	/// <inheritdoc />
	public override IReadOnlyList<string> SupportedControls =>
		[ControlAvl001, ControlAvl002, ControlAvl003];

	/// <inheritdoc />
	public override IReadOnlyList<TrustServicesCriterion> SupportedCriteria =>
		[
			TrustServicesCriterion.A1_InfrastructureManagement,
			TrustServicesCriterion.A2_CapacityManagement,
			TrustServicesCriterion.A3_BackupRecovery
		];

	/// <inheritdoc />
	public override Task<ControlValidationResult> ValidateAsync(
		string controlId,
		CancellationToken cancellationToken)
	{
		_ = cancellationToken; // Reserved for future async operations

		return controlId switch
		{
			ControlAvl001 => Task.FromResult(ValidateHealthMonitoring()),
			ControlAvl002 => Task.FromResult(ValidatePerformanceMetrics()),
			ControlAvl003 => Task.FromResult(ValidateBackupVerification()),
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
			ControlAvl001 => new ControlDescription
			{
				ControlId = ControlAvl001,
				Name = "Health Monitoring",
				Description = "System health is continuously monitored via health check endpoints",
				Implementation = "ASP.NET Core Health Checks infrastructure",
				Type = ControlType.Detective,
				Frequency = ControlFrequency.Continuous
			},
			ControlAvl002 => new ControlDescription
			{
				ControlId = ControlAvl002,
				Name = "Performance Metrics",
				Description = "Performance metrics are collected and monitored for capacity planning",
				Implementation = "IComplianceMetrics with OpenTelemetry integration",
				Type = ControlType.Detective,
				Frequency = ControlFrequency.Continuous
			},
			ControlAvl003 => new ControlDescription
			{
				ControlId = ControlAvl003,
				Name = "Backup Verification",
				Description = "Backups and snapshots are verified for recoverability",
				Implementation = "Event store snapshot validation",
				Type = ControlType.Detective,
				Frequency = ControlFrequency.Daily
			},
			_ => null
		};
	}

	private ControlValidationResult ValidateHealthMonitoring()
	{
		var evidence = new List<EvidenceItem>();

		// Health monitoring is typically done via ASP.NET Core HealthChecks
		// This validator confirms monitoring infrastructure exists

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			"Health monitoring check - ASP.NET Core Health Checks recommended",
			nameof(AvailabilityControlValidator)));

		// The Configuration evidence above is true: the capability is shipped. What is NOT
		// established is that this deployment operates it, and a TestResult item saying the
		// control was "verified" asserted a check that never ran. Offering a capability is not
		// operating a control.
		return CreateFailureResult(
			ControlAvl001,
			[
				"Health monitoring is performed by the host, typically ASP.NET Core health checks, and is not observable from this framework, so it is unverified here."
			],
			effectivenessScore: ControlEffectiveness.Unverified,
			evidence);
	}

	private ControlValidationResult ValidatePerformanceMetrics()
	{
		var issues = new List<string>();
		var evidence = new List<EvidenceItem>();

		if (_complianceMetrics == null)
		{
			// "using external monitoring" was an assumption recorded as evidence — nothing here observes
			// such a system. Same shape as AVL-003 above: the declared control is absent, so the honest
			// signal is an unverified control with the gap named, not a pass.
			issues.Add(
				"Compliance metrics provider (IComplianceMetrics) not registered — availability monitoring is "
				+ "unverified; monitoring performed by an external system requires independent attestation.");

			evidence.Add(CreateEvidence(
				EvidenceType.Configuration,
				"IComplianceMetrics not configured - availability monitoring cannot be observed from this framework",
				nameof(AvailabilityControlValidator)));

			// Partial score, matching AVL-003: external monitoring MAY exist and cannot be confirmed here.
			return CreateFailureResult(ControlAvl002, issues, effectivenessScore: ControlEffectiveness.Unverified, evidence);
		}

		{
			evidence.Add(CreateEvidence(
				EvidenceType.Configuration,
				"IComplianceMetrics configured for performance monitoring",
				nameof(AvailabilityControlValidator)));
		}

		evidence.Add(CreateEvidence(
			EvidenceType.TestResult,
			"Compliance-metrics provider is registered and available for availability monitoring — this "
				+ "evidences that the monitoring seam is configured, not that any availability target was met.",
			nameof(AvailabilityControlValidator)));

		// The evidence above is true and stays: the seam is configured. What does NOT follow is that
		// the CONTROL operated. Presence of a component is not operation of a control, and this
		// returned effective at a score of 100 to an external assessor.
		return CreateFailureResult(
			ControlAvl002,
			[
				"A compliance-metrics provider is registered, but no availability target was observed to be "
				+ "met in this period, so availability is unverified here and requires independent "
				+ "attestation."
			],
			effectivenessScore: ControlEffectiveness.Unverified,
			evidence,
			isConfigured: true);
	}

	private ControlValidationResult ValidateBackupVerification()
	{
		var issues = new List<string>();
		var evidence = new List<EvidenceItem>();

		// Check if backup infrastructure is configured via DI (Option C: configuration verification only)
		if (_backupConfigProvider == null || !_backupConfigProvider.IsBackupConfigured)
		{
			evidence.Add(CreateEvidence(
				EvidenceType.Configuration,
				_backupConfigProvider == null
					? "IBackupConfigurationProvider not registered in dependency injection container"
					: "Backup infrastructure not configured",
				nameof(AvailabilityControlValidator)));

			evidence.Add(CreateEvidence(
				EvidenceType.Configuration,
				"Recommendation: Configure backup infrastructure for automated backup verification. " +
				"For event sourcing, register ISnapshotStore (e.g., SqlServerSnapshotStore). " +
				"For traditional databases, configure database backup agents or cloud backup services.",
				nameof(AvailabilityControlValidator)));

			// AVL-003's DECLARED control is verified backup. A null IBackupConfigurationProvider, or one
			// reporting no configuration, means that declared mechanism is ABSENT here. Returning PASS on
			// unverifiable "backups may be taken externally" would launder a false-green into an auditor's
			// evidence pack. Surface the gap instead, so an assessor sees an unverified control rather than
			// a green that hides one.
			issues.Add(
				_backupConfigProvider == null
					? "Backup configuration provider (IBackupConfigurationProvider) not registered — the declared "
						+ "backup-verification control is unverified; external backup arrangements require "
						+ "independent attestation."
					: "Backup infrastructure reports itself not configured — the declared backup-verification "
						+ "control is unverified; external backup arrangements require independent attestation.");

			// Partial score: a compensating external backup arrangement MAY exist, but the declared control
			// is absent and unverifiable here — not a full failure, not a pass.
			return CreateFailureResult(ControlAvl003, issues, effectivenessScore: ControlEffectiveness.Unverified, evidence);
		}

		// Backup infrastructure is configured - report positive evidence
		var providerName = _backupConfigProvider.BackupProviderName ?? "Unknown";
		var configDescription = _backupConfigProvider.ConfigurationDescription;

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			$"Backup infrastructure configured: {providerName}",
			nameof(AvailabilityControlValidator)));

		evidence.Add(CreateEvidence(
			EvidenceType.Configuration,
			configDescription,
			nameof(AvailabilityControlValidator)));

		evidence.Add(CreateEvidence(
			EvidenceType.TestResult,
			$"Backup verification infrastructure present ({providerName}). No backup was enumerated or "
			+ "restored, so this evidences the mechanism and not the control.",
			nameof(AvailabilityControlValidator)));

		// The evidence above is true and stays: the seam is configured. What does NOT follow is that
		// the CONTROL operated. Presence of a component is not operation of a control, and this
		// returned effective at a score of 100 to an external assessor.
		return CreateFailureResult(
			ControlAvl003,
			[
				"Backup infrastructure is configured, but no backup was enumerated or restored in this "
				+ "period, so the backup control is unverified here and requires independent attestation."
			],
			effectivenessScore: ControlEffectiveness.Unverified,
			evidence,
			isConfigured: true);
	}
}
