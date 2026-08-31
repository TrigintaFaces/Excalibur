// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Decorator that routes <see cref="ISagaStore"/> operations to the correct
/// tenant's shard based on the current <see cref="ITenantContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// Per S4 decision: saga state lives on the initiating tenant's shard.
/// Cross-tenant steps dispatch through the normal pipeline with tenant routing.
/// </para>
/// </remarks>
internal sealed class TenantRoutingSagaStore : ISagaStore
{
	private readonly ITenantStoreResolver<ISagaStore> _resolver;
	private readonly ITenantContext _tenantContext;

	// Binds each saga instance to the tenant it was resolved under, so a later save under a different
	// ambient tenant (cross-tenant step, background timeout, retry on a drifted scope) is detected
	// instead of silently writing to the wrong shard. The decorator is Scoped, so this map lives for the
	// scope and needs no eviction. (ambient-with-guard; see SaveAsync for the structural assert.)
	private readonly ConcurrentDictionary<Guid, string> _loadedTenants = new();

	// Public on a type that is internal sealed, so this widens nothing outside the assembly. It is public
	// because AddTenantAwareStore derives the tenancy mechanism from the PUBLIC constructors, and an
	// internal one is invisible to that probe: the seam would classify this store as having no tenancy
	// mechanism and emit no capability marker, which is the opposite of the truth -- it reads the ambient
	// tenant below and refuses without one.
	public TenantRoutingSagaStore(
		ITenantStoreResolver<ISagaStore> resolver,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_resolver = resolver;
		_tenantContext = tenantContext;
	}

	/// <inheritdoc />
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		var tenantId = ResolveTenant();
		var store = _resolver.Resolve(tenantId);
		var state = await store.LoadAsync<TSagaState>(sagaId, cancellationToken).ConfigureAwait(false);

		if (state is not null)
		{
			// Record the tenant this saga was loaded under so a subsequent save cannot silently
			// cross into a different tenant's shard.
			_loadedTenants[sagaId] = tenantId;
		}

		return state;
	}

	/// <inheritdoc />
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ArgumentNullException.ThrowIfNull(sagaState);

		var tenantId = ResolveTenant();

		// Structural tenant-drift guard: a saga read from tenant A's shard MUST be written back to
		// tenant A's shard. If the ambient tenant changed between load and save, fail loud rather than
		// persist to the wrong shard (silent cross-tenant data leakage / 'saga not found' on the wrong
		// shard). This makes mid-flow tenant drift inexpressible without a saga-row schema change.
		if (_loadedTenants.TryGetValue(sagaState.SagaId, out var loadedTenant)
			&& !string.Equals(loadedTenant, tenantId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Saga '{sagaState.SagaId}' was loaded under tenant '{loadedTenant}' but is being saved under " +
				$"tenant '{tenantId}'. Cross-tenant saga drift is not permitted; a saga's tenant scope must remain " +
				"constant for its lifetime.");
		}

		var store = _resolver.Resolve(tenantId);
		await store.SaveAsync(sagaState, cancellationToken).ConfigureAwait(false);

		// Bind a newly-created saga (no prior load) to the tenant it was first saved under, so any later
		// save in this scope is held to the same tenant.
		_loadedTenants[sagaState.SagaId] = tenantId;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Purges completed sagas on the <b>current ambient tenant's shard</b>, consistent with this decorator's
	/// per-tenant routing model (saga state lives on its initiating tenant's shard). Retention is driven
	/// per-tenant; call once per tenant scope to purge each shard.
	/// </remarks>
	public Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		var tenantId = ResolveTenant();
		var store = _resolver.Resolve(tenantId);
		return store.PurgeCompletedBeforeAsync(threshold, cancellationToken);
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// <b>Not supported by a sharded store, deliberately.</b> An estate-wide purge must reach every shard, and
	/// <see cref="ITenantStoreResolver{TStore}"/> resolves a shard for a tenant it is given — it cannot enumerate
	/// the tenants or shards that exist. There is no way to visit them all from here.
	/// </para>
	/// <para>
	/// This throws rather than forwarding to the ambient tenant's shard, because forwarding would purge one
	/// shard while reporting a count that reads as estate-wide. A partial deletion that reports success is worse
	/// than a refusal: the operator would believe retention ran and every other shard would silently grow. Drive
	/// retention per shard instead — establish each tenant's scope and call
	/// <see cref="PurgeCompletedBeforeAsync"/> once per tenant.
	/// </para>
	/// </remarks>
	public Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			"A tenant-routing saga store cannot purge across all tenants: the shard resolver routes a known " +
			"tenant to its shard and cannot enumerate the shards that exist, so no estate-wide sweep is " +
			"reachable from here. Purging only the ambient tenant's shard would report a count that reads as " +
			"estate-wide while every other shard kept growing. Drive retention per shard: establish each " +
			"tenant's scope and call PurgeCompletedBeforeAsync once per tenant.");

	/// <summary>
	/// Resolves the ambient tenant, failing closed when none is established.
	/// </summary>
	/// <returns>The ambient tenant identifier.</returns>
	/// <exception cref="TenantRequiredException">
	/// No tenant is resolved. The guard is <see cref="TenantScope.FromContext(ITenantContext)"/> rather than a
	/// local null check, so this path throws the same documented type as every other tenant-required path in
	/// the framework — a consumer's <c>catch (TenantRequiredException)</c> handler covers routing too.
	/// </exception>
	private string ResolveTenant() => TenantScope.FromContext(_tenantContext).TenantId;
}
