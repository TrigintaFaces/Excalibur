// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Queries;

/// <summary>
/// Provides time-travel query capabilities for event-sourced aggregates.
/// </summary>
/// <remarks>
/// <para>
/// Time-travel queries enable examining the state of aggregates at any point in their history,
/// either by timestamp or by version number. This is useful for auditing, debugging,
/// historical reporting, and temporal queries.
/// </para>
/// <para>
/// Implementations should leverage the <see cref="IEventStore"/> to load events up to
/// the specified point and replay them against the aggregate.
/// </para>
/// </remarks>
public interface ITimeTravelQuery
{
	/// <summary>
	/// Gets events for an aggregate up to a specific point in time.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="pointInTime">The timestamp to query up to (inclusive).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events that occurred up to the specified point in time.</returns>
	ValueTask<IReadOnlyList<StoredEvent>> GetEventsAtPointInTimeAsync(
		string aggregateId,
		string aggregateType,
		DateTimeOffset pointInTime,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets events for an aggregate up to a specific version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="version">The version to query up to (inclusive).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events up to and including the specified version.</returns>
	ValueTask<IReadOnlyList<StoredEvent>> GetEventsAtVersionAsync(
		string aggregateId,
		string aggregateType,
		long version,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets events for an aggregate within a specific time range.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="from">The start of the time range (inclusive).</param>
	/// <param name="to">The end of the time range (inclusive).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events within the specified time range.</returns>
	ValueTask<IReadOnlyList<StoredEvent>> GetEventsInTimeRangeAsync(
		string aggregateId,
		string aggregateType,
		DateTimeOffset from,
		DateTimeOffset to,
		CancellationToken cancellationToken);
}
