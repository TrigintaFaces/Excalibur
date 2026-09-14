// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Compliance;

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
	[Required]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// System purpose and scope.
	/// </summary>
	[Required]
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Services provided.
	/// </summary>
	[Required]
	public IReadOnlyList<string> Services { get; set; } = [];

	/// <summary>
	/// Infrastructure components.
	/// </summary>
	[Required]
	public IReadOnlyList<string> Infrastructure { get; set; } = [];

	/// <summary>
	/// Data types processed.
	/// </summary>
	[Required]
	public IReadOnlyList<string> DataTypes { get; set; } = [];

	/// <summary>
	/// Third-party dependencies.
	/// </summary>
	public IReadOnlyList<string> ThirdParties { get; set; } = [];
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
	/// Whether this criterion was assessed, and if so what was concluded.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This was a <see langword="bool"/>, so a criterion nobody assessed and a criterion assessed
	/// and found wanting produced the same output: <b>Not Met</b>. Validators are opt-in, so a
	/// consumer without one registered for every criterion is the ordinary case rather than an
	/// error, and the unassessed criterion lowered their compliance percentage and could move the
	/// auditor's opinion on evidence that does not exist.
	/// </para>
	/// <para>
	/// Uses the same vocabulary as <see cref="CriterionStatus.Outcome"/> deliberately: one enum for
	/// one idea across the report and the status model, rather than a second spelling of it.
	/// </para>
	/// </remarks>
	public required CriterionOutcome Outcome { get; init; }

	/// <summary>
	/// The per-control validation verdicts this section's <see cref="Outcome"/> was derived from.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Without this, a section could report <see cref="CriterionOutcome.NotMet"/> and carry no evidence
	/// of WHY. The only per-control channel was <see cref="TestResult.Outcome"/>, which answers a
	/// different question -- what a test found -- so the effectiveness verdict was smuggled through it
	/// and the two facts corrupted each other: forcing a verdict into the test outcome made every
	/// control look like an exception, and reporting the test honestly as not-performed made a failing
	/// criterion report no findings at all.
	/// </para>
	/// <para>
	/// Separating them lets each be honest: the test outcome says no sample was drawn, and the
	/// verdict here says which controls were found ineffective. A section reporting
	/// <see cref="CriterionOutcome.NotMet"/> is therefore always able to name a cause.
	/// </para>
	/// </remarks>
	public IReadOnlyList<ControlValidationResult> ValidationResults { get; init; } = [];
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
	/// How many exceptions the test found, or <see langword="null"/> when no test ran.
	/// </summary>
	/// <remarks>
	/// A count of findings asserts that a search happened and returned that many. Where no test ran
	/// there is no such number, and zero would be the most favourable reading available — the one an
	/// assessor is most likely to act on. Absent is the honest value. Compare <see cref="SampleSize"/>,
	/// where zero is a true statement about how many items were examined and needs no other state.
	/// </remarks>
	public int? ExceptionsFound { get; init; }

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
	ControlFailure,

	/// <summary>
	/// The control was never exercised, so nothing is known about its effectiveness.
	/// <b>This is not a failure</b>: it must not be reported to an assessor as a control that was
	/// tested and failed, and it must not be counted as evidence of effectiveness. The accompanying
	/// <see cref="TestResult.Notes"/> states why no test ran.
	/// </summary>
	NotTested
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
	/// The control this exception is about, or <see langword="null"/> when the finding is about the
	/// criterion as a whole and names no single control.
	/// </summary>
	/// <value>
	/// A control identifier, or <see langword="null"/> for a criterion-level finding.
	/// </value>
	/// <remarks>
	/// Optional because a finding is not always attributable to one control: a criterion whose controls
	/// are collectively insufficient is a real finding that names none of them individually. Before this
	/// was expressible the producers wrote the string <c>"N/A"</c>, which a reader takes as a control
	/// whose identifier is literally "N/A" rather than as the absence of one — and a report is read by
	/// people and tools that cannot tell those apart. <see langword="null"/> is the absence; it is not a
	/// placeholder standing in for a value that exists.
	/// </remarks>
	public string? ControlId { get; init; }

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
