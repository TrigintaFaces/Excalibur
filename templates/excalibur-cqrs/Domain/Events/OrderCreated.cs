using Excalibur.Dispatch.Abstractions;

namespace Company.ExcaliburCqrs.Domain.Events;

/// <summary>
/// Raised when a new order is created.
/// </summary>
public sealed record OrderCreated : IDomainEvent
{
    public Guid OrderId { get; init; }
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }

    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public string AggregateId { get; init; } = string.Empty;

    /// <inheritdoc />
    public long Version { get; init; }

    /// <inheritdoc />
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string EventType => nameof(OrderCreated);

    /// <inheritdoc />
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
