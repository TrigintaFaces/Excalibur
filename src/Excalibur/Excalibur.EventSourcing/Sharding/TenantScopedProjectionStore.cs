// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

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
public sealed class TenantScopedProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	private readonly IProjectionStore<TProjection> _inner;
	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantScopedProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="inner">The inner projection store that performs the tenant-scoped persistence.</param>
	/// <param name="tenantContext">The ambient tenant context whose presence is required (fail-closed).</param>
	public TenantScopedProjectionStore(IProjectionStore<TProjection> inner, ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(inner);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_inner = inner;
		_tenantContext = tenantContext;
	}

	/// <inheritdoc />
	public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		RequireTenant();
		return _inner.GetByIdAsync(id, cancellationToken);
	}

	/// <inheritdoc />
	public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		RequireTenant();
		return _inner.UpsertAsync(id, projection, cancellationToken);
	}

	/// <inheritdoc />
	public Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		RequireTenant();
		return _inner.DeleteAsync(id, cancellationToken);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		RequireTenant();
		return _inner.QueryAsync(filters, options, cancellationToken);
	}

	/// <inheritdoc />
	public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
	{
		RequireTenant();
		return _inner.CountAsync(filters, cancellationToken);
	}

	private void RequireTenant()
	{
		if (string.IsNullOrEmpty(_tenantContext.TenantId))
		{
			throw new TenantRequiredException();
		}
	}
}
