// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Provides context about the aggregate whose events are being projected,
/// passed to <see cref="IProjectionEventHandler{TProjection, TEvent}.HandleAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// A new context is created for each event. For persisted events, aggregate identity,
/// version and timestamp come from that event's stored envelope, even when a batch spans
/// multiple streams. <see cref="OverrideProjectionId"/> starts as <see langword="null"/>
/// and allows the handler to redirect the projection to a different ID.
/// </para>
/// </remarks>
public sealed class ProjectionHandlerContext
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionHandlerContext"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="committedVersion">The persisted event's zero-based stream version.</param>
	/// <param name="timestamp">The persisted event's timestamp.</param>
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
	/// <b>It means a rebuild, recovery or on-demand replay is deliberately re-folding history</b> —
	/// nothing more. It is a property of the OPERATION, set by the caller that asked for the re-fold,
	/// not a judgement about whether any particular effect already happened.
	/// </para>
	/// <para>
	/// A rebuild or a recovery re-runs every handler over history. An asynchronous handler is
	/// arbitrary code with arbitrary side effects -- it may send mail, call a payment provider, or
	/// publish a message -- and a whole-history re-fold would perform all of them again.
	/// </para>
	/// <para>
	/// It is a REQUIRED constructor parameter rather than an optional one with a default. A default of
	/// <see langword="false"/> is correct for the common case and wrong for the dangerous one, and the
	/// dangerous one is the path nobody is looking at when they add a call site.
	/// </para>
	/// <para>
	/// <b>This deliberately diverges from the sibling <c>EventNotificationContext</c></b>, which
	/// carries the same flag with a default of <see langword="false"/>. That type is a record with
	/// init-only members, where the default buys real ergonomics. This one is constructed only by the
	/// framework's own apply and replay paths, so the ergonomic saving is small and the safety is not.
	/// The divergence is a choice, not an oversight.
	/// </para>
	/// <para>
	/// <b>Consumer obligation: a handler with side effects outside the projection state MUST be
	/// idempotent, keyed on the event identifier.</b> Delivery is at-least-once — an effect may be
	/// requested more than once for a single event after a process crash or an overlap between two
	/// readers of one subscription, and this flag is <see langword="false"/> on those deliveries,
	/// because they are not re-folds of history. The duplicate window is bounded by the events applied
	/// since the last durable checkpoint.
	/// </para>
	/// <para>
	/// So use it to skip effects during a deliberate re-fold of the whole of history, where repeating
	/// them is certainly wrong. <b>Never use it as the mechanism that makes a handler correct.</b> No
	/// flag the framework computes can be that mechanism: the effect is performed by consumer code,
	/// in a process the framework does not control, against a resource it cannot name, so the effect
	/// and the record of it cannot commit together. Idempotency is the only thing that closes it.
	/// </para>
	/// <para>
	/// Folding into the projection state itself is idempotent by construction and needs no guard.
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
	/// Gets the persisted event's zero-based stream version.
	/// </summary>
	/// <value>The committed version number.</value>
	public long CommittedVersion { get; }

	/// <summary>
	/// Gets the persisted event's timestamp.
	/// </summary>
	/// <value>The event timestamp.</value>
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
