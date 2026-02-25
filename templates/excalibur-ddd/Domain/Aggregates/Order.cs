using Company.ExcaliburDdd.Domain.Events;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace Company.ExcaliburDdd.Domain.Aggregates;

/// <summary>
/// Order aggregate root demonstrating event sourcing with pattern matching.
/// </summary>
public class Order : AggregateRoot<Guid>
{
    private readonly List<OrderItem> _items = [];

    /// <summary>
    /// Gets the current status of the order.
    /// </summary>
    public OrderStatus Status { get; private set; }

    /// <summary>
    /// Gets the order line items.
    /// </summary>
    public IReadOnlyList<OrderItem> Items => _items;

    /// <summary>
    /// Parameterless constructor required for rehydration.
    /// </summary>
    public Order() { }

    /// <summary>
    /// Creates a new Order aggregate with the specified identifier.
    /// </summary>
    public Order(Guid id) : base(id) { }

    /// <summary>
    /// Factory method to create a new order.
    /// </summary>
    public static Order Create(Guid id, string productId, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var order = new Order(id);
        order.RaiseEvent(new OrderCreated
        {
            OrderId = id,
            ProductId = productId,
            Quantity = quantity,
            AggregateId = id.ToString(),
            Version = order.Version
        });
        return order;
    }

    /// <summary>
    /// Marks the order as shipped.
    /// </summary>
    public void Ship()
    {
        if (Status != OrderStatus.Created)
            throw new InvalidOperationException($"Cannot ship order in status {Status}.");

        RaiseEvent(new OrderShipped
        {
            OrderId = Id,
            AggregateId = Id.ToString(),
            Version = Version
        });
    }

    /// <summary>
    /// Applies domain events using pattern matching (no reflection).
    /// </summary>
    protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
    {
        OrderCreated e => Apply(e),
        OrderShipped e => Apply(e),
        _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}")
    };

    private bool Apply(OrderCreated e)
    {
        Id = e.OrderId;
        Status = OrderStatus.Created;
        _items.Add(new OrderItem(e.ProductId, e.Quantity));
        return true;
    }

    private bool Apply(OrderShipped e)
    {
        Status = OrderStatus.Shipped;
        return true;
    }
}

/// <summary>
/// Represents the status of an order.
/// </summary>
public enum OrderStatus
{
    /// <summary>Order has been created.</summary>
    Created,

    /// <summary>Order has been shipped.</summary>
    Shipped,

    /// <summary>Order has been cancelled.</summary>
    Cancelled
}

/// <summary>
/// Represents a line item in an order.
/// </summary>
public sealed record OrderItem(string ProductId, int Quantity);
