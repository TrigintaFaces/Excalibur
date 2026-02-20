// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a compliance report.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ComplianceReport" /> class.
/// </remarks>
/// <param name="framework"> The compliance framework that was evaluated. </param>
/// <param name="startTime"> The start time of the compliance analysis period. </param>
/// <param name="endTime"> The end time of the compliance analysis period. </param>
/// <param name="totalEventsAnalyzed"> The total number of events that were analyzed for compliance. </param>
/// <param name="violations"> The list of compliance violations found during the analysis. </param>
/// <param name="details"> Additional details and metadata about the compliance report. </param>
public sealed class ComplianceReport(
	ComplianceFramework framework,
	DateTimeOffset startTime,
	DateTimeOffset endTime,
	int totalEventsAnalyzed,
	List<ComplianceViolation> violations,
	Dictionary<string, object> details)
{
	/// <summary>
	/// Gets the compliance framework that was evaluated in this report.
	/// </summary>
	/// <value> The compliance framework type (GDPR, HIPAA, SOX, etc.). </value>
	public ComplianceFramework Framework { get; } = framework;

	/// <summary>
	/// Gets the start time of the compliance analysis period.
	/// </summary>
	/// <value> The timestamp marking the beginning of the compliance evaluation period. </value>
	public DateTimeOffset StartTime { get; } = startTime;

	/// <summary>
	/// Gets the end time of the compliance analysis period.
	/// </summary>
	/// <value> The timestamp marking the end of the compliance evaluation period. </value>
	public DateTimeOffset EndTime { get; } = endTime;

	/// <summary>
	/// Gets the total number of events that were analyzed for compliance.
	/// </summary>
	/// <value> The count of all events examined during the compliance analysis. </value>
	public int TotalEventsAnalyzed { get; } = totalEventsAnalyzed;

	/// <summary>
	/// Gets the list of compliance violations found during the analysis.
	/// </summary>
	/// <value> A collection of compliance violations detected in the analyzed events. </value>
	public List<ComplianceViolation> Violations { get; } = violations;

	/// <summary>
	/// Gets additional details and metadata about the compliance report.
	/// </summary>
	/// <value> A dictionary containing supplementary information about the compliance evaluation. </value>
	public Dictionary<string, object> Details { get; } = details;
}
