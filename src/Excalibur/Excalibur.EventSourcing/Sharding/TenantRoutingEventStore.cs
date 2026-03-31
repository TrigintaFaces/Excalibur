// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Decorator that routes <see cref="IEventStore"/> operations to the correct
/// tenant's shard based on the current <see cref="ITenantId"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered as Scoped when <see cref="ShardMapOptions.EnableTenantSharding"/> is true.
/// All <see cref="IEventStore"/> methods route transparently.
/// </para>
/// <para>
/// Store instances are cached per shard ID via <see cref="ITenantStoreResolver{TStore}"/>
/// to avoid creating new connections per call.
/// </para>
/// </remarks>
internal sealed class TenantRoutingEventStore : IEventStore
{
	private readonly ITenantStoreResolver<IEventStore> _resolver;
	private readonly ITenantId _tenantId;

	internal TenantRoutingEventStore(
		ITenantStoreResolver<IEventStore> resolver,
		ITenantId tenantId)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		ArgumentNullException.ThrowIfNull(tenantId);

		_resolver = resolver;
		_tenantId = tenantId;
	}

	/// <inheritdoc />
	public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var store = ResolveStore();
		return store.LoadAsync(aggregateId, aggregateType, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var store = ResolveStore();
		return store.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var store = ResolveStore();
		return store.AppendAsync(aggregateId, aggregateType, events, expectedVersion, cancellationToken);
	}

	private IEventStore ResolveStore()
	{
		var tenantId = _tenantId.Value;
		if (string.IsNullOrEmpty(tenantId))
		{
			throw new InvalidOperationException(
				"Tenant ID is not set. Ensure ITenantId is populated before accessing the event store.");
		}

		return _resolver.Resolve(tenantId);
	}
}
