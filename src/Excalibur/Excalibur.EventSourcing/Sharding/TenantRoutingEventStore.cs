// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;

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
/// </remarks>
internal sealed class TenantRoutingEventStore : IEventStore
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
