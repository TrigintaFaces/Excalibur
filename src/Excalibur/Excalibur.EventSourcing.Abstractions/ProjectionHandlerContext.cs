// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

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
	/// <param name="isReplay">
	/// <see langword="true"/> when this event is being re-applied by a rebuild or a recovery rather
	/// than delivered live.
	/// </param>
	public ProjectionHandlerContext(
		string aggregateId,
		string aggregateType,
		long committedVersion,
		DateTimeOffset timestamp,
		bool isReplay)
	{
		AggregateId = aggregateId;
		AggregateType = aggregateType;
		CommittedVersion = committedVersion;
		Timestamp = timestamp;
		IsReplay = isReplay;
	}

	/// <summary>
	/// Gets a value indicating whether this event is being RE-APPLIED rather than delivered live.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A rebuild or a recovery re-runs every handler over history. An asynchronous handler is
	/// arbitrary code with arbitrary side effects -- it may send mail, call a payment provider, or
	/// publish a message -- and without this flag it cannot tell a replay from a first delivery, so it
	/// performs those effects again.
	/// </para>
	/// <para>
	/// It is a REQUIRED constructor parameter rather than an optional one with a default. A default of
	/// <see langword="false"/> is correct for the common case and wrong for the dangerous one, and the
	/// dangerous one is the path nobody is looking at when they add a call site.
	/// </para>
	/// <para>
	/// <b>This deliberately diverges from the sibling <c>EventNotificationContext</c></b>, which
	/// carries the same flag with a default of <see langword="false"/>. That type is a record with
	/// init-only members constructed in many places, where the default buys real ergonomics. This one
	/// has two construction sites in the framework, so the ergonomic saving is nil and the safety is
	/// not. The divergence is a choice, not an oversight.
	/// </para>
	/// <para>
	/// <b>Consumer obligation:</b> a handler with side effects outside the projection state MUST check
	/// this and skip them when it is <see langword="true"/>. Folding into the projection state itself
	/// is always correct and needs no guard.
	/// </para>
	/// </remarks>
	/// <value><see langword="true"/> during a rebuild or recovery; otherwise <see langword="false"/>.</value>
	public bool IsReplay { get; }

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
