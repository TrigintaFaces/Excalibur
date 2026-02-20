// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Request object for searching audit events.
/// </summary>
public sealed class AuditSearchRequest
{
	/// <summary>
	/// Gets the start time for the search.
	/// </summary>
	/// <value>
	/// The start time for the search.
	/// </value>
	public DateTimeOffset? StartTime { get; init; }

	/// <summary>
	/// Gets the end time for the search.
	/// </summary>
	/// <value>
	/// The end time for the search.
	/// </value>
	public DateTimeOffset? EndTime { get; init; }

	/// <summary>
	/// Gets the user ID filter.
	/// </summary>
	/// <value>
	/// The user ID filter.
	/// </value>
	public string? UserId { get; init; }

	/// <summary>
	/// Gets the event type filter.
	/// </summary>
	/// <value>
	/// The event type filter.
	/// </value>
	public string? EventType { get; init; }

	/// <summary>
	/// Gets the severity level filter.
	/// </summary>
	/// <value>
	/// The severity level filter.
	/// </value>
	public SecurityEventSeverity? Severity { get; init; }

	/// <summary>
	/// Gets the source IP address filter.
	/// </summary>
	/// <value>
	/// The source IP address filter.
	/// </value>
	public string? SourceIpAddress { get; init; }

	/// <summary>
	/// Gets the event source filter.
	/// </summary>
	/// <value>
	/// The event source filter.
	/// </value>
	public string? Source { get; init; }

	/// <summary>
	/// Gets the free-text search query.
	/// </summary>
	/// <value>
	/// The free-text search query.
	/// </value>
	public string? SearchQuery { get; init; }

	/// <summary>
	/// Gets the maximum number of results to return.
	/// </summary>
	/// <value>
	/// The maximum number of results to return.
	/// </value>
	public int MaxResults { get; init; } = 100;

	/// <summary>
	/// Gets the number of results to skip.
	/// </summary>
	/// <value>
	/// The number of results to skip.
	/// </value>
	public int Skip { get; init; }

	/// <summary>
	/// Gets a value indicating whether to sort results in descending order.
	/// </summary>
	/// <value>
	/// A value indicating whether to sort results in descending order.
	/// </value>
	public bool SortDescending { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to include event details in the results.
	/// </summary>
	/// <value>
	/// A value indicating whether to include event details in the results.
	/// </value>
	public bool IncludeDetails { get; init; } = true;
}
