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
/// Write operations always go to the hot store. Reads preserve the hot stream's membership and order,
/// restoring payloads only for non-erased archive markers that lack readable hot data.
/// </para>
/// <para>
/// Missing non-erased payloads fail the read. A positive erasure marker takes precedence over
/// archive metadata. This decorator does not expose a capability to erase cold storage.
/// </para>
/// <para>
/// Each archive marker is rechecked after the cold fetch using the decorated hot store's authoritative
/// reader. These observations do not form an atomic stream snapshot or fence subsequent erasure.
/// Readable hot rows are not rechecked. An unavailable cold provider can still prevent the read.
/// </para>
/// </remarks>
internal sealed class TieredEventStoreDecorator : IEventStore
{
	private readonly IEventStore _hotStore;
	private readonly IColdEventStore _coldStore;
	private readonly IEventStoreAuthoritativeReader _authoritativeReader;
	private readonly TieredStorageCompositionReceipt _compositionReceipt;
	private readonly ITenantContext _tenantContext;
	private readonly ILogger<TieredEventStoreDecorator> _logger;

	internal TieredEventStoreDecorator(
		IEventStore hotStore,
		IColdEventStore coldStore,
		ILogger<TieredEventStoreDecorator> logger,
		ITenantContext tenantContext,
		TieredStorageCompositionReceipt? compositionReceipt = null)
	{
		ArgumentNullException.ThrowIfNull(hotStore);
		ArgumentNullException.ThrowIfNull(coldStore);
		ArgumentNullException.ThrowIfNull(logger);

		_hotStore = hotStore;
		_coldStore = coldStore;
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_logger = logger;
		_compositionReceipt = compositionReceipt ?? new TieredStorageCompositionReceipt();
		_authoritativeReader = hotStore.GetService(typeof(IEventStoreAuthoritativeReader)) as IEventStoreAuthoritativeReader
			?? throw new InvalidOperationException("Tiered storage requires an authoritative event reader from the decorated hot store. Configure an owned primary read path and ensure decorators mediate the capability.");
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
		var tenant = CurrentTenant;
		var hotEvents = await _hotStore.LoadAsync(aggregateId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		return await HydrateArchivedPayloadsAsync(tenant, aggregateId, aggregateType, hotEvents, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var tenant = CurrentTenant;
		var hotEvents = await _hotStore.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken)
			.ConfigureAwait(false);

		return await HydrateArchivedPayloadsAsync(tenant, aggregateId, aggregateType, hotEvents, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Replaces the payload of every archived entry with the copy held in cold storage.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An archive stamp identifies a moved payload. A positive erasure marker takes precedence even
	/// when that stamp survives erasure. A missing payload alone does not establish erasure.
	/// </para>
	/// <para>
	/// Cold storage is read once for the whole stream and indexed by version, rather than once per archived
	/// entry: a stream with a thousand archived events would otherwise issue a thousand blob reads.
	/// </para>
	/// <para>
	/// An unresolved non-erased payload fails the whole read. Returning it as an apparent erasure would
	/// allow callers to omit a committed event while continuing through later versions.
	/// </para>
	/// </remarks>
	private async ValueTask<IReadOnlyList<StoredEvent>> HydrateArchivedPayloadsAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		string aggregateType,
		IReadOnlyList<StoredEvent> hotEvents,
		CancellationToken cancellationToken)
	{
		var archivedCount = 0;
		for (var i = 0; i < hotEvents.Count; i++)
		{
			var row = hotEvents[i];
			if (!string.Equals(row.AggregateId, aggregateId, StringComparison.Ordinal)
				|| !string.Equals(row.AggregateType, aggregateType, StringComparison.Ordinal)
				|| (row.TenantId is not null && !string.Equals(row.TenantId, tenant.TenantId, StringComparison.Ordinal))
				|| row.GlobalPosition < 0)
			{
				throw new InvalidOperationException("The hot event does not match the requested stream identity.");
			}

			if (ErasedEventMarker.IsErased(hotEvents[i].EventType))
			{
				continue;
			}

			if (hotEvents[i].ArchivedAt is not null && hotEvents[i].EventData is null)
			{
				if (row.TenantId is null)
				{
					throw new InvalidOperationException("An archived event requires recorded tenant provenance before cold storage can be accessed.");
				}
				archivedCount++;
			}
			else if (hotEvents[i].EventData is null)
			{
				throw new InvalidOperationException("A non-erased event has no payload or archive location.");
			}
		}

		if (archivedCount == 0)
		{
			return hotEvents;
		}

		_logger.LoadingColdAndHotEvents(aggregateId, hotEvents.Count, archivedCount);

		var coldEvents = await _coldStore.ReadAsync(tenant, aggregateId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		// Snapshot cold data, then observe each archived marker afresh. Erasure may commit during
		// the cold fetch; the original hot read is not authority to restore an older payload.
		var revalidator = new ArchivedEventRevalidator(tenant, aggregateId, aggregateType, coldEvents, _authoritativeReader);

		var hydrated = new List<StoredEvent>(hotEvents.Count);
		foreach (var hot in hotEvents)
		{
			if (ErasedEventMarker.IsErased(hot.EventType) || hot.EventData is not null)
			{
				hydrated.Add(hot);
				continue;
			}

			hydrated.Add(await revalidator.ResolveAsync(hot, cancellationToken).ConfigureAwait(false));
		}

		return hydrated;
	}

	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(TieredStorageCompositionReceipt))
		{
			return _compositionReceipt;
		}

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
