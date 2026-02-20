using Excalibur.Dispatch.Abstractions;

namespace Company.ExcaliburDdd.Domain.Events;

/// <summary>
/// Raised when an order is shipped.
/// </summary>
public sealed record OrderShipped : IDomainEvent
{
    public Guid OrderId { get; init; }

    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public string AggregateId { get; init; } = string.Empty;

    /// <inheritdoc />
    public long Version { get; init; }

    /// <inheritdoc />
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string EventType => nameof(OrderShipped);

    /// <inheritdoc />
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
