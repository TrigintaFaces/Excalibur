// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>
/// Preserves the complete supplied legacy archive while allowing additions to its typed copy.
/// </summary>
/// <remarks>
/// This is an in-memory content check, not a migration receipt or writer fence. The caller must
/// obtain complete source bytes and their revision together, fence legacy writers, and bind checks
/// to the destination revision used for a conditional update. No stored key is switched here.
/// Archive timestamps and timestamp offsets are copied exactly, even though archive timestamps
/// are lifecycle metadata rather than event identity. No payload or baseline collection is exposed.
/// Callers must keep supplied collections and buffers stable while they are being snapshotted.
/// </remarks>
internal sealed class ColdArchiveMigrationBaseline
{
	private readonly KeyedTenantPartition _tenant;
	private readonly string _aggregateId;
	private readonly string _aggregateType;
	private readonly IReadOnlyList<StoredEvent> _events;

	private ColdArchiveMigrationBaseline(KeyedTenantPartition tenant, string aggregateId,
		string aggregateType, IReadOnlyList<StoredEvent> events)
	{
		_tenant = tenant;
		_aggregateId = aggregateId;
		_aggregateType = aggregateType;
		_events = events;
	}

	internal static ColdArchiveMigrationBaseline Capture(KeyedTenantPartition tenant, string aggregateId,
		string aggregateType, IReadOnlyList<StoredEvent> source)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);
		ArgumentNullException.ThrowIfNull(source);
		if (source.Count == 0)
		{
			throw new InvalidOperationException("An empty legacy archive cannot establish migrated stream ownership.");
		}

		var snapshot = SnapshotAndValidate(source, tenant, aggregateId, aggregateType);
		return new ColdArchiveMigrationBaseline(tenant, aggregateId, aggregateType, snapshot);
	}

	internal void VerifyPreservedBy(IReadOnlyList<StoredEvent> destination)
	{
		ArgumentNullException.ThrowIfNull(destination);
		var snapshot = SnapshotAndValidate(destination, _tenant, _aggregateId, _aggregateType);
		var byVersion = snapshot.ToDictionary(e => e.Version);
		foreach (var original in _events)
		{
			if (!byVersion.TryGetValue(original.Version, out var copied) || !SameRepresentation(original, copied))
			{
				throw new InvalidOperationException("The typed archive does not preserve the complete migrated baseline.");
			}
		}
	}

	private static IReadOnlyList<StoredEvent> SnapshotAndValidate(IReadOnlyList<StoredEvent> events,
		KeyedTenantPartition tenant, string aggregateId, string aggregateType)
	{
		foreach (var storedEvent in events)
		{
			if (storedEvent is null || string.IsNullOrWhiteSpace(storedEvent.EventId)
				|| string.IsNullOrWhiteSpace(storedEvent.EventType))
			{
				throw new InvalidOperationException("A migration archive contains a missing event identity.");
			}
		}

		var snapshot = ColdArchiveBatch.Snapshot(events);
		ColdArchiveBatch.ValidateStream(snapshot, tenant, aggregateId, aggregateType);
		return snapshot;
	}

	private static bool SameRepresentation(StoredEvent left, StoredEvent right) =>
		string.Equals(left.EventId, right.EventId, StringComparison.Ordinal)
		&& string.Equals(left.AggregateId, right.AggregateId, StringComparison.Ordinal)
		&& string.Equals(left.AggregateType, right.AggregateType, StringComparison.Ordinal)
		&& string.Equals(left.EventType, right.EventType, StringComparison.Ordinal)
		&& string.Equals(left.TenantId, right.TenantId, StringComparison.Ordinal)
		&& left.Version == right.Version
		&& left.GlobalPosition == right.GlobalPosition
		&& left.Timestamp.EqualsExact(right.Timestamp)
		&& SameArchiveTime(left.ArchivedAt, right.ArchivedAt)
		&& SameBytes(left.EventData, right.EventData)
		&& SameBytes(left.Metadata, right.Metadata);

	private static bool SameArchiveTime(DateTimeOffset? left, DateTimeOffset? right) =>
		left is { } timestamp ? right is { } other && timestamp.EqualsExact(other) : right is null;

	private static bool SameBytes(byte[]? left, byte[]? right) =>
		left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
}
