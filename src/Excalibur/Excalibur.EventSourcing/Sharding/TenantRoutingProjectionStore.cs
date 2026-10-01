// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

	/// <summary>
	/// Forwards the positioned-write capability, re-resolving the shard on every call.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Without this the capability silently disappears for every sharded host.</b> This type
	/// implements <see cref="IProjectionStore{TProjection}"/> directly rather than deriving from the
	/// decorator base, so it inherits the interface's default <c>GetService</c>, which answers only for
	/// interfaces the instance itself implements. A caller asking for the positioned capability got
	/// <see langword="null"/> and fell through to the unconditional write — the double application the
	/// capability exists to prevent, reinstated by a routing layer that never intended to weaken
	/// anything.
	/// </para>
	/// <para>
	/// <b>The view re-resolves per call and never captures a shard.</b> The ambient tenant can change
	/// between calls on the same instance, and a captured shard would send one tenant's write to
	/// another tenant's store.
	/// </para>
	/// </remarks>
	/// <param name="serviceType">The capability being resolved.</param>
	/// <returns>A routing view, or <see langword="null"/> when no shard provides the capability.</returns>
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType.IsInstanceOfType(this))
		{
			return this;
		}

		if (serviceType == typeof(IPositionedProjectionStore<TProjection>))
		{
			// Probed against the CURRENT shard: a topology where the resolved store lacks the
			// capability must report its absence rather than hand back a view that fails later.
			return ResolveStore().GetService(typeof(IPositionedProjectionStore<TProjection>)) is null
				? null
				: new TenantRoutingPositionedView(this);
		}

		return null;
	}

	/// <summary>Routes each positioned call to the shard resolved at that moment.</summary>
	private sealed class TenantRoutingPositionedView(TenantRoutingProjectionStore<TProjection> outer)
		: IPositionedProjectionStore<TProjection>
	{
		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public Task<(TProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(
			string id, CancellationToken cancellationToken) =>
			Resolve().GetWithPositionAsync(id, cancellationToken);

		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public Task UpsertUnnumberedAsync(
			string id, TProjection projection, CancellationToken cancellationToken) =>
			Resolve().UpsertUnnumberedAsync(id, projection, cancellationToken);

		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
			string id, TProjection projection, long? expectedPosition, long newPosition,
			CancellationToken cancellationToken) =>
			Resolve().UpsertAtPositionAsync(id, projection, expectedPosition, newPosition, cancellationToken);

		/// <inheritdoc />
		/// <remarks>
		/// Routed to the tenant's own shard, like every other operation here. A re-fold that reached a
		/// different shard from the read that produced its position would compare a position against a
		/// row that never held it.
		/// </remarks>
		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public Task<ProjectionRefoldResult> RefoldAtPositionAsync(
			string id, TProjection projection, long atPosition, CancellationToken cancellationToken) =>
			Resolve().RefoldAtPositionAsync(id, projection, atPosition, cancellationToken);

		/// <inheritdoc />
		/// <remarks>
		/// Routed to the tenant's own shard, like every other operation here. A rebuild that reached a
		/// different shard would populate one tenant's row from another tenant's replay.
		/// </remarks>
		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public Task<ProjectionRebuildResult> RebuildAtPositionAsync(
			string id, TProjection projection, long newPosition, CancellationToken cancellationToken) =>
			Resolve().RebuildAtPositionAsync(id, projection, newPosition, cancellationToken);

		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
			outer.GetByIdAsync(id, cancellationToken);

		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken) =>
			outer.UpsertAsync(id, projection, cancellationToken);

		public Task DeleteAsync(string id, CancellationToken cancellationToken) =>
			outer.DeleteAsync(id, cancellationToken);

		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public Task<IReadOnlyList<TProjection>> QueryAsync(
			IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
			outer.QueryAsync(filters, options, cancellationToken);

		public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
			outer.CountAsync(filters, cancellationToken);

		private IPositionedProjectionStore<TProjection> Resolve() =>
			outer.ResolveStore().GetService(typeof(IPositionedProjectionStore<TProjection>))
				as IPositionedProjectionStore<TProjection>
			?? throw new InvalidOperationException(
				"The projection store resolved for the current tenant does not support "
				+ "position-conditional writes. Sharded topologies must be homogeneous in this "
				+ "capability: otherwise one tenant silently loses the guarantee the others have.");
	}
}
