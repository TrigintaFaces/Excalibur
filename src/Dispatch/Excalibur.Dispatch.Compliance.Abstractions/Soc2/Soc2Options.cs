// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration for SOC 2 compliance module.
/// </summary>
public sealed class Soc2Options
{
	/// <summary>
	/// Trust Services categories to enable.
	/// Default: Security only (minimum for SOC 2).
	/// </summary>
	public TrustServicesCategory[] EnabledCategories { get; set; } =
		[TrustServicesCategory.Security];

	/// <summary>
	/// Whether to enable continuous compliance monitoring.
	/// Default: true.
	/// </summary>
	public bool EnableContinuousMonitoring { get; set; } = true;

	/// <summary>
	/// Monitoring interval for control validation.
	/// Default: 1 hour.
	/// </summary>
	public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Whether to generate alerts for compliance gaps.
	/// Default: true.
	/// </summary>
	public bool EnableAlerts { get; set; } = true;

	/// <summary>
	/// Alert severity threshold.
	/// Default: Medium.
	/// </summary>
	public GapSeverity AlertThreshold { get; set; } = GapSeverity.Medium;

	/// <summary>
	/// Evidence retention period.
	/// Default: 7 years.
	/// </summary>
	public TimeSpan EvidenceRetentionPeriod { get; set; } = TimeSpan.FromDays(365 * 7);

	/// <summary>
	/// System description for reports.
	/// </summary>
	public SystemDescription? SystemDescription { get; set; }

	/// <summary>
	/// Custom control definitions.
	/// </summary>
	public List<ControlDefinition> CustomControls { get; set; } = [];

	/// <summary>
	/// Default sample size for control testing.
	/// </summary>
	public int DefaultTestSampleSize { get; set; } = 25;

	/// <summary>
	/// Minimum period length for Type II reports (days).
	/// </summary>
	public int MinimumTypeIIPeriodDays { get; set; } = 90;

	/// <summary>
	/// Whether to include sub-service organizations in reports.
	/// </summary>
	public bool IncludeSubServiceOrganizations { get; set; }
}

/// <summary>
/// Custom control definition for extending built-in controls.
/// </summary>
public sealed class ControlDefinition
{
	/// <summary>
	/// Control identifier.
	/// </summary>
	public required string ControlId { get; init; }

	/// <summary>
	/// Mapped criterion.
	/// </summary>
	public required TrustServicesCriterion Criterion { get; init; }

	/// <summary>
	/// Control name.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Control description.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Implementation details.
	/// </summary>
	public required string Implementation { get; init; }

	/// <summary>
	/// Control type.
	/// </summary>
	public ControlType Type { get; init; } = ControlType.Preventive;

	/// <summary>
	/// Control frequency.
	/// </summary>
	public ControlFrequency Frequency { get; init; } = ControlFrequency.Continuous;

	/// <summary>
	/// Validation function type name (must implement IControlValidator).
	/// </summary>
	public string? ValidatorTypeName { get; init; }
}
