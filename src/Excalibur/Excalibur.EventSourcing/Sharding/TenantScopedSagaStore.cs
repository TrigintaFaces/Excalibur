// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Fail-closed tenant-scoping decorator for <see cref="ISagaStore"/> (the row-discriminator multi-tenancy
/// strategy). The message-handling operations (load and save) require an ambient tenant and throw
/// <see cref="TenantRequiredException"/> when none is resolved; the retention purges are delegated to the
/// inner store, which applies the tenant discriminator itself.
/// </summary>
/// <remarks>
/// The decorator delegates to the inner saga store, which reads the same ambient <see cref="ITenantContext"/>
/// and applies the <c>TenantId</c> row predicate inside the same atomic SQL (the load filter and the
/// version-gated save match key) — so a saga can never be loaded or overwritten across tenants. Registered
/// only when multi-tenancy uses the row-discriminator strategy.
/// </remarks>
public sealed class TenantScopedSagaStore : ISagaStore
{
	private readonly ISagaStore _inner;
	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantScopedSagaStore"/> class.
	/// </summary>
	/// <param name="inner">The inner saga store that performs the tenant-scoped persistence.</param>
	/// <param name="tenantContext">The ambient tenant context whose presence is required on the handling path.</param>
	public TenantScopedSagaStore(ISagaStore inner, ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(inner);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_inner = inner;
		_tenantContext = tenantContext;
	}

	/// <inheritdoc />
	public Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		RequireTenant();
		return _inner.LoadAsync<TSagaState>(sagaId, cancellationToken);
	}

	/// <inheritdoc />
	public Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		RequireTenant();
		return _inner.SaveAsync(sagaState, cancellationToken);
	}

	/// <inheritdoc />
	/// <remarks>
	/// Delegated to the inner store, which applies the tenant discriminator inside its own SQL. No ambient tenant
	/// is required here: with none established the inner store purges the untenanted partition, which is a real
	/// scope rather than a missing one, and requiring a tenant would strand those rows — unreachable for
	/// retention and growing without bound.
	/// </remarks>
	public Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
		=> _inner.PurgeCompletedBeforeAsync(threshold, cancellationToken);

	/// <inheritdoc />
	/// <remarks>
	/// Forwards to the inner store. Required rather than optional: the interface member's default implementation
	/// throws, so omitting this override would make a decorated store report estate-wide purge as unsupported
	/// even when the inner store supports it.
	/// </remarks>
	public Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
		=> _inner.PurgeAllTenantsCompletedBeforeAsync(threshold, cancellationToken);

	private void RequireTenant()
	{
		if (string.IsNullOrEmpty(_tenantContext.TenantId))
		{
			throw new TenantRequiredException();
		}
	}
}
