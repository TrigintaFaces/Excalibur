// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Decorator that routes <see cref="IProjectionStore{TProjection}"/> operations
/// to the correct tenant's shard based on the current <see cref="ITenantId"/>.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
internal sealed class TenantRoutingProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	private readonly ITenantStoreResolver<IProjectionStore<TProjection>> _resolver;
	private readonly ITenantId _tenantId;

	internal TenantRoutingProjectionStore(
		ITenantStoreResolver<IProjectionStore<TProjection>> resolver,
		ITenantId tenantId)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		ArgumentNullException.ThrowIfNull(tenantId);

		_resolver = resolver;
		_tenantId = tenantId;
	}

	/// <inheritdoc />
	public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		var store = ResolveStore();
		return store.GetByIdAsync(id, cancellationToken);
	}

	/// <inheritdoc />
	public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		var store = ResolveStore();
		return store.UpsertAsync(id, projection, cancellationToken);
	}

	/// <inheritdoc />
	public Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		var store = ResolveStore();
		return store.DeleteAsync(id, cancellationToken);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		var store = ResolveStore();
		return store.QueryAsync(filters, options, cancellationToken);
	}

	/// <inheritdoc />
	public Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		var store = ResolveStore();
		return store.CountAsync(filters, cancellationToken);
	}

	private IProjectionStore<TProjection> ResolveStore()
	{
		var tenantId = _tenantId.Value;
		if (string.IsNullOrEmpty(tenantId))
		{
			throw new InvalidOperationException(
				"Tenant ID is not set. Ensure ITenantId is populated before accessing the projection store.");
		}

		return _resolver.Resolve(tenantId);
	}
}
