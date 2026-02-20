// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Queries;

/// <summary>
/// Default implementation of <see cref="ITimeTravelQuery"/> that loads all events
/// from the <see cref="IEventStore"/> and filters by timestamp or version.
/// </summary>
/// <remarks>
/// <para>
/// This implementation loads all events for an aggregate and filters client-side.
/// Provider-specific implementations can override this behavior to push filtering
/// to the database for better performance with large event streams.
/// </para>
/// </remarks>
public sealed class EventStoreTimeTravelQuery : ITimeTravelQuery
{
	private readonly IEventStore _eventStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventStoreTimeTravelQuery"/> class.
	/// </summary>
	/// <param name="eventStore">The event store to query events from.</param>
	public EventStoreTimeTravelQuery(IEventStore eventStore)
	{
		_eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> GetEventsAtPointInTimeAsync(
		string aggregateId,
		string aggregateType,
		DateTimeOffset pointInTime,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);

		var allEvents = await _eventStore.LoadAsync(aggregateId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		return allEvents
			.Where(e => e.Timestamp <= pointInTime)
			.ToList()
			.AsReadOnly();
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> GetEventsAtVersionAsync(
		string aggregateId,
		string aggregateType,
		long version,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);

		var allEvents = await _eventStore.LoadAsync(aggregateId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		return allEvents
			.Where(e => e.Version <= version)
			.ToList()
			.AsReadOnly();
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> GetEventsInTimeRangeAsync(
		string aggregateId,
		string aggregateType,
		DateTimeOffset from,
		DateTimeOffset to,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);

		var allEvents = await _eventStore.LoadAsync(aggregateId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		return allEvents
			.Where(e => e.Timestamp >= from && e.Timestamp <= to)
			.ToList()
			.AsReadOnly();
	}
}
