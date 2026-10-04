// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Observes the current rows of synchronous in-memory test stores. This is not a database reader:
/// provider freshness and transaction isolation are covered by the real database integration tests.
/// </summary>
internal sealed class TestEventStateReader(IEventStore store) : IEventStoreAuthoritativeReader
{
	public async ValueTask<EventStoreEventState?> ReadCurrentAsync(KeyedTenantPartition tenant,
		string aggregateId, string aggregateType, string eventId, long version, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var rows = await store.LoadAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
		var row = rows.SingleOrDefault(e => e.TenantId == tenant.TenantId && e.AggregateId == aggregateId
			&& e.AggregateType == aggregateType && e.EventId == eventId && e.Version == version);
		return row is null ? null : new EventStoreEventState(KeyedTenantPartition.FromStoredValue(row.TenantId!),
			row.AggregateId, row.AggregateType, row.EventId, row.Version, row.GlobalPosition,
			row.EventType, row.Timestamp, row.ArchivedAt);
	}
}
