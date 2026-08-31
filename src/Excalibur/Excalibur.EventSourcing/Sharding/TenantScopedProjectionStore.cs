// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.EventSourcing.Decorators;

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Fail-closed tenant-scoping decorator for <see cref="IProjectionStore{TProjection}"/> (the row-discriminator
/// multi-tenancy strategy). Every operation requires an ambient tenant: when none is resolved it throws
/// <see cref="TenantRequiredException"/> rather than proceeding with an unscoped (false-isolation) operation.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
/// <remarks>
/// The decorator delegates to the inner store, which reads the same ambient <see cref="ITenantContext"/> and
/// applies the <c>TenantId</c> row predicate inside the same atomic SQL statement (including the version-gated
/// upsert match key) — isolation is enforced by the store's own query, never a client-side post-filter.
/// Registered only when multi-tenancy uses the row-discriminator strategy.
/// </remarks>
public sealed class TenantScopedProjectionStore<TProjection> : IsolatingProjectionStoreDecorator<TProjection>
	where TProjection : class
{
	private readonly IProjectionStore<TProjection> _inner;
	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantScopedProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="inner">The inner projection store that performs the tenant-scoped persistence.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public TenantScopedProjectionStore(IProjectionStore<TProjection> inner, ITenantContext tenantContext)
		: base(inner)
	{
		ArgumentNullException.ThrowIfNull(tenantContext);

		_inner = Inner;
		_tenantContext = tenantContext;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public override Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		RequireTenant();
		return _inner.GetByIdAsync(id, cancellationToken);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public override Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		RequireTenant();
		return _inner.UpsertAsync(id, projection, cancellationToken);
	}

	/// <inheritdoc />
	public override Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		RequireTenant();
		return _inner.DeleteAsync(id, cancellationToken);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public override Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		RequireTenant();
		return _inner.QueryAsync(filters, options, cancellationToken);
	}

	/// <inheritdoc />
	public override Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
	{
		RequireTenant();
		return _inner.CountAsync(filters, cancellationToken);
	}

	/// <summary>
	/// Wraps a capability of the decorated store so it cannot be reached without the ambient-tenant check.
	/// </summary>
	/// <param name="serviceType">The capability interface being resolved.</param>
	/// <returns>A tenant-checked view over the capability, or <see langword="null"/> when the inner store lacks it.</returns>
	/// <remarks>
	/// The inner store applies the <c>TenantId</c> row predicate inside its own query; this decorator's
	/// contribution is the fail-closed check that an ambient tenant exists at all. A capability handed over
	/// unwrapped would be reachable without that check, so each is fronted by a view that performs it first.
	/// </remarks>
	protected override object? WrapCapability(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IPageableProjectionStore<TProjection>)
			&& Inner.GetService(typeof(IPageableProjectionStore<TProjection>)) is IPageableProjectionStore<TProjection> pageable)
		{
			return new TenantScopedPageableView(this, pageable);
		}

		if (serviceType == typeof(ICursorProjectionStore<TProjection>)
			&& Inner.GetService(typeof(ICursorProjectionStore<TProjection>)) is ICursorProjectionStore<TProjection> cursor)
		{
			return new TenantScopedCursorView(this, cursor);
		}

		return null;
	}

	private void RequireTenant()
	{
		// IsNullOrWhiteSpace, matching TenantScope.Scoped: a whitespace tenant must raise the same
		// TenantRequiredException here as it does inside the inner store's own scope resolution, rather
		// than passing this check and failing one layer down with a different provenance.
		if (string.IsNullOrWhiteSpace(_tenantContext.TenantId))
		{
			throw new TenantRequiredException();
		}
	}

	private sealed class TenantScopedPageableView(
		TenantScopedProjectionStore<TProjection> outer,
		IPageableProjectionStore<TProjection> capability)
		: ProjectionStoreCapabilityView<TProjection>(outer), IPageableProjectionStore<TProjection>
	{
		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public Task<PagedResult<TProjection>> QueryPagedAsync(
			IDictionary<string, object>? filters,
			int pageNumber,
			int pageSize,
			QueryOptions? options,
			CancellationToken cancellationToken)
		{
			outer.RequireTenant();
			return capability.QueryPagedAsync(filters, pageNumber, pageSize, options, cancellationToken);
		}
	}

	private sealed class TenantScopedCursorView(
		TenantScopedProjectionStore<TProjection> outer,
		ICursorProjectionStore<TProjection> capability)
		: ProjectionStoreCapabilityView<TProjection>(outer), ICursorProjectionStore<TProjection>
	{
		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public Task<CursorPagedResult<TProjection>> QueryCursorAsync(
			IDictionary<string, object>? filters,
			string? cursor,
			int pageSize,
			CancellationToken cancellationToken)
		{
			outer.RequireTenant();
			return capability.QueryCursorAsync(filters, cursor, pageSize, cancellationToken);
		}
	}
}
