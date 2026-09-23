// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using Excalibur.EventSourcing.Decorators;

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Tenant-scoping decorator for <see cref="IProjectionStore{TProjection}"/> (the row-discriminator
/// multi-tenancy strategy). Refuses an operation whose ambient context resolves no tenant, at the call
/// boundary rather than inside the store, by throwing <see cref="TenantRequiredException"/>.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
/// <remarks>
/// <para>
/// <b>What provides isolation.</b> Not this decorator. Every tenant-owned statement binds a tenant term
/// because <see cref="TenantScope"/> has no inhabitant meaning "absent": <see cref="TenantScope.TenantId"/> is
/// total, so a scope always yields a term and a statement carrying no tenant predicate cannot be constructed
/// from one. A store resolving its scope through <see cref="TenantScope.FromContext(ITenantContext)"/>
/// therefore fails closed on an unresolved tenant whether or not it is decorated, and applies the
/// <c>TenantId</c> row predicate inside its own atomic statement — including the version-gated upsert match
/// key — never as a client-side post-filter.
/// </para>
/// <para>
/// <b>What this decorator adds.</b> Defence in depth, and an earlier failure: the refusal moves from inside
/// the store to the call boundary, and a store that does <em>not</em> resolve its scope through
/// <see cref="TenantScope"/> gets a fail-closed boundary it would otherwise lack.
/// </para>
/// <para>
/// <b>What it does not guarantee.</b> It resolves the ambient tenant once; the store it wraps resolves it
/// again. Those reads agree because <see cref="ITenantContext"/> requires the resolved tenant of an execution
/// flow to be stable — the history constraint stated on that contract — not because this decorator pins
/// anything. An implementation that violates that constraint is not made safe by this decorator.
/// </para>
/// <para>
/// Registered only when multi-tenancy uses the row-discriminator strategy.
/// </para>
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

	/// <summary>
	/// Resolves the ambient partition once, and throws if the context resolves none.
	/// </summary>
	/// <remarks>
	/// The gate <em>is</em> the conversion the inner store uses to build its row predicate, rather than a
	/// restatement of it. A restatement is a second definition of "this context resolves a partition", and
	/// two definitions drift: a decorator that tested only null-or-empty admitted a whitespace term that the
	/// conversion refuses, so the gate passed and the operation failed a layer down. Calling the conversion
	/// makes that divergence inexpressible — there is nothing here to keep in step.
	/// </remarks>
	/// <exception cref="TenantRequiredException">The context resolves no tenant.</exception>
	private void RequireTenant() => _ = TenantScope.FromContext(_tenantContext);

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
