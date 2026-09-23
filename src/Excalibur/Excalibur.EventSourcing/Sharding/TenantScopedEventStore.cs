// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using System.Data;

using Excalibur.EventSourcing.Decorators;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Tenant-scoping decorator for <see cref="IEventStore"/> (the row-discriminator multi-tenancy strategy).
/// Refuses an operation whose ambient context resolves no tenant, at the call boundary rather than inside the
/// store, by throwing <see cref="TenantRequiredException"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What provides isolation.</b> Not this decorator. Every tenant-owned statement binds a tenant term
/// because <see cref="TenantScope"/> has no inhabitant meaning "absent": <see cref="TenantScope.TenantId"/> is
/// total, so a scope always yields a term and a statement carrying no tenant predicate cannot be constructed
/// from one. A store resolving its scope through <see cref="TenantScope.FromContext(ITenantContext)"/>
/// therefore fails closed on an unresolved tenant whether or not it is decorated, and applies the
/// <c>TenantId</c> row predicate inside its own atomic statement rather than as a client-side post-filter.
/// </para>
/// <para>
/// <b>What this decorator adds.</b> Defence in depth, and an earlier failure. It moves the refusal from inside
/// the store — mid-operation, possibly mid-transaction — to the call boundary, before a connection is opened;
/// and it gives a store that does <em>not</em> resolve its scope through <see cref="TenantScope"/> a
/// fail-closed boundary it would otherwise lack.
/// </para>
/// <para>
/// <b>What it does not guarantee.</b> It resolves the ambient tenant once; the store it wraps resolves it
/// again. Those reads agree because <see cref="ITenantContext"/> requires the resolved tenant of an execution
/// flow to be stable — the history constraint stated on that contract — not because this decorator pins
/// anything. An implementation that violates that constraint is not made safe by this decorator.
/// </para>
/// <para>
/// Registered only when multi-tenancy uses the row-discriminator strategy. Non-multi-tenant deployments use
/// the bare store, whose behavior is unchanged.
/// </para>
/// <para>
/// The base <see cref="DelegatingEventStore"/> forwards <see cref="IEventStoreErasure"/> to the inner store, so
/// erasure survives the decoration chain. This decorator <b>overrides</b> the erase path so that the erase
/// boundary refuses an unresolved tenant exactly as reads and appends do, rather than letting the refusal
/// surface from inside the store part-way through.
/// </para>
/// </remarks>
public sealed class TenantScopedEventStore : IsolatingEventStoreDecorator
{
	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantScopedEventStore"/> class.
	/// </summary>
	/// <param name="inner">The inner event store that performs the tenant-scoped persistence.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
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

	/// <summary>
	/// Wraps a capability of the decorated store so it cannot be reached without the ambient-tenant check.
	/// </summary>
	/// <param name="serviceType">The capability interface being resolved.</param>
	/// <returns>A tenant-checked view over the capability, or <see langword="null"/> when the inner store lacks it.</returns>
	/// <remarks>
	/// The inner store applies the tenant predicate inside its own statement; this decorator's contribution
	/// is the fail-closed check that an ambient tenant exists at all. A capability handed over unwrapped
	/// would be reachable without that check, so each is fronted by a view that performs it first.
	/// </remarks>
	protected override object? WrapCapability(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(ITransactionalEventStore)
			&& Inner.GetService(typeof(ITransactionalEventStore)) is ITransactionalEventStore transactional)
		{
			return new TenantScopedTransactionalView(this, transactional);
		}

		if (serviceType == typeof(IEventStoreArchive)
			&& Inner.GetService(typeof(IEventStoreArchive)) is IEventStoreArchive archive)
		{
			return new TenantScopedArchiveView(this, archive);
		}

		return null;
	}

	private sealed class TenantScopedTransactionalView(
		TenantScopedEventStore outer,
		ITransactionalEventStore capability)
		: EventStoreCapabilityView(outer), ITransactionalEventStore
	{
		public ValueTask<AppendResult> AppendWithOutboxStagingAsync(
			string aggregateId,
			string aggregateType,
			IEnumerable<IDomainEvent> events,
			long expectedVersion,
			Func<IDbTransaction, CancellationToken, ValueTask> stageOutbox,
			CancellationToken cancellationToken)
		{
			outer.RequireTenant();
			return capability.AppendWithOutboxStagingAsync(
				aggregateId, aggregateType, events, expectedVersion, stageOutbox, cancellationToken);
		}
	}

	private sealed class TenantScopedArchiveView(TenantScopedEventStore outer, IEventStoreArchive capability)
		: IEventStoreArchive
	{
		public Task<IReadOnlyList<ArchiveCandidate>> GetArchiveCandidatesAsync(
			ArchivePolicy policy,
			int batchSize,
			CancellationToken cancellationToken)
		{
			outer.RequireTenant();
			return capability.GetArchiveCandidatesAsync(policy, batchSize, cancellationToken);
		}

		public Task<int> DeleteEventsUpToVersionAsync(
			KeyedTenantPartition tenant,
			string aggregateId,
			string aggregateType,
			long toVersion,
			CancellationToken cancellationToken)
		{
			outer.RequireTenant();
			return capability.DeleteEventsUpToVersionAsync(
				tenant, aggregateId, aggregateType, toVersion, cancellationToken);
		}
	}
}
