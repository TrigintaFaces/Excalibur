// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Decorators;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Fail-closed tenant-scoping decorator for <see cref="IEventStore"/> (the row-discriminator multi-tenancy
/// strategy). Every operation requires an ambient tenant: when none is resolved it throws
/// <see cref="TenantRequiredException"/> rather than proceeding with an unscoped (false-isolation) operation.
/// </summary>
/// <remarks>
/// <para>
/// This decorator enforces the fail-closed guarantee at the tenant-facing surface. It delegates to the inner
/// store, which reads the same ambient <see cref="ITenantContext"/> and applies the <c>TenantId</c> row
/// predicate inside the same atomic SQL statement — so isolation is enforced by the store's own query, not by
/// a client-side post-filter.
/// </para>
/// <para>
/// Registered only when multi-tenancy uses the row-discriminator strategy. Non-multi-tenant deployments use
/// the bare store, whose behavior is unchanged.
/// </para>
/// <para>
/// The base <see cref="DelegatingEventStore"/> forwards <see cref="IEventStoreErasure"/> to the inner store, so
/// erasure survives the decoration chain. This decorator <b>overrides</b> the erase path to apply the same
/// fail-closed tenant guard that reads and appends have — an unscoped erase can never emit a predicate-less
/// statement across every tenant's rows; it throws <see cref="TenantRequiredException"/> before delegating.
/// </para>
/// </remarks>
public sealed class TenantScopedEventStore : DelegatingEventStore
{
	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantScopedEventStore"/> class.
	/// </summary>
	/// <param name="inner">The inner event store that performs the tenant-scoped persistence.</param>
	/// <param name="tenantContext">The ambient tenant context whose presence is required (fail-closed).</param>
	public TenantScopedEventStore(IEventStore inner, ITenantContext tenantContext)
		: base(inner)
	{
		ArgumentNullException.ThrowIfNull(tenantContext);

		_tenantContext = tenantContext;
	}

	/// <inheritdoc />
	public override ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		RequireTenant();
		return base.LoadAsync(aggregateId, aggregateType, cancellationToken);
	}

	/// <inheritdoc />
	public override ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		RequireTenant();
		return base.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken);
	}

	/// <inheritdoc />
	public override ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		RequireTenant();
		return base.AppendAsync(aggregateId, aggregateType, events, expectedVersion, cancellationToken);
	}

	/// <inheritdoc />
	public override Task<int> EraseEventsAsync(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		CancellationToken cancellationToken)
	{
		RequireTenant();
		return base.EraseEventsAsync(aggregateId, aggregateType, erasureRequestId, cancellationToken);
	}

	/// <inheritdoc />
	public override Task<bool> IsErasedAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		RequireTenant();
		return base.IsErasedAsync(aggregateId, aggregateType, cancellationToken);
	}

	private void RequireTenant()
	{
		// Unify the empty-tenant predicate to IsNullOrWhiteSpace, matching TenantScope.Scoped so whitespace
		// yields exactly one exception type (TenantRequiredException) across the decorator and the request type.
		if (string.IsNullOrWhiteSpace(_tenantContext.TenantId))
		{
			throw new TenantRequiredException();
		}
	}
}
