// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace ExcaliburCqrs.Domain.Events;

/// <summary>
/// Event raised when a new order is created.
/// </summary>
public sealed record OrderCreated(Guid OrderId, string ProductId, int Quantity) : DomainEvent;

/// <summary>
/// Event raised when an item is added to an existing order.
/// </summary>
public sealed record OrderItemAdded(Guid OrderId, string ProductId, int Quantity) : DomainEvent;

/// <summary>
/// Event raised when an order is confirmed.
/// </summary>
public sealed record OrderConfirmed(Guid OrderId) : DomainEvent
{
	/// <summary>Gets when the order was confirmed.</summary>
	public DateTime ConfirmedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when an order is shipped.
/// </summary>
public sealed record OrderShipped(Guid OrderId, string TrackingNumber) : DomainEvent
{
	/// <summary>Gets when the order was shipped.</summary>
	public DateTime ShippedAt { get; init; } = DateTime.UtcNow;
}
