// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

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
	public Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		var store = ResolveStore();
		return store.LoadAsync<TSagaState>(sagaId, cancellationToken);
	}

	/// <inheritdoc />
	public Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		var store = ResolveStore();
		return store.SaveAsync(sagaState, cancellationToken);
	}

	private ISagaStore ResolveStore()
	{
		var tenantId = _tenantId.Value;
		if (string.IsNullOrEmpty(tenantId))
		{
			throw new InvalidOperationException(
				"Tenant ID is not set. Ensure ITenantId is populated before accessing the saga store.");
		}

		return _resolver.Resolve(tenantId);
	}
}
