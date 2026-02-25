// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Overall SOC 2 compliance status.
/// </summary>
public record ComplianceStatus
{
	/// <summary>
	/// Overall compliance level.
	/// </summary>
	public required ComplianceLevel OverallLevel { get; init; }

	/// <summary>
	/// Status for each Trust Services category.
	/// </summary>
	public required IReadOnlyDictionary<TrustServicesCategory, CategoryStatus> CategoryStatuses { get; init; }

	/// <summary>
	/// Detailed status for each criterion.
	/// </summary>
	public required IReadOnlyDictionary<TrustServicesCriterion, CriterionStatus> CriterionStatuses { get; init; }

	/// <summary>
	/// Active compliance gaps requiring attention.
	/// </summary>
	public IReadOnlyList<ComplianceGap> ActiveGaps { get; init; } = [];

	/// <summary>
	/// When the status was last evaluated.
	/// </summary>
	public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Tenant context (null for system-wide).
	/// </summary>
	public string? TenantId { get; init; }
}

/// <summary>
/// Compliance levels.
/// </summary>
public enum ComplianceLevel
{
	/// <summary>All controls effective, no gaps.</summary>
	FullyCompliant,

	/// <summary>Minor gaps, controls generally effective.</summary>
	SubstantiallyCompliant,

	/// <summary>Significant gaps requiring remediation.</summary>
	PartiallyCompliant,

	/// <summary>Critical gaps, immediate action required.</summary>
	NonCompliant,

	/// <summary>Status unknown, assessment needed.</summary>
	Unknown
}

/// <summary>
/// Status for a Trust Services category.
/// </summary>
public record CategoryStatus
{
	/// <summary>
	/// Category being evaluated.
	/// </summary>
	public required TrustServicesCategory Category { get; init; }

	/// <summary>
	/// Compliance level for this category.
	/// </summary>
	public required ComplianceLevel Level { get; init; }

	/// <summary>
	/// Percentage of criteria met (0-100).
	/// </summary>
	public required int CompliancePercentage { get; init; }

	/// <summary>
	/// Number of active controls.
	/// </summary>
	public required int ActiveControls { get; init; }

	/// <summary>
	/// Number of controls with issues.
	/// </summary>
	public required int ControlsWithIssues { get; init; }
}

/// <summary>
/// Status for a specific criterion.
/// </summary>
public record CriterionStatus
{
	/// <summary>
	/// Criterion being evaluated.
	/// </summary>
	public required TrustServicesCriterion Criterion { get; init; }

	/// <summary>
	/// Whether the criterion is met.
	/// </summary>
	public required bool IsMet { get; init; }

	/// <summary>
	/// Control effectiveness (0-100).
	/// </summary>
	public required int EffectivenessScore { get; init; }

	/// <summary>
	/// Last validation timestamp.
	/// </summary>
	public required DateTimeOffset LastValidated { get; init; }

	/// <summary>
	/// Evidence count for this criterion.
	/// </summary>
	public required int EvidenceCount { get; init; }

	/// <summary>
	/// Any gaps identified.
	/// </summary>
	public IReadOnlyList<string> Gaps { get; init; } = [];
}

/// <summary>
/// A compliance gap requiring remediation.
/// </summary>
public record ComplianceGap
{
	/// <summary>
	/// Gap identifier.
	/// </summary>
	public required string GapId { get; init; }

	/// <summary>
	/// Affected criterion.
	/// </summary>
	public required TrustServicesCriterion Criterion { get; init; }

	/// <summary>
	/// Description of the gap.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Severity level.
	/// </summary>
	public required GapSeverity Severity { get; init; }

	/// <summary>
	/// Recommended remediation.
	/// </summary>
	public required string Remediation { get; init; }

	/// <summary>
	/// When the gap was identified.
	/// </summary>
	public required DateTimeOffset IdentifiedAt { get; init; }

	/// <summary>
	/// Target remediation date.
	/// </summary>
	public DateTimeOffset? TargetRemediationDate { get; init; }
}

/// <summary>
/// Gap severity levels.
/// </summary>
public enum GapSeverity
{
	/// <summary>Minor issue, low risk.</summary>
	Low,

	/// <summary>Moderate issue, should be addressed.</summary>
	Medium,

	/// <summary>Significant issue, priority remediation.</summary>
	High,

	/// <summary>Critical issue, immediate action required.</summary>
	Critical
}
