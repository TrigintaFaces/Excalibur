// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;

namespace OutOfBandProjections.Domain;

/// <summary>
/// Raised when a new order is placed.
/// </summary>
public sealed record OrderPlaced : IDomainEvent
{
    public required string EventId { get; init; }
    public required string AggregateId { get; init; }
    public required long Version { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string EventType { get; init; }
    public IDictionary<string, object>? Metadata { get; init; }

    public required string CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Region { get; init; }
}

/// <summary>
/// Raised when an order is shipped.
/// </summary>
public sealed record OrderShipped : IDomainEvent
{
    public required string EventId { get; init; }
    public required string AggregateId { get; init; }
    public required long Version { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string EventType { get; init; }
    public IDictionary<string, object>? Metadata { get; init; }

    public required string Carrier { get; init; }
    public required string TrackingNumber { get; init; }
}

/// <summary>
/// Raised when an order is cancelled.
/// </summary>
public sealed record OrderCancelled : IDomainEvent
{
    public required string EventId { get; init; }
    public required string AggregateId { get; init; }
    public required long Version { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string EventType { get; init; }
    public IDictionary<string, object>? Metadata { get; init; }

    public required string Reason { get; init; }
}
