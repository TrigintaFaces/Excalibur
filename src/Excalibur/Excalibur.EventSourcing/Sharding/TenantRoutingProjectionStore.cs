// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Decorator that routes <see cref="IProjectionStore{TProjection}"/> operations
/// to the correct tenant's shard based on the current <see cref="ITenantContext"/>.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
internal sealed class TenantRoutingProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	private readonly ITenantStoreResolver<IProjectionStore<TProjection>> _resolver;
	private readonly ITenantContext _tenantContext;

	internal TenantRoutingProjectionStore(
		ITenantStoreResolver<IProjectionStore<TProjection>> resolver,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_resolver = resolver;
		_tenantContext = tenantContext;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		var store = ResolveStore();
		return store.GetByIdAsync(id, cancellationToken);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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

	/// <summary>
	/// Resolves the shard for the ambient tenant, failing closed when none is established.
	/// </summary>
	/// <returns>The projection store for the ambient tenant's shard.</returns>
	/// <exception cref="TenantRequiredException">
	/// No tenant is resolved. The guard is <see cref="TenantScope.FromContext(ITenantContext)"/> rather than a
	/// local null check, so this path throws the same documented type as every other tenant-required path in
	/// the framework — a consumer's <c>catch (TenantRequiredException)</c> handler covers routing too.
	/// </exception>
	private IProjectionStore<TProjection> ResolveStore() => _resolver.Resolve(TenantScope.FromContext(_tenantContext).TenantId);
}
