// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace ExcaliburCqrs.Domain.Events;

/// <summary>
/// Event raised when a new order is created.
/// </summary>
[MessageName("Contoso.Orders.OrderCreated")]
public sealed record OrderCreated(Guid OrderId, string ProductId, int Quantity) : DomainEvent;

/// <summary>
/// Event raised when an item is added to an existing order.
/// </summary>
[MessageName("Contoso.Orders.OrderItemAdded")]
public sealed record OrderItemAdded(Guid OrderId, string ProductId, int Quantity) : DomainEvent;

/// <summary>
/// Event raised when an order is confirmed.
/// </summary>
[MessageName("Contoso.Orders.OrderConfirmed")]
public sealed record OrderConfirmed(Guid OrderId) : DomainEvent
{
	/// <summary>Gets when the order was confirmed.</summary>
	public DateTime ConfirmedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when an order is shipped.
/// </summary>
[MessageName("Contoso.Orders.OrderShipped")]
public sealed record OrderShipped(Guid OrderId, string TrackingNumber) : DomainEvent
{
	/// <summary>Gets when the order was shipped.</summary>
	public DateTime ShippedAt { get; init; } = DateTime.UtcNow;
}
