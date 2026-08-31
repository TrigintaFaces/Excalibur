// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Decorators;

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
	private readonly ITenantContext _tenantContext;
	private readonly ILogger<TieredEventStoreDecorator> _logger;

	internal TieredEventStoreDecorator(
		IEventStore hotStore,
		IColdEventStore coldStore,
		ILogger<TieredEventStoreDecorator> logger,
		ITenantContext tenantContext,
		ISnapshotStore? snapshotStore = null)
	{
		ArgumentNullException.ThrowIfNull(hotStore);
		ArgumentNullException.ThrowIfNull(coldStore);
		ArgumentNullException.ThrowIfNull(logger);

		_hotStore = hotStore;
		_coldStore = coldStore;
		_snapshotStore = snapshotStore;
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_logger = logger;
	}

	/// <summary>
	/// The tenant partition for the current read. This decorator sits on the consumer read path, where an
	/// ambient tenant is established per request, so the cold read is addressed to the same tenant the hot
	/// read was. This is distinct from the archive service, which enumerates every tenant in one pass and
	/// therefore has no ambient tenant to inherit.
	/// </summary>
	private KeyedTenantPartition CurrentTenant =>
		KeyedTenantPartition.FromContext(_tenantContext);

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
			var coldEvents = await _coldStore.ReadAsync(CurrentTenant, aggregateId, fromVersion, cancellationToken)
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
		if (!await _coldStore.HasArchivedEventsAsync(CurrentTenant, aggregateId, cancellationToken).ConfigureAwait(false))
		{
			return Array.Empty<StoredEvent>();
		}

		_logger.LoadingFromColdStorage(aggregateId);

		return await _coldStore.ReadAsync(CurrentTenant, aggregateId, cancellationToken).ConfigureAwait(false);
	}

	private async Task<IReadOnlyList<StoredEvent>> MergeWithColdAsync(
		string aggregateId,
		IReadOnlyList<StoredEvent> hotEvents,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		_logger.LoadingColdAndHotEvents(aggregateId, hotEvents.Count, fromVersion);

		var coldEvents = await _coldStore.ReadAsync(CurrentTenant, aggregateId, fromVersion, cancellationToken)
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

	/// <summary>
	/// Resolves a capability, mediating the hot store's so a caller cannot reach it without the cold tier.
	/// </summary>
	/// <param name="serviceType">The capability interface being resolved.</param>
	/// <returns>A tiered view over the capability, or <see langword="null"/> when the hot store lacks it.</returns>
	/// <remarks>
	/// The transactional append writes to the hot store, as the ordinary append does, so it can be mediated.
	/// What must not escape is the capability's inherited read surface: the archive service deletes from hot
	/// after copying to cold, so a caller loading through the bare hot store would receive a history missing
	/// everything already archived, and would have no way to tell. The view routes every read back through
	/// this decorator, which consults both tiers.
	/// </remarks>
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType.IsInstanceOfType(this))
		{
			return this;
		}

		// Deny by default. This decorator deliberately does NOT inherit the delegating base, because that base
		// declares IEventStoreErasure and this decorator must not answer the erasure probe: it can erase the
		// hot tier only, and the archived range in cold is outside its reach. Answering would be a claim it
		// cannot honour. Only the transactional append is mediated, and only its read surface is re-routed.
		if (serviceType == typeof(ITransactionalEventStore)
			&& _hotStore.GetService(typeof(ITransactionalEventStore)) is ITransactionalEventStore transactional)
		{
			return new TieredTransactionalView(this, transactional);
		}

		return null;
	}

	private sealed class TieredTransactionalView(TieredEventStoreDecorator outer, ITransactionalEventStore capability)
		: EventStoreCapabilityView(outer), ITransactionalEventStore
	{
		public ValueTask<AppendResult> AppendWithOutboxStagingAsync(
			string aggregateId,
			string aggregateType,
			IEnumerable<IDomainEvent> events,
			long expectedVersion,
			Func<IDbTransaction, CancellationToken, ValueTask> stageOutbox,
			CancellationToken cancellationToken) =>
			capability.AppendWithOutboxStagingAsync(
				aggregateId, aggregateType, events, expectedVersion, stageOutbox, cancellationToken);
	}
}
