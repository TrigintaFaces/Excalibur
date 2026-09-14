using Excalibur.Dispatch;

namespace Company.ExcaliburDdd.Domain.Events;

/// <summary>
/// Raised when an order is shipped.
/// </summary>
[MessageName("Contoso.Orders.OrderShipped")]
public sealed record OrderShipped : IDomainEvent
{
    public Guid OrderId { get; init; }

    /// <inheritdoc />
    public string EventId { get; init; } = Guid.NewGuid().ToString();

    public string AggregateId { get; init; } = string.Empty;

    public long Version { get; init; }

    /// <inheritdoc />
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;


    /// <inheritdoc />
    public IDictionary<string, object>? Metadata { get; init; }
}
