// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Decorator that routes <see cref="ISagaStore"/> operations to the correct
/// tenant's shard based on the current <see cref="ITenantId"/>.
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
	private readonly ITenantId _tenantId;

	// Binds each saga instance to the tenant it was resolved under, so a later save under a different
	// ambient tenant (cross-tenant step, background timeout, retry on a drifted scope) is detected
	// instead of silently writing to the wrong shard. The decorator is Scoped, so this map lives for the
	// scope and needs no eviction. (93ilgc — ambient-with-guard; see SaveAsync for the structural assert.)
	private readonly ConcurrentDictionary<Guid, string> _loadedTenants = new();

	internal TenantRoutingSagaStore(
		ITenantStoreResolver<ISagaStore> resolver,
		ITenantId tenantId)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		ArgumentNullException.ThrowIfNull(tenantId);

		_resolver = resolver;
		_tenantId = tenantId;
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

	private string ResolveTenant()
	{
		var tenantId = _tenantId.Value;
		if (string.IsNullOrEmpty(tenantId))
		{
			throw new InvalidOperationException(
				"Tenant ID is not set. Ensure ITenantId is populated before accessing the saga store.");
		}

		return tenantId;
	}
}
