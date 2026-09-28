// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Decorators;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Decorator that routes <see cref="IEventStore"/> operations to the correct
/// tenant's shard based on the current <see cref="ITenantContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered as Scoped when <c>IEventSourcingBuilder.EnableTenantSharding(...)</c> is called.
/// All <see cref="IEventStore"/> methods route transparently.
/// </para>
/// <para>
/// Store instances are cached per shard ID via <see cref="ITenantStoreResolver{TStore}"/>
/// to avoid creating new connections per call.
/// </para>
/// <para>
/// <b>Erasure.</b> This store also implements <see cref="IEventStoreErasure"/> and forwards it the same
/// way every other operation is forwarded: resolved to the ambient tenant's shard, never to a single
/// fixed inner. A capability probe (<see cref="GetService"/>) therefore answers for the resolved shard's
/// own store, not for this router's static type — a router over shards that do not support erasure must
/// not claim they do.
/// </para>
/// </remarks>
internal sealed class TenantRoutingEventStore : IEventStore, IEventStoreErasure
{
	private readonly ITenantStoreResolver<IEventStore> _resolver;
	private readonly ITenantContext _tenantContext;

	// Public on a type that is internal sealed, so this widens nothing outside the assembly. It is public
	// because AddTenantAwareStore derives the tenancy mechanism from the PUBLIC constructors, and an
	// internal one is invisible to that probe: the seam would classify this store as having no tenancy
	// mechanism and emit no capability marker, which is the opposite of the truth -- it reads the ambient
	// tenant below and refuses without one.
	public TenantRoutingEventStore(
		ITenantStoreResolver<IEventStore> resolver,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_resolver = resolver;
		_tenantContext = tenantContext;
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

	/// <inheritdoc />
	/// <remarks>
	/// Routed to the ambient tenant's own shard, exactly like <see cref="LoadAsync(string, string, CancellationToken)"/>
	/// and <see cref="AppendAsync"/> — never to a fixed inner. Fails closed with
	/// <see cref="TenantRequiredException"/> when no tenant is ambient, and with
	/// <see cref="NotSupportedException"/> when the resolved shard's own store does not support erasure.
	/// </remarks>
	public Task<int> EraseEventsAsync(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		CancellationToken cancellationToken)
		=> RequireErasure(ResolveStore()).EraseEventsAsync(aggregateId, aggregateType, erasureRequestId, cancellationToken);

	/// <inheritdoc />
	/// <remarks>
	/// Routed to the ambient tenant's own shard. See <see cref="EraseEventsAsync"/> for the failure modes.
	/// </remarks>
	public Task<bool> IsErasedAsync(string aggregateId, string aggregateType, CancellationToken cancellationToken)
		=> RequireErasure(ResolveStore()).IsErasedAsync(aggregateId, aggregateType, cancellationToken);

	/// <summary>
	/// Resolves the <see cref="IEventStoreErasure"/> capability, routed to the ambient tenant's own shard
	/// rather than declared unconditionally by this router's static type.
	/// </summary>
	/// <param name="serviceType">The capability interface being resolved.</param>
	/// <returns>
	/// This router when <paramref name="serviceType"/> is <see cref="IEventStoreErasure"/> and the ambient
	/// tenant's resolved shard provides it; the router itself when <paramref name="serviceType"/> is
	/// <see cref="IEventStore"/>; otherwise <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// A plain type test (<c>this is IEventStoreErasure</c>) would read this class's unconditional interface
	/// declaration and answer <see langword="true"/> for every shard, including one whose own store cannot
	/// erase — the same over-claiming hazard <see cref="Decorators.DelegatingEventStore"/> documents for
	/// decorators. The probe is resolved against the resolved shard instead, so a router over shards that
	/// cannot erase truthfully answers <see langword="null"/>. Resolving requires an ambient tenant, so this
	/// probe fails closed with <see cref="TenantRequiredException"/> exactly as every other operation on
	/// this store does — there is no shard-independent answer to "can this router erase".
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <see langword="null"/>.</exception>
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IEventStoreErasure))
		{
			return ResolveStore().GetService(serviceType) is null ? null : this;
		}

		// The transactional append+outbox capability must be forwarded, not dropped. Answering null here
		// does not degrade the caller loudly: EventSourcedRepository falls back to staging the outbox on
		// its own connection, so the events and the outbox rows stop sharing a transaction and integration
		// events are silently lost whenever the append commits and the staging does not. Over a sharded
		// deployment that is every tenant, on providers that do support the capability.
		//
		// Nothing upstream catches it either. The startup validator probes the DI REGISTRATION rather than
		// the resolved store, and the registration is present because the shard's provider registers it —
		// so startup passes while the resolved chain has lost the capability.
		//
		// The view re-resolves the shard on EVERY call rather than closing over one, which is the whole
		// difference between this router and the decorators it sits beside: they wrap ONE inner store, and
		// the shard this router delegates to is a function of the ambient tenant. Capturing a shard here
		// would pin the first tenant's shard into a capability the next tenant then uses.
		if (serviceType == typeof(ITransactionalEventStore))
		{
			return ResolveStore().GetService(serviceType) is null ? null : new TenantRoutingTransactionalView(this);
		}

		return serviceType.IsInstanceOfType(this) ? this : null;
	}

	/// <summary>
	/// Routes a transactional append+outbox to the ambient tenant's shard, resolving the shard per call.
	/// </summary>
	/// <remarks>
	/// Every member other than the transactional append defers to the router, so the routing and the
	/// fail-closed tenant check stay in one place.
	/// </remarks>
	private sealed class TenantRoutingTransactionalView(TenantRoutingEventStore outer)
		: EventStoreCapabilityView(outer), ITransactionalEventStore
	{
		public ValueTask<AppendResult> AppendWithOutboxStagingAsync(
			string aggregateId,
			string aggregateType,
			IEnumerable<IDomainEvent> events,
			long expectedVersion,
			Func<IDbTransaction, CancellationToken, ValueTask> stageOutbox,
			CancellationToken cancellationToken)
		{
			var shard = outer.ResolveStore();

			// Resolved fresh with the shard, for the same reason the shard itself is: a capability captured
			// at construction would belong to whichever tenant happened to resolve first.
			return shard.GetService(typeof(ITransactionalEventStore)) is ITransactionalEventStore transactional
				? transactional.AppendWithOutboxStagingAsync(
					aggregateId, aggregateType, events, expectedVersion, stageOutbox, cancellationToken)
				: throw new NotSupportedException(
					$"The resolved shard's event store ({shard.GetType().Name}) does not support "
					+ "transactional outbox staging (ITransactionalEventStore). The capability probe "
					+ "answered for a different shard, so the shards backing this deployment do not agree "
					+ "on it.");
		}
	}

	private static IEventStoreErasure RequireErasure(IEventStore resolvedShard)
		=> resolvedShard.GetService(typeof(IEventStoreErasure)) as IEventStoreErasure
			?? throw new NotSupportedException(
				$"The resolved shard's event store ({resolvedShard.GetType().Name}) does not support GDPR erasure (IEventStoreErasure).");

	/// <summary>
	/// Resolves the shard for the ambient tenant, failing closed when none is established.
	/// </summary>
	/// <returns>The event store for the ambient tenant's shard.</returns>
	/// <exception cref="TenantRequiredException">
	/// No tenant is resolved. The guard is <see cref="TenantScope.FromContext(ITenantContext)"/> rather than a
	/// local null check, so this path throws the same documented type as every other tenant-required path in
	/// the framework — a consumer's <c>catch (TenantRequiredException)</c> handler covers routing too.
	/// </exception>
	private IEventStore ResolveStore() => _resolver.Resolve(TenantScope.FromContext(_tenantContext).TenantId);
}
