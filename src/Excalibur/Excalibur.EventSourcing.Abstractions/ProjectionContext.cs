// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Provides contextual information about the projection processing environment
/// when handling events in a <c>When&lt;TEvent&gt;</c> handler.
/// </summary>
/// <remarks>
/// <para>
/// Use this context to distinguish between live event processing and replay/rebuild
/// scenarios. For example, skip sending notifications during replay:
/// </para>
/// <code>
/// builder.AddProjection&lt;OrderSummary&gt;(p => p
///     .Inline()
///     .When&lt;OrderPlaced&gt;((proj, e, ctx) =>
///     {
///         proj.Total = e.Amount;
///         if (!ctx.IsReplay)
///         {
///             // Only send notifications for live events
///         }
///     }));
/// </code>
/// </remarks>
public sealed class ProjectionContext
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionContext"/> class.
	/// </summary>
	/// <param name="isReplay">Whether this event is being processed during a projection rebuild/replay.</param>
	/// <param name="globalPosition">The global stream position of the event, if available.</param>
	/// <param name="aggregateId">The identifier of the aggregate whose event is being applied.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="aggregateId"/> is null or empty.
	/// </exception>
	/// <remarks>
	/// The aggregate identifier is required and there is no overload without it. Every event reaching a
	/// projection came from exactly one aggregate, so a context without an identity does not describe
	/// anything that can happen -- and a projection silently stamped with an empty id is the failure
	/// this type exists to prevent. Making it required moves that from a convention to something the
	/// compiler enforces.
	/// </remarks>
	public ProjectionContext(bool isReplay, long? globalPosition, string aggregateId)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);

		IsReplay = isReplay;
		GlobalPosition = globalPosition;
		AggregateId = aggregateId;
	}

	/// <summary>
	/// Gets a value indicating whether the current event is being processed during
	/// a projection rebuild or replay, as opposed to live event processing.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the event is part of a rebuild/replay;
	/// <see langword="false"/> if it is a live event from <c>SaveAsync</c>.
	/// </value>
	public bool IsReplay { get; }

	/// <summary>
	/// Gets the global stream position of the event, if available.
	/// </summary>
	/// <value>
	/// The global position in the all-events stream, or <see langword="null"/>
	/// if the position is not available (e.g., during inline projection processing
	/// where global position has not yet been assigned).
	/// </value>
	public long? GlobalPosition { get; }

	/// <summary>
	/// Gets the identifier of the aggregate whose event is being applied.
	/// </summary>
	/// <value>The identifier of the aggregate whose event is being applied. Never null or empty.</value>
	/// <remarks>
	/// <para>
	/// Domain events do not carry the aggregate identifier; the stored envelope is authoritative for
	/// it. This is how a projection reaches it.
	/// </para>
	/// <para>
	/// <b>A projection read back by a client usually needs to store this.</b> Projection stores key the
	/// stored document by the projection ID, but a read returns the document body alone, so an
	/// identifier never written into the body is not available to the caller. A client loading a
	/// projection to populate an edit screen then has nothing to send to an update command:
	/// </para>
	/// <code>
	/// .When&lt;CustomerCreated&gt;((view, e, ctx) =>
	/// {
	///     view.Id = ctx.AggregateId;   // the read model's own identity
	///     view.Name = e.Name;
	/// })
	/// </code>
	/// <para>
	/// Always populated. The constructor rejects a null or empty identifier, so a handler never has to
	/// guard it: a projection whose identity silently became <c>""</c> is indistinguishable from one
	/// that was never given an identity, and the type refuses to represent that state.
	/// </para>
	/// </remarks>
	public string AggregateId { get; }

	/// <summary>
	/// Creates a replay context for an event belonging to the specified aggregate.
	/// </summary>
	/// <param name="globalPosition">The global stream position of the event.</param>
	/// <param name="aggregateId">The identifier of the aggregate whose event is being applied.</param>
	/// <returns>A new <see cref="ProjectionContext"/> configured for replay.</returns>
	public static ProjectionContext Replay(long globalPosition, string aggregateId)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(globalPosition);
		return new(isReplay: true, globalPosition, aggregateId);
	}
}
