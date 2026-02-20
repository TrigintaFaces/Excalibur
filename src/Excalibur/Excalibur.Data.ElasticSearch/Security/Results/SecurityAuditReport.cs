// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a comprehensive security audit report.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SecurityAuditReport" /> class.
/// </remarks>
/// <param name="reportId"> The unique identifier for this report. </param>
/// <param name="startTime"> The start time of the reporting period. </param>
/// <param name="endTime"> The end time of the reporting period. </param>
/// <param name="reportType"> The type of audit report. </param>
public sealed class SecurityAuditReport(Guid reportId, DateTimeOffset startTime, DateTimeOffset endTime, AuditReportType reportType)
{
	/// <summary>
	/// Gets the unique identifier for this report.
	/// </summary>
	/// <value>
	/// The unique identifier for this report.
	/// </value>
	public Guid ReportId { get; } = reportId;

	/// <summary>
	/// Gets the start time of the reporting period.
	/// </summary>
	/// <value>
	/// The start time of the reporting period.
	/// </value>
	public DateTimeOffset StartTime { get; } = startTime;

	/// <summary>
	/// Gets the end time of the reporting period.
	/// </summary>
	/// <value>
	/// The end time of the reporting period.
	/// </value>
	public DateTimeOffset EndTime { get; } = endTime;

	/// <summary>
	/// Gets the type of audit report.
	/// </summary>
	/// <value>
	/// The type of audit report.
	/// </value>
	public AuditReportType ReportType { get; } = reportType;

	/// <summary>
	/// Gets the timestamp when this report was generated.
	/// </summary>
	/// <value>
	/// The timestamp when this report was generated.
	/// </value>
	public DateTimeOffset GeneratedAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the total number of events analyzed.
	/// </summary>
	/// <value>
	/// The total number of events analyzed.
	/// </value>
	public long TotalEventsAnalyzed { get; init; }

	/// <summary>
	/// Gets the breakdown of events by type.
	/// </summary>
	/// <value>
	/// The breakdown of events by type.
	/// </value>
	public IReadOnlyDictionary<string, long> EventTypeBreakdown { get; init; } = new Dictionary<string, long>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the breakdown of events by severity.
	/// </summary>
	/// <value>
	/// The breakdown of events by severity.
	/// </value>
	public IReadOnlyDictionary<SecurityEventSeverity, long> SeverityBreakdown { get; init; } =
		new Dictionary<SecurityEventSeverity, long>();

	/// <summary>
	/// Gets the number of unique users involved in events.
	/// </summary>
	/// <value>
	/// The number of unique users involved in events.
	/// </value>
	public int UniqueUserCount { get; init; }

	/// <summary>
	/// Gets the number of unique source IP addresses.
	/// </summary>
	/// <value>
	/// The number of unique source IP addresses.
	/// </value>
	public int UniqueSourceIpCount { get; init; }

	/// <summary>
	/// Gets the security alerts generated during the period.
	/// </summary>
	/// <value>
	/// The security alerts generated during the period.
	/// </value>
	public IReadOnlyList<SecurityAlert> SecurityAlerts { get; init; } = Array.Empty<SecurityAlert>();

	/// <summary>
	/// Gets the compliance status summary.
	/// </summary>
	/// <value>
	/// The compliance status summary.
	/// </value>
	public IReadOnlyDictionary<string, object> ComplianceStatus { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the security recommendations based on the analysis.
	/// </summary>
	/// <value>
	/// The security recommendations based on the analysis.
	/// </value>
	public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Gets the summary statistics for the reporting period.
	/// </summary>
	/// <value>
	/// The summary statistics for the reporting period.
	/// </value>
	public IReadOnlyDictionary<string, object> Statistics { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets any errors or issues encountered during report generation.
	/// </summary>
	/// <value>
	/// Any errors or issues encountered during report generation.
	/// </value>
	public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
