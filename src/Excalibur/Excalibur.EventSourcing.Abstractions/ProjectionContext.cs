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
	public ProjectionContext(bool isReplay, long? globalPosition)
	{
		IsReplay = isReplay;
		GlobalPosition = globalPosition;
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
	/// Gets the default context for live (non-replay) event processing
	/// without a known global position.
	/// </summary>
	public static ProjectionContext Live { get; } = new(isReplay: false, globalPosition: null);

	/// <summary>
	/// Creates a replay context with the specified global position.
	/// </summary>
	/// <param name="globalPosition">The global stream position of the event.</param>
	/// <returns>A new <see cref="ProjectionContext"/> configured for replay.</returns>
	public static ProjectionContext Replay(long globalPosition)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(globalPosition);
		return new(isReplay: true, globalPosition);
	}
}
