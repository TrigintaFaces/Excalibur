using System.Collections.Concurrent;

namespace Company.DispatchApi.Infrastructure;

/// <summary>
/// In-memory order store for demonstration purposes.
/// </summary>
/// <remarks>
/// In a real application, replace this with a database-backed repository
/// (e.g., SQL Server, PostgreSQL, CosmosDB).
/// </remarks>
public sealed class InMemoryOrderStore
{
    private readonly ConcurrentDictionary<Guid, OrderData> _orders = new();

    /// <summary>
    /// Saves an order to the in-memory store.
    /// </summary>
    /// <param name="id">The order identifier.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The quantity ordered.</param>
    /// <param name="status">The order status.</param>
    public void Save(Guid id, string productId, int quantity, string status)
        => _orders[id] = new OrderData(id, productId, quantity, status);

    /// <summary>
    /// Gets an order by its identifier.
    /// </summary>
    /// <param name="id">The order identifier.</param>
    /// <returns>The order data if found; otherwise, <see langword="null"/>.</returns>
    public OrderData? GetById(Guid id)
    {
        _orders.TryGetValue(id, out var order);
        return order;
    }

    /// <summary>
    /// Represents a stored order.
    /// </summary>
    /// <param name="Id">The order identifier.</param>
    /// <param name="ProductId">The product identifier.</param>
    /// <param name="Quantity">The quantity ordered.</param>
    /// <param name="Status">The current order status.</param>
    public record OrderData(Guid Id, string ProductId, int Quantity, string Status);
}
