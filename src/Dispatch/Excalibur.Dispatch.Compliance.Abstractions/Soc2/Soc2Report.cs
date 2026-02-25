// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// SOC 2 compliance report.
/// </summary>
public record Soc2Report
{
	/// <summary>
	/// Report identifier.
	/// </summary>
	public required Guid ReportId { get; init; }

	/// <summary>
	/// Report type (Type I or Type II).
	/// </summary>
	public required Soc2ReportType ReportType { get; init; }

	/// <summary>
	/// Report title.
	/// </summary>
	public required string Title { get; init; }

	/// <summary>
	/// For Type I: the point-in-time date.
	/// For Type II: the period start date.
	/// </summary>
	public required DateTimeOffset PeriodStart { get; init; }

	/// <summary>
	/// For Type II: the period end date.
	/// For Type I: same as PeriodStart.
	/// </summary>
	public required DateTimeOffset PeriodEnd { get; init; }

	/// <summary>
	/// Trust Services categories included in the report.
	/// </summary>
	public required IReadOnlyList<TrustServicesCategory> CategoriesIncluded { get; init; }

	/// <summary>
	/// System description.
	/// </summary>
	public required SystemDescription System { get; init; }

	/// <summary>
	/// Control descriptions and test results.
	/// </summary>
	public required IReadOnlyList<ControlSection> ControlSections { get; init; }

	/// <summary>
	/// Overall auditor opinion.
	/// </summary>
	public required AuditorOpinion Opinion { get; init; }

	/// <summary>
	/// Exceptions or deviations noted.
	/// </summary>
	public IReadOnlyList<ReportException> Exceptions { get; init; } = [];

	/// <summary>
	/// Report generation timestamp.
	/// </summary>
	public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Tenant context (null for system-wide).
	/// </summary>
	public string? TenantId { get; init; }
}

/// <summary>
/// SOC 2 report types.
/// </summary>
public enum Soc2ReportType
{
	/// <summary>
	/// Type I: Point-in-time assessment of control design.
	/// </summary>
	TypeI,

	/// <summary>
	/// Type II: Period assessment of control design and operating effectiveness.
	/// </summary>
	TypeII
}

/// <summary>
/// System description for SOC 2 report.
/// </summary>
public record SystemDescription
{
	/// <summary>
	/// System name.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// System purpose and scope.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Services provided.
	/// </summary>
	public required IReadOnlyList<string> Services { get; init; }

	/// <summary>
	/// Infrastructure components.
	/// </summary>
	public required IReadOnlyList<string> Infrastructure { get; init; }

	/// <summary>
	/// Data types processed.
	/// </summary>
	public required IReadOnlyList<string> DataTypes { get; init; }

	/// <summary>
	/// Third-party dependencies.
	/// </summary>
	public IReadOnlyList<string> ThirdParties { get; init; } = [];
}

/// <summary>
/// Control section in SOC 2 report.
/// </summary>
public record ControlSection
{
	/// <summary>
	/// Trust Services criterion.
	/// </summary>
	public required TrustServicesCriterion Criterion { get; init; }

	/// <summary>
	/// Criterion description.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Controls implemented for this criterion.
	/// </summary>
	public required IReadOnlyList<ControlDescription> Controls { get; init; }

	/// <summary>
	/// Test results (Type II only).
	/// </summary>
	public IReadOnlyList<TestResult>? TestResults { get; init; }

	/// <summary>
	/// Section compliance status.
	/// </summary>
	public required bool IsMet { get; init; }
}

/// <summary>
/// Description of a specific control.
/// </summary>
public record ControlDescription
{
	/// <summary>
	/// Control identifier.
	/// </summary>
	public required string ControlId { get; init; }

	/// <summary>
	/// Control name.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Control description.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// How the control is implemented.
	/// </summary>
	public required string Implementation { get; init; }

	/// <summary>
	/// Control type (preventive, detective, corrective).
	/// </summary>
	public required ControlType Type { get; init; }

	/// <summary>
	/// Frequency of control operation.
	/// </summary>
	public required ControlFrequency Frequency { get; init; }
}

/// <summary>
/// Control types.
/// </summary>
public enum ControlType
{
	/// <summary>Prevents issues from occurring.</summary>
	Preventive,

	/// <summary>Detects issues when they occur.</summary>
	Detective,

	/// <summary>Corrects issues after detection.</summary>
	Corrective
}

/// <summary>
/// Control operation frequency.
/// </summary>
public enum ControlFrequency
{
	/// <summary>Operates continuously.</summary>
	Continuous,

	/// <summary>Operates on each transaction.</summary>
	PerTransaction,

	/// <summary>Operates daily.</summary>
	Daily,

	/// <summary>Operates weekly.</summary>
	Weekly,

	/// <summary>Operates monthly.</summary>
	Monthly,

	/// <summary>Operates quarterly.</summary>
	Quarterly,

	/// <summary>Operates annually.</summary>
	Annually,

	/// <summary>Operates on-demand.</summary>
	OnDemand
}

/// <summary>
/// Test result for Type II reports.
/// </summary>
public record TestResult
{
	/// <summary>
	/// Control being tested.
	/// </summary>
	public required string ControlId { get; init; }

	/// <summary>
	/// Test procedure description.
	/// </summary>
	public required string TestProcedure { get; init; }

	/// <summary>
	/// Sample size tested.
	/// </summary>
	public required int SampleSize { get; init; }

	/// <summary>
	/// Exceptions found.
	/// </summary>
	public required int ExceptionsFound { get; init; }

	/// <summary>
	/// Test result.
	/// </summary>
	public required TestOutcome Outcome { get; init; }

	/// <summary>
	/// Test notes.
	/// </summary>
	public string? Notes { get; init; }
}

/// <summary>
/// Test outcomes.
/// </summary>
public enum TestOutcome
{
	/// <summary>Control operated effectively.</summary>
	NoExceptions,

	/// <summary>Minor exceptions, control generally effective.</summary>
	MinorExceptions,

	/// <summary>Significant exceptions, control effectiveness impaired.</summary>
	SignificantExceptions,

	/// <summary>Control not operating effectively.</summary>
	ControlFailure
}

/// <summary>
/// Auditor opinion types.
/// </summary>
public enum AuditorOpinion
{
	/// <summary>Unqualified (clean) opinion.</summary>
	Unqualified,

	/// <summary>Qualified opinion (some exceptions).</summary>
	Qualified,

	/// <summary>Adverse opinion (significant issues).</summary>
	Adverse,

	/// <summary>Disclaimer (unable to form opinion).</summary>
	Disclaimer
}

/// <summary>
/// Exception noted in the report.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Represents a SOC2 report exception (audit finding), not a runtime exception.")]
public record ReportException
{
	/// <summary>
	/// Exception identifier.
	/// </summary>
	public required string ExceptionId { get; init; }

	/// <summary>
	/// Affected criterion.
	/// </summary>
	public required TrustServicesCriterion Criterion { get; init; }

	/// <summary>
	/// Affected control.
	/// </summary>
	public required string ControlId { get; init; }

	/// <summary>
	/// Exception description.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Management response.
	/// </summary>
	public string? ManagementResponse { get; init; }

	/// <summary>
	/// Remediation plan.
	/// </summary>
	public string? RemediationPlan { get; init; }
}
