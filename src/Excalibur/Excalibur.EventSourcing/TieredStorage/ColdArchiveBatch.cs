// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>
/// Validates archive retries before a storage receipt can authorize hot payload removal.
/// </summary>
internal static class ColdArchiveBatch
{
	internal static IReadOnlyList<StoredEvent> Snapshot(IReadOnlyList<StoredEvent> events) =>
		events.Select(e => e with { EventData = e.EventData?.ToArray(), Metadata = e.Metadata?.ToArray() }).ToArray();

	internal static List<StoredEvent> GetAdditions(
		KeyedTenantPartition tenant,
		string aggregateId,
		IReadOnlyList<StoredEvent> existing,
		IReadOnlyList<StoredEvent> submitted,
		string? expectedAggregateType = null)
	{
		if (submitted.Count == 0)
		{
			return [];
		}

		var aggregateType = expectedAggregateType ?? submitted[0].AggregateType;
		var versions = Validate(existing, tenant, aggregateId, aggregateType);
		_ = Validate(submitted, tenant, aggregateId, aggregateType);
		var eventIds = existing.ToDictionary(e => e.EventId, StringComparer.Ordinal);
		var additions = new List<StoredEvent>();
		foreach (var candidate in submitted)
		{
			if (versions.TryGetValue(candidate.Version, out var prior))
			{
				if (!SameContent(prior, candidate))
				{
					throw new InvalidOperationException($"Conflicting archived event at version {candidate.Version}.");
				}
			}
			else
			{
				if (eventIds.ContainsKey(candidate.EventId))
				{
					throw new InvalidOperationException($"Archived event '{candidate.EventId}' has a different version.");
				}

				additions.Add(candidate);
			}
		}

		return additions;
	}

	internal static void ValidateStream(IReadOnlyList<StoredEvent> events, KeyedTenantPartition tenant, string aggregateId, string aggregateType) =>
		_ = Validate(events, tenant, aggregateId, aggregateType);

	internal static long ContiguousDurablePrefix(IReadOnlyList<StoredEvent> ascendingEvents)
	{
		long watermark = -1;
		foreach (var storedEvent in ascendingEvents.OrderBy(e => e.Version))
		{
			if (watermark == long.MaxValue || storedEvent.Version != watermark + 1)
			{
				break;
			}

			watermark = storedEvent.Version;
		}

		return watermark;
	}

	private static Dictionary<long, StoredEvent> Validate(
		IReadOnlyList<StoredEvent> events,
		KeyedTenantPartition tenant,
		string aggregateId,
		string aggregateType)
	{
		var versions = new Dictionary<long, StoredEvent>();
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (var storedEvent in events)
		{
			// A legacy null tenant is owned by the exact tenant-qualified object key. A present
			// conflicting term is never normalized away or replaced with ambient tenant context.
			if (storedEvent.Version < 0 || storedEvent.GlobalPosition < 0 || storedEvent.EventData is null || ErasedEventMarker.IsErased(storedEvent.EventType)
				|| !string.Equals(storedEvent.AggregateId, aggregateId, StringComparison.Ordinal)
				|| !string.Equals(storedEvent.AggregateType, aggregateType, StringComparison.Ordinal)
				|| (storedEvent.TenantId is not null
					&& !string.Equals(storedEvent.TenantId, tenant.TenantId, StringComparison.Ordinal))
				|| !versions.TryAdd(storedEvent.Version, storedEvent)
				|| !ids.Add(storedEvent.EventId))
			{
				throw new InvalidOperationException("The archive contains an unresolved, duplicate, or mismatched event.");
			}
		}

		return versions;
	}

	private static bool SameContent(StoredEvent left, StoredEvent right) =>
		string.Equals(left.EventId, right.EventId, StringComparison.Ordinal)
		&& string.Equals(left.EventType, right.EventType, StringComparison.Ordinal)
		&& left.Timestamp == right.Timestamp
		// Zero denotes unavailable position provenance, including archives written by older
		// aggregate-load mappings. A duplicate receipt retains the stored record unchanged.
		&& (left.GlobalPosition == 0 || right.GlobalPosition == 0 || left.GlobalPosition == right.GlobalPosition)
		&& SameBytes(left.EventData, right.EventData)
		&& SameBytes(left.Metadata, right.Metadata);

	private static bool SameBytes(byte[]? left, byte[]? right) =>
		left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
}
