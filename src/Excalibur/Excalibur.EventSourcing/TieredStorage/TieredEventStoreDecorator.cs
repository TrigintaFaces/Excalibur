// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>
/// Decorator that transparently reads through to cold storage when events are
/// missing from the hot tier due to archival.
/// </summary>
/// <remarks>
/// <para>
/// Write operations (AppendAsync) always go to the hot store. Read operations
/// check the hot store first; if a version gap is detected (events start at
/// version N &gt; 1 and no snapshot covers the gap), the cold store is queried
/// for the missing range.
/// </para>
/// <para>
/// Snapshot-aware: if a snapshot exists at version S and hot events start at
/// version S+1, no cold read is needed (the snapshot covers the archived range).
/// </para>
/// </remarks>
internal sealed class TieredEventStoreDecorator : IEventStore
{
	private readonly IEventStore _hotStore;
	private readonly IColdEventStore _coldStore;
	private readonly ISnapshotStore? _snapshotStore;
	private readonly ILogger<TieredEventStoreDecorator> _logger;

	internal TieredEventStoreDecorator(
		IEventStore hotStore,
		IColdEventStore coldStore,
		ILogger<TieredEventStoreDecorator> logger,
		ISnapshotStore? snapshotStore = null)
	{
		ArgumentNullException.ThrowIfNull(hotStore);
		ArgumentNullException.ThrowIfNull(coldStore);
		ArgumentNullException.ThrowIfNull(logger);

		_hotStore = hotStore;
		_coldStore = coldStore;
		_snapshotStore = snapshotStore;
		_logger = logger;
	}

	/// <inheritdoc />
	public ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		// Writes always go to hot store
		return _hotStore.AppendAsync(aggregateId, aggregateType, events, expectedVersion, cancellationToken);
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var hotEvents = await _hotStore.LoadAsync(aggregateId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		// If hot has events starting from version 0/1, no gap exists
		if (hotEvents.Count > 0 && hotEvents[0].Version <= 1)
		{
			return hotEvents;
		}

		// If no hot events, check cold
		if (hotEvents.Count == 0)
		{
			return await LoadFromColdAsync(aggregateId, cancellationToken).ConfigureAwait(false);
		}

		// Gap detected: hot events start after version 1
		// Check if a snapshot covers the gap
		if (await IsGapCoveredBySnapshotAsync(aggregateId, aggregateType, hotEvents[0].Version, cancellationToken)
			.ConfigureAwait(false))
		{
			return hotEvents;
		}

		// Need cold events to fill the gap
		return await MergeWithColdAsync(aggregateId, hotEvents, 0, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var hotEvents = await _hotStore.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken)
			.ConfigureAwait(false);

		// If we got events and they start right after fromVersion, no gap
		if (hotEvents.Count > 0 && hotEvents[0].Version <= fromVersion + 1)
		{
			return hotEvents;
		}

		// Gap: need cold events from fromVersion
		if (hotEvents.Count == 0)
		{
			// All events might be in cold storage
			var coldEvents = await _coldStore.ReadAsync(aggregateId, fromVersion, cancellationToken)
				.ConfigureAwait(false);
			return coldEvents;
		}

		return await MergeWithColdAsync(aggregateId, hotEvents, fromVersion, cancellationToken)
			.ConfigureAwait(false);
	}

	private async Task<IReadOnlyList<StoredEvent>> LoadFromColdAsync(
		string aggregateId,
		CancellationToken cancellationToken)
	{
		if (!await _coldStore.HasArchivedEventsAsync(aggregateId, cancellationToken).ConfigureAwait(false))
		{
			return Array.Empty<StoredEvent>();
		}

		_logger.LoadingFromColdStorage(aggregateId);

		return await _coldStore.ReadAsync(aggregateId, cancellationToken).ConfigureAwait(false);
	}

	private async Task<IReadOnlyList<StoredEvent>> MergeWithColdAsync(
		string aggregateId,
		IReadOnlyList<StoredEvent> hotEvents,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		_logger.LoadingColdAndHotEvents(aggregateId, hotEvents.Count, fromVersion);

		var coldEvents = await _coldStore.ReadAsync(aggregateId, fromVersion, cancellationToken)
			.ConfigureAwait(false);

		if (coldEvents.Count == 0)
		{
			return hotEvents;
		}

		// Merge: cold events first, then hot events (both in version order)
		var merged = new List<StoredEvent>(coldEvents.Count + hotEvents.Count);
		merged.AddRange(coldEvents);
		merged.AddRange(hotEvents);
		return merged;
	}

	private async ValueTask<bool> IsGapCoveredBySnapshotAsync(
		string aggregateId,
		string aggregateType,
		long firstHotVersion,
		CancellationToken cancellationToken)
	{
		if (_snapshotStore is null)
		{
			return false;
		}

		var snapshot = await _snapshotStore.GetLatestSnapshotAsync(aggregateId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		// Snapshot at version S covers versions 1..S.
		// If hot events start at S+1 or earlier, the snapshot fills the gap.
		return snapshot is not null && firstHotVersion <= snapshot.Version + 1;
	}
}
