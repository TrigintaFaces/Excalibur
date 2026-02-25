// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Queries;

/// <summary>
/// Provides cross-aggregate event querying capabilities over the global event stream.
/// </summary>
/// <remarks>
/// <para>
/// The global stream query enables reading events across all aggregates, which is
/// essential for:
/// <list type="bullet">
/// <item>Building cross-aggregate projections</item>
/// <item>Event-driven integrations that need all events</item>
/// <item>Audit and compliance reporting</item>
/// <item>Event replay for projection rebuilding</item>
/// </list>
/// </para>
/// <para>
/// Events are returned in global order (as committed). Use <see cref="GlobalStreamPosition"/>
/// to track reading progress and resume from a checkpoint.
/// </para>
/// </remarks>
public interface IGlobalStreamQuery
{
	/// <summary>
	/// Reads events from the global stream starting at the specified position.
	/// </summary>
	/// <param name="position">The position to start reading from.</param>
	/// <param name="maxCount">The maximum number of events to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events in global order from the specified position.</returns>
	ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads events of a specific type from the global stream starting at the specified position.
	/// </summary>
	/// <param name="eventType">The event type name to filter by.</param>
	/// <param name="position">The position to start reading from.</param>
	/// <param name="maxCount">The maximum number of events to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events of the specified type in global order.</returns>
	ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
		string eventType,
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken);
}
