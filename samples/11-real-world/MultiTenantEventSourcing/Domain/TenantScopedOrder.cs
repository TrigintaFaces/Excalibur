// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Domain.Model;

namespace MultiTenantEventSourcing.Domain;

/// <summary>
/// Tenant-scoped order aggregate. The aggregate identity is
/// <c>{tenantId}:{orderId}</c> so it can be uniquely addressed across shards.
/// </summary>
/// <remarks>
/// Two ways to encode tenant isolation in event-sourced aggregates:
/// 1. Composite aggregate id (shown here) -- simple, works across providers.
/// 2. Separate <see cref="Excalibur.Dispatch.ITenantId"/> +
///    <c>EnableTenantSharding()</c> -- framework-managed, picks the right shard
///    per operation.
/// This sample uses approach (2) and keeps the aggregate id tenant-free.
/// </remarks>
public sealed class TenantScopedOrder : AggregateRoot<Guid>
{
	public TenantScopedOrder() { }
	public TenantScopedOrder(Guid id) : base(id) { }

	public decimal Total { get; private set; }

	public static TenantScopedOrder Create(Guid id, decimal total)
	{
		var order = new TenantScopedOrder(id);
		order.RaiseEvent(new OrderCreated(id, total));
		return order;
	}

	/// <inheritdoc />
	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case OrderCreated c: Id = c.OrderId; Total = c.Total; break;
		}
	}
}

/// <summary>Order created event.</summary>
public sealed record OrderCreated(Guid OrderId, decimal Total) : DomainEvent;
