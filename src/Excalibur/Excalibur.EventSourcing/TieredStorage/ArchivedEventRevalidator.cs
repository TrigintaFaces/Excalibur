// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>Revalidates one archive marker after the cold batch has been fetched.</summary>
internal sealed class ArchivedEventRevalidator
{
	private readonly KeyedTenantPartition _tenant;
	private readonly string _aggregateId;
	private readonly string _aggregateType;
	private readonly IEventStoreAuthoritativeReader _reader;
	private readonly Lazy<IReadOnlyDictionary<long, StoredEvent>> _coldByVersion;

	// The producer must retain ownership of the input collection and arrays until this snapshot completes.
	// Lazy initialization publishes a complete validated index once, and does not capture any caller's token.
	internal ArchivedEventRevalidator(KeyedTenantPartition tenant, string aggregateId, string aggregateType,
		IReadOnlyList<StoredEvent> coldEvents, IEventStoreAuthoritativeReader reader)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentNullException.ThrowIfNull(coldEvents);
		ArgumentNullException.ThrowIfNull(reader);
		_tenant = tenant;
		_aggregateId = aggregateId;
		_aggregateType = aggregateType;
		_reader = reader;
		var snapshot = ColdArchiveBatch.Snapshot(coldEvents);
		_coldByVersion = new Lazy<IReadOnlyDictionary<long, StoredEvent>>(() =>
		{
			ColdArchiveBatch.ValidateStream(snapshot, tenant, aggregateId, aggregateType);
			return snapshot.ToDictionary(static e => e.Version);
		});
	}

	/// <remarks>
	/// The caller must fetch cold data before invoking this method. A fresh erased state produces a
	/// normalized tombstone: stale payload and metadata are discarded. Stored erasure audit metadata
	/// is not changed, but is not included in this normalized result. This is not an application fence.
	/// </remarks>
	internal async ValueTask<StoredEvent> ResolveAsync(
		StoredEvent original,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(original);
		ArgumentException.ThrowIfNullOrWhiteSpace(original.EventId);
		cancellationToken.ThrowIfCancellationRequested();
		if (original.TenantId is null || original.Version < 0 || original.GlobalPosition < 0
			|| original.ArchivedAt is null || original.EventData is not null
			|| ErasedEventMarker.IsErased(original.EventType))
		{
			throw new InvalidOperationException("Revalidation requires a non-erased archive marker with known tenant provenance.");
		}

		var tenant = KeyedTenantPartition.FromStoredValue(original.TenantId);
		if (!string.Equals(tenant.TenantId, original.TenantId, StringComparison.Ordinal)
			|| !string.Equals(tenant.TenantId, _tenant.TenantId, StringComparison.Ordinal)
			|| !string.Equals(original.AggregateId, _aggregateId, StringComparison.Ordinal)
			|| !string.Equals(original.AggregateType, _aggregateType, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("The recorded tenant identity must not be normalized during revalidation.");
		}

		var current = await _reader.ReadCurrentAsync(tenant, original.AggregateId, original.AggregateType,
			original.EventId, original.Version, cancellationToken).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
		if (current is null || current.Tenant is null
			|| !string.Equals(current.Tenant.TenantId, tenant.TenantId, StringComparison.Ordinal)
			|| !string.Equals(current.AggregateId, original.AggregateId, StringComparison.Ordinal)
			|| !string.Equals(current.AggregateType, original.AggregateType, StringComparison.Ordinal)
			|| !string.Equals(current.EventId, original.EventId, StringComparison.Ordinal)
			|| current.Version != original.Version || current.GlobalPosition < 0
			|| (original.GlobalPosition > 0 && current.GlobalPosition != original.GlobalPosition)
			|| current.Timestamp != original.Timestamp)
		{
			throw new InvalidOperationException("The authoritative event state is missing or has a different stable identity.");
		}

		// Erasure changes event type and metadata. Decide it before requiring a live cold match.
		if (ErasedEventMarker.IsErased(current.EventType))
		{
			return original with
			{
				EventType = ErasedEventMarker.EventType,
				EventData = null,
				Metadata = null,
				ArchivedAt = current.ArchivedAt,
			};
		}

		if (!string.Equals(current.EventType, original.EventType, StringComparison.Ordinal) || current.ArchivedAt is null)
		{
			throw new InvalidOperationException("The authoritative event is no longer the expected archive marker.");
		}

		if (!_coldByVersion.Value.TryGetValue(original.Version, out var cold)
			|| !string.Equals(cold.EventId, original.EventId, StringComparison.Ordinal)
			|| !string.Equals(cold.EventType, original.EventType, StringComparison.Ordinal)
			|| cold.Timestamp != original.Timestamp
			|| (cold.GlobalPosition > 0 && current.GlobalPosition > 0 && cold.GlobalPosition != current.GlobalPosition)
			|| (original.Metadata is null ? cold.Metadata is not null
				: cold.Metadata is null || !original.Metadata.AsSpan().SequenceEqual(cold.Metadata)))
		{
			throw new InvalidOperationException("The cold event does not match the authoritative archive identity.");
		}

		// Do not expose the cached array: callers own returned payloads and may mutate them.
		return original with { EventData = cold.EventData!.ToArray(), ArchivedAt = current.ArchivedAt };
	}
}
