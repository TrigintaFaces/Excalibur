// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Tenant-scoping decorator for <see cref="ISagaStore"/> (the row-discriminator multi-tenancy strategy). The
/// message-handling operations (load and save) refuse an ambient context that resolves no tenant, at the call
/// boundary rather than inside the store, by throwing <see cref="TenantRequiredException"/>; the retention
/// purges are delegated to the inner store, which applies the tenant discriminator itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>What provides isolation.</b> Not this decorator. Every tenant-owned statement binds a tenant term
/// because <see cref="TenantScope"/> has no inhabitant meaning "absent": <see cref="TenantScope.TenantId"/> is
/// total, so a scope always yields a term and a statement carrying no tenant predicate cannot be constructed
/// from one. A saga store resolving its scope through <see cref="TenantScope.FromContext(ITenantContext)"/>
/// therefore fails closed on an unresolved tenant whether or not it is decorated, and applies the
/// <c>TenantId</c> row predicate inside its own atomic SQL — the load filter and the version-gated save match
/// key — so a saga can never be loaded or overwritten across tenants.
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
public sealed class TenantScopedSagaStore : ISagaStore
{
	private readonly ISagaStore _inner;
	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantScopedSagaStore"/> class.
	/// </summary>
	/// <param name="inner">The inner saga store that performs the tenant-scoped persistence.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
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
	[RequiresUnreferencedCode("Delegates to a saga store whose serializer may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Delegates to a saga store that serializes with a reflection-based serializer generating converters at run time.")]
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
}
