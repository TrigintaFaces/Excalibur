// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	private readonly ITenantContext _tenantContext;
	private readonly ILogger<TieredEventStoreDecorator> _logger;

	internal TieredEventStoreDecorator(
		IEventStore hotStore,
		IColdEventStore coldStore,
		ILogger<TieredEventStoreDecorator> logger,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(hotStore);
		ArgumentNullException.ThrowIfNull(coldStore);
		ArgumentNullException.ThrowIfNull(logger);

		_hotStore = hotStore;
		_coldStore = coldStore;
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
	/// <remarks>
	/// Archival moves a payload to cold storage and leaves the ENTRY in the hot store, so the hot read
	/// already returns every event of the stream in version order. This decorator's only job is to put the
	/// archived payloads back.
	/// <para>
	/// That is why there is no gap detection here any more. The previous implementation inferred archival
	/// from ABSENCE -- if the hot stream did not start at version 1 it assumed the earlier events had been
	/// archived, and consulted a snapshot to decide whether the gap was benign. Every one of those
	/// inferences is unnecessary once the row survives archival, and each was capable of being wrong in a
	/// direction nothing downstream could detect.
	/// </para>
	/// </remarks>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var hotEvents = await _hotStore.LoadAsync(aggregateId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		return await HydrateArchivedPayloadsAsync(aggregateId, hotEvents, cancellationToken)
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

		return await HydrateArchivedPayloadsAsync(aggregateId, hotEvents, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Replaces the payload of every archived entry with the copy held in cold storage.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An entry is archived exactly when it carries <see cref="StoredEvent.ArchivedAt"/>. That is a fact
	/// recorded by the archive, not a property inferred from what is missing, so this cannot mistake an
	/// ERASED event for an archived one -- an erased entry has no payload and no archive stamp, and its
	/// payload is meant to stay gone.
	/// </para>
	/// <para>
	/// Cold storage is read once for the whole stream and indexed by version, rather than once per archived
	/// entry: a stream with a thousand archived events would otherwise issue a thousand blob reads.
	/// </para>
	/// <para>
	/// A cold payload that cannot be found is left as-is rather than throwing. The entry keeps its version
	/// and position, so the stream stays contiguous and the caller sees an event whose payload is absent --
	/// the same shape as an erased event, and one it must already tolerate. Throwing would take down a read
	/// of the whole aggregate because one archived payload could not be fetched.
	/// </para>
	/// </remarks>
	private async ValueTask<IReadOnlyList<StoredEvent>> HydrateArchivedPayloadsAsync(
		string aggregateId,
		IReadOnlyList<StoredEvent> hotEvents,
		CancellationToken cancellationToken)
	{
		var archivedCount = 0;
		for (var i = 0; i < hotEvents.Count; i++)
		{
			if (hotEvents[i].ArchivedAt is not null)
			{
				archivedCount++;
			}
		}

		if (archivedCount == 0)
		{
			return hotEvents;
		}

		_logger.LoadingColdAndHotEvents(aggregateId, hotEvents.Count, archivedCount);

		var coldEvents = await _coldStore.ReadAsync(CurrentTenant, aggregateId, cancellationToken)
			.ConfigureAwait(false);

		var payloadByVersion = new Dictionary<long, byte[]?>(coldEvents.Count);
		foreach (var cold in coldEvents)
		{
			payloadByVersion[cold.Version] = cold.EventData;
		}

		var hydrated = new List<StoredEvent>(hotEvents.Count);
		foreach (var hot in hotEvents)
		{
			hydrated.Add(
				hot.ArchivedAt is not null && payloadByVersion.TryGetValue(hot.Version, out var payload)
					? hot with { EventData = payload }
					: hot);
		}

		return hydrated;
	}

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
