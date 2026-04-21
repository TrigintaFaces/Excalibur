// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Provides context about the aggregate whose events are being projected,
/// passed to <see cref="IProjectionEventHandler{TProjection, TEvent}.HandleAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// The context is created once per projection batch and reused across events.
/// <see cref="OverrideProjectionId"/> is reset to <see langword="null"/> before
/// each event, allowing handlers to optionally redirect the projection to a
/// different ID than the default aggregate ID.
/// </para>
/// </remarks>
public sealed class ProjectionHandlerContext
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionHandlerContext"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="committedVersion">The aggregate version after commit.</param>
	/// <param name="timestamp">The UTC timestamp of the notification.</param>
	public ProjectionHandlerContext(
		string aggregateId,
		string aggregateType,
		long committedVersion,
		DateTimeOffset timestamp)
	{
		AggregateId = aggregateId;
		AggregateType = aggregateType;
		CommittedVersion = committedVersion;
		Timestamp = timestamp;
	}

	/// <summary>
	/// Gets the unique identifier of the aggregate that produced the events.
	/// </summary>
	/// <value>The aggregate identifier.</value>
	public string AggregateId { get; }

	/// <summary>
	/// Gets the type name of the aggregate.
	/// </summary>
	/// <value>The aggregate type name.</value>
	public string AggregateType { get; }

	/// <summary>
	/// Gets the aggregate version after the events were committed.
	/// </summary>
	/// <value>The committed version number.</value>
	public long CommittedVersion { get; }

	/// <summary>
	/// Gets the UTC timestamp when the notification was created.
	/// </summary>
	/// <value>The notification timestamp.</value>
	public DateTimeOffset Timestamp { get; }

	/// <summary>
	/// Gets or sets an optional projection ID override. When set, the projection
	/// pipeline uses this ID instead of <see cref="AggregateId"/> to load and
	/// upsert the projection instance for the current event.
	/// </summary>
	/// <value>
	/// The overridden projection ID, or <see langword="null"/> to use the default
	/// aggregate ID.
	/// </value>
	public string? OverrideProjectionId { get; set; }
}
