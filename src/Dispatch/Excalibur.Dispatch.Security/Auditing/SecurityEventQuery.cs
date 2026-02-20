// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Query parameters for retrieving security events.
/// </summary>
public sealed class SecurityEventQuery
{
	/// <summary>
	/// Gets or sets the start time for filtering security events.
	/// </summary>
	/// <value>
	/// The start time boundary for the query, or <see langword="null"/> for no lower bound.
	/// </value>
	public DateTimeOffset? StartTime { get; set; }

	/// <summary>
	/// Gets or sets the end time for filtering security events.
	/// </summary>
	/// <value>
	/// The end time boundary for the query, or <see langword="null"/> for no upper bound.
	/// </value>
	public DateTimeOffset? EndTime { get; set; }

	/// <summary>
	/// Gets or sets the event type for filtering security events.
	/// </summary>
	/// <value>
	/// The specific event type to filter by, or <see langword="null"/> to include all types.
	/// </value>
	public SecurityEventType? EventType { get; set; }

	/// <summary>
	/// Gets or sets the minimum severity level for filtering security events.
	/// </summary>
	/// <value>
	/// The minimum severity level to include, or <see langword="null"/> to include all severities.
	/// </value>
	public SecuritySeverity? MinimumSeverity { get; set; }

	/// <summary>
	/// Gets or sets the user identifier for filtering security events.
	/// </summary>
	/// <value>
	/// The user identifier to filter by, or <see langword="null"/> to include all users.
	/// </value>
	public string? UserId { get; set; }

	/// <summary>
	/// Gets or sets the source IP address for filtering security events.
	/// </summary>
	/// <value>
	/// The source IP address to filter by, or <see langword="null"/> to include all IP addresses.
	/// </value>
	public string? SourceIp { get; set; }

	/// <summary>
	/// Gets or sets the correlation identifier for filtering security events.
	/// </summary>
	/// <value>
	/// The correlation identifier to filter by, or <see langword="null"/> to include all events.
	/// </value>
	public Guid? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of results to return.
	/// </summary>
	/// <value>
	/// The maximum number of security events to return from the query (default is 1000).
	/// </value>
	public int MaxResults { get; set; } = 1000;
}
