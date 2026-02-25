// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents an audit report.
/// </summary>
public sealed class AuditReport
{
	/// <summary>
	/// Gets or sets the unique identifier for the audit report.
	/// </summary>
	/// <value> The unique report identifier. </value>
	public Guid ReportId { get; set; }

	/// <summary>
	/// Gets or sets the start time for the audit period covered by this report.
	/// </summary>
	/// <value> The start timestamp for the audit period. </value>
	public DateTimeOffset StartTime { get; set; }

	/// <summary>
	/// Gets or sets the end time for the audit period covered by this report.
	/// </summary>
	/// <value> The end timestamp for the audit period. </value>
	public DateTimeOffset EndTime { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when this audit report was generated.
	/// </summary>
	/// <value> The report generation timestamp. </value>
	public DateTimeOffset GeneratedAt { get; set; }

	/// <summary>
	/// Gets or sets the type of audit report.
	/// </summary>
	/// <value> The audit report type classification. </value>
	public AuditReportType ReportType { get; set; }

	/// <summary>
	/// Gets or sets the total number of audit events included in this report.
	/// </summary>
	/// <value> The total count of audit events. </value>
	public int TotalEvents { get; set; }

	/// <summary>
	/// Gets or sets the breakdown of events by event type.
	/// </summary>
	/// <value> A dictionary mapping event types to their occurrence counts. </value>
	public Dictionary<string, int> EventBreakdown { get; set; } = [];

	/// <summary>
	/// Gets or sets the breakdown of events by severity level.
	/// </summary>
	/// <value> A dictionary mapping severity levels to their occurrence counts. </value>
	public Dictionary<string, int> SeverityBreakdown { get; set; } = [];

	/// <summary>
	/// Gets or sets the number of unique users involved in the audit events.
	/// </summary>
	/// <value> The count of unique users. </value>
	public int UniqueUsers { get; set; }

	/// <summary>
	/// Gets or sets the number of unique source IP addresses involved in the audit events.
	/// </summary>
	/// <value> The count of unique source IP addresses. </value>
	public int UniqueSourceIps { get; set; }

	/// <summary>
	/// Gets or sets the compliance status information for various regulatory frameworks.
	/// </summary>
	/// <value> A dictionary containing compliance status data. </value>
	public Dictionary<string, object> ComplianceStatus { get; set; } = [];

	/// <summary>
	/// Gets or sets the list of security recommendations based on the audit findings.
	/// </summary>
	/// <value> A list of recommended security actions or improvements. </value>
	public List<string> Recommendations { get; set; } = [];
}
