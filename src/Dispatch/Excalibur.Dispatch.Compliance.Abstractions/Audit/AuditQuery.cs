// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0




namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents query parameters for searching audit events.
/// </summary>
public sealed record AuditQuery
{
	/// <summary>
	/// Gets or sets the start of the time range (inclusive).
	/// </summary>
	public DateTimeOffset? StartDate { get; init; }

	/// <summary>
	/// Gets or sets the end of the time range (inclusive).
	/// </summary>
	public DateTimeOffset? EndDate { get; init; }

	/// <summary>
	/// Gets or sets the event types to filter by.
	/// </summary>
	public IReadOnlyList<AuditEventType>? EventTypes { get; init; }

	/// <summary>
	/// Gets or sets the outcomes to filter by.
	/// </summary>
	public IReadOnlyList<AuditOutcome>? Outcomes { get; init; }

	/// <summary>
	/// Gets or sets the actor ID to filter by.
	/// </summary>
	public string? ActorId { get; init; }

	/// <summary>
	/// Gets or sets the resource ID to filter by.
	/// </summary>
	public string? ResourceId { get; init; }

	/// <summary>
	/// Gets or sets the resource type to filter by.
	/// </summary>
	public string? ResourceType { get; init; }

	/// <summary>
	/// Gets or sets the minimum classification level to filter by.
	/// </summary>
	public DataClassification? MinimumClassification { get; init; }

	/// <summary>
	/// Gets or sets the tenant ID to filter by.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets or sets the correlation ID to filter by.
	/// </summary>
	public string? CorrelationId { get; init; }

	/// <summary>
	/// Gets or sets the action to filter by (exact match).
	/// </summary>
	public string? Action { get; init; }

	/// <summary>
	/// Gets or sets the IP address to filter by.
	/// </summary>
	public string? IpAddress { get; init; }

	/// <summary>
	/// Gets or sets the maximum number of results to return. Default is 100.
	/// </summary>
	public int MaxResults { get; init; } = 100;

	/// <summary>
	/// Gets or sets the number of results to skip for pagination.
	/// </summary>
	public int Skip { get; init; }

	/// <summary>
	/// Gets or sets a value indicating whether to sort by timestamp descending. Default is true (newest first).
	/// </summary>
	public bool OrderByDescending { get; init; } = true;
}
