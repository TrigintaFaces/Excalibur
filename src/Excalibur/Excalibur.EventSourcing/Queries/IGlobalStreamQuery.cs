// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	/// Reads events from the global stream lying STRICTLY ABOVE the specified position.
	/// </summary>
	/// <param name="position">
	/// The exclusive lower bound. The event at this position is NOT returned; reading resumes at the
	/// next one.
	/// </param>
	/// <param name="maxCount">The maximum number of events to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The events strictly above <paramref name="position"/>, in ascending global order, with no
	/// duplicates.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>Exclusivity is a contract, not an implementation detail, and a consumer of this interface
	/// relies on it for correctness.</b> A subscription records its progress as the position it has
	/// applied through, and resumes by passing that position here. An implementation that returned the
	/// event AT the given position would redeliver the last applied event on every single poll, and a
	/// subscription would never make progress past it.
	/// </para>
	/// <para>
	/// It also decides what counts as a re-delivery. Because every event returned here is above the
	/// caller's own recorded progress, a reader resuming from its checkpoint knows that nothing it
	/// receives has been applied before — which is why the projection host can state that its
	/// deliveries are first deliveries rather than having to compute it. An inclusive implementation
	/// silently breaks that reasoning.
	/// </para>
	/// <para>
	/// Implementers: the bound is <c>&gt;</c>, never <c>&gt;=</c>. Ascending order and absence of
	/// duplicates are required, and a position must not be returned before every position below it is
	/// committed — a reader cannot distinguish a gap that will fill from one that never will, so
	/// returning past a gap converts a transient hole into a permanently skipped event.
	/// </para>
	/// </remarks>
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
	/// <remarks>
	/// Selection is made when the underlying page is read. If archive hydration subsequently observes
	/// that a selected event was erased, that row may be returned as a normalized erasure marker with
	/// no payload or metadata, retaining its identity and position. Consumers must handle such markers
	/// even when requesting a different event type. A page is not an atomic snapshot across hydration.
	/// </remarks>
	ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
		string eventType,
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current head position of the global stream — the position of the most recently
	/// committed event across all aggregates.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The highest committed global-stream position, or <c>0</c> when the stream is empty.
	/// </returns>
	/// <remarks>
	/// Pairing the head with a subscription's checkpoint yields the subscription's projection lag.
	/// </remarks>
	ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken);
}
