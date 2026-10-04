// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Queries;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>Restores archived payloads without changing the selected global page.</summary>
/// <remarks>
/// Composition must bind the inner query, authoritative reader and cold tier to the same store.
/// Any contiguous-prefix filtering belongs inside this decorator, before cold data is fetched.
/// Each archive marker receives its own fresh observation after its stream's cold fetch. These
/// observations are not an atomic page snapshot and do not fence erasure after the observation.
/// </remarks>
internal sealed class TieredGlobalStreamQuery : IGlobalStreamQuery
{
	private readonly IGlobalStreamQuery _inner;
	private readonly IColdEventStore _cold;
	private readonly IEventStoreAuthoritativeReader _reader;

	internal TieredGlobalStreamQuery(IGlobalStreamQuery inner, IColdEventStore cold, IEventStoreAuthoritativeReader reader)
	{
		ArgumentNullException.ThrowIfNull(inner);
		ArgumentNullException.ThrowIfNull(cold);
		ArgumentNullException.ThrowIfNull(reader);
		_inner = inner;
		_cold = cold;
		_reader = reader;
	}

	public async ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
		GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);
		ArgumentOutOfRangeException.ThrowIfNegative(position.Position);
		ArgumentOutOfRangeException.ThrowIfNegative(maxCount);
		var rows = await _inner.ReadAllAsync(position, maxCount, cancellationToken).ConfigureAwait(false);
		return await HydrateAsync(rows, position, maxCount, cancellationToken).ConfigureAwait(false);
	}

	public async ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
		string eventType, GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
		ArgumentNullException.ThrowIfNull(position);
		ArgumentOutOfRangeException.ThrowIfNegative(position.Position);
		ArgumentOutOfRangeException.ThrowIfNegative(maxCount);
		var rows = await _inner.ReadByEventTypeAsync(eventType, position, maxCount, cancellationToken).ConfigureAwait(false);
		return await HydrateAsync(rows, position, maxCount, cancellationToken).ConfigureAwait(false);
	}

	public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
		_inner.GetHeadPositionAsync(cancellationToken);

	private async ValueTask<IReadOnlyList<StoredEvent>> HydrateAsync(IReadOnlyList<StoredEvent> rows,
		GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		// Retain membership even when awaiting other services. Validate the entire page before cold I/O.
		var page = rows.ToArray();
		if (page.Length > maxCount)
		{
			throw new InvalidOperationException("The global query exceeded its requested page size.");
		}
		var previous = position.Position;
		foreach (var row in page)
		{
			if (row is null || string.IsNullOrWhiteSpace(row.TenantId)
				|| string.IsNullOrWhiteSpace(row.AggregateId) || string.IsNullOrWhiteSpace(row.AggregateType)
				|| string.IsNullOrWhiteSpace(row.EventId) || string.IsNullOrWhiteSpace(row.EventType)
				|| row.Version < 0 || row.GlobalPosition <= 0 || row.GlobalPosition <= previous)
			{
				throw new InvalidOperationException("The global page has invalid identity, tenant provenance, or position ordering.");
			}
			previous = row.GlobalPosition;
			if (!ErasedEventMarker.IsErased(row.EventType) && row.EventData is null && row.ArchivedAt is null)
			{
				throw new InvalidOperationException("A non-erased global event has no payload or archive location.");
			}
		}

		// ValueTuple's string equality is ordinal. Neither tenant nor aggregate type may be omitted.
		var streams = new Dictionary<(string Tenant, string Type, string Id), ArchivedEventRevalidator>();
		var result = new List<StoredEvent>(page.Length);
		foreach (var row in page)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (ErasedEventMarker.IsErased(row.EventType) || row.EventData is not null)
			{
				result.Add(row);
				continue;
			}

			var key = (row.TenantId!, row.AggregateType, row.AggregateId);
			if (!streams.TryGetValue(key, out var revalidator))
			{
				var tenant = KeyedTenantPartition.FromStoredValue(row.TenantId!);
				var cold = await _cold.ReadAsync(tenant, row.AggregateId, row.AggregateType, cancellationToken).ConfigureAwait(false);
				revalidator = new ArchivedEventRevalidator(tenant, row.AggregateId, row.AggregateType, cold, _reader);
				streams.Add(key, revalidator);
			}

			result.Add(await revalidator.ResolveAsync(row, cancellationToken).ConfigureAwait(false));
		}

		return result;
	}
}
