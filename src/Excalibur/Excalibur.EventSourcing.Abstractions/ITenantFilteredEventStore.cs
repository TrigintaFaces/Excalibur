// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Extends <see cref="IEventStore"/> with tenant-level filtering for shared-shard scenarios
/// where multiple tenants coexist in the same database.
/// </summary>
/// <remarks>
/// <para>
/// When tenant sharding uses a shared-shard model (multiple tenants per physical database),
/// this interface adds a <c>WHERE TenantId = @tenantId</c> clause to event store queries.
/// This is distinct from <see cref="Excalibur.Data.Abstractions.Sharding.ITenantStoreResolver{TStore}"/>
/// which routes to entirely different databases.
/// </para>
/// <para>
/// Providers implement this interface on their event store class when shared-shard
/// isolation is needed. The routing layer checks for this interface via
/// <c>GetService&lt;ITenantFilteredEventStore&gt;</c> and delegates when available.
/// </para>
/// </remarks>
public interface ITenantFilteredEventStore
{
	/// <summary>
	/// Loads all events for an aggregate filtered by tenant.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="tenantId">The tenant identifier for filtering.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events for the aggregate belonging to the specified tenant.</returns>
	ValueTask<IReadOnlyList<StoredEvent>> LoadByTenantAsync(
		string aggregateId,
		string aggregateType,
		string tenantId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Appends events to the store with tenant isolation.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="tenantId">The tenant identifier for isolation.</param>
	/// <param name="events">The events to append.</param>
	/// <param name="expectedVersion">The expected current version (-1 for new aggregate).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the append operation.</returns>
	ValueTask<AppendResult> AppendByTenantAsync(
		string aggregateId,
		string aggregateType,
		string tenantId,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken);
}
