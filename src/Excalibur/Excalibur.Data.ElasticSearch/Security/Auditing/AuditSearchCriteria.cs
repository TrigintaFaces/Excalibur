// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents audit search criteria.
/// </summary>
public sealed class AuditSearchCriteria
{
	/// <summary>
	/// Gets or sets the start time for filtering audit events.
	/// </summary>
	/// <value> The earliest timestamp to include in search results, or <c> null </c> for no start time limit. </value>
	public DateTimeOffset? StartTime { get; set; }

	/// <summary>
	/// Gets or sets the end time for filtering audit events.
	/// </summary>
	/// <value> The latest timestamp to include in search results, or <c> null </c> for no end time limit. </value>
	public DateTimeOffset? EndTime { get; set; }

	/// <summary>
	/// Gets or sets the user identifier for filtering audit events by user.
	/// </summary>
	/// <value> The user ID to filter by, or <c> null </c> to include all users. </value>
	public string? UserId { get; set; }

	/// <summary>
	/// Gets or sets the event type for filtering audit events by type.
	/// </summary>
	/// <value> The event type to filter by, or <c> null </c> to include all event types. </value>
	public string? EventType { get; set; }

	/// <summary>
	/// Gets or sets the severity level for filtering audit events by severity.
	/// </summary>
	/// <value> The severity level to filter by, or <c> null </c> to include all severities. </value>
	public string? Severity { get; set; }

	/// <summary>
	/// Gets or sets the source IP address for filtering audit events by origin.
	/// </summary>
	/// <value> The source IP address to filter by, or <c> null </c> to include all IP addresses. </value>
	public string? SourceIpAddress { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of results to return.
	/// </summary>
	/// <value> The maximum number of audit events to include in search results. Default is 100. </value>
	public int MaxResults { get; set; } = 100;

	/// <summary>
	/// Gets or sets the number of results to skip for pagination.
	/// </summary>
	/// <value> The number of audit events to skip before returning results. Default is 0. </value>
	public int Skip { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to sort results in descending order.
	/// </summary>
	/// <value> <c> true </c> to sort results in descending order; <c> false </c> for ascending order. Default is <c> true </c>. </value>
	public bool SortDescending { get; set; } = true;
}
