// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;

namespace OutOfBandProjections.Domain;

/// <summary>
/// Raised when a new order is placed.
/// </summary>
[MessageName("Contoso.Orders.OrderPlaced")]
public sealed record OrderPlaced : DomainEvent
{
    /// <summary>Gets the order this event belongs to — the event's own business data.</summary>
    /// <remarks>
    /// A domain event carries no aggregate id and no stream version. The aggregate id is a parameter
    /// to the event store; the version is assigned by the store at append time and read back on
    /// <c>StoredEvent.Version</c>. <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c> come from
    /// <c>DomainEvent</c>.
    /// </remarks>
    public required string OrderId { get; init; }

    public required string CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Region { get; init; }
}

/// <summary>
/// Raised when an order is shipped.
/// </summary>
[MessageName("Contoso.Orders.OrderShipped")]
public sealed record OrderShipped : DomainEvent
{
    /// <summary>Gets the order this event belongs to — the event's own business data.</summary>
    /// <remarks>
    /// A domain event carries no aggregate id and no stream version. The aggregate id is a parameter
    /// to the event store; the version is assigned by the store at append time and read back on
    /// <c>StoredEvent.Version</c>. <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c> come from
    /// <c>DomainEvent</c>.
    /// </remarks>
    public required string OrderId { get; init; }

    public required string Carrier { get; init; }
    public required string TrackingNumber { get; init; }
}

/// <summary>
/// Raised when an order is cancelled.
/// </summary>
[MessageName("Contoso.Orders.OrderCancelled")]
public sealed record OrderCancelled : DomainEvent
{
    /// <summary>Gets the order this event belongs to — the event's own business data.</summary>
    /// <remarks>
    /// A domain event carries no aggregate id and no stream version. The aggregate id is a parameter
    /// to the event store; the version is assigned by the store at append time and read back on
    /// <c>StoredEvent.Version</c>. <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c> come from
    /// <c>DomainEvent</c>.
    /// </remarks>
    public required string OrderId { get; init; }

    public required string Reason { get; init; }
}
