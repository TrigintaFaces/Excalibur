// Order Repository

using Excalibur.EventSourcing.Abstractions;

using MultiProviderQueueProcessor.Domain;

namespace MultiProviderQueueProcessor.Infrastructure;

/// <summary>
/// Repository for Order aggregates backed by SQL Server event store.
/// </summary>
/// <remarks>
/// This uses the high-level <see cref="IEventSourcedRepository{TAggregate}"/> interface
/// which handles all event store operations, serialization, and optimistic concurrency.
/// </remarks>
public sealed class OrderRepository(
	IEventSourcedRepository<Order> repository,
	ILogger<OrderRepository> logger)
{
	/// <summary>
	/// Gets an order by ID.
	/// </summary>
	public async Task<Order?> GetByIdAsync(string orderId, CancellationToken cancellationToken = default)
	{
		var order = await repository.GetByIdAsync(orderId, cancellationToken);

		if (order != null)
		{
			logger.LogDebug("Loaded order {OrderId} at version {Version}", orderId, order.Version);
		}

		return order;
	}

	/// <summary>
	/// Saves an order and its uncommitted events.
	/// </summary>
	public async Task SaveAsync(Order order, CancellationToken cancellationToken = default)
	{
		if (!order.HasUncommittedEvents)
		{
			logger.LogDebug("No uncommitted events for order {OrderId}", order.Id);
			return;
		}

		await repository.SaveAsync(order, order.ETag, cancellationToken);

		logger.LogInformation(
			"Saved order {OrderId} at version {Version}",
			order.Id,
			order.Version);
	}

	/// <summary>
	/// Checks if an order exists.
	/// </summary>
	public Task<bool> ExistsAsync(string orderId, CancellationToken cancellationToken = default)
	{
		return repository.ExistsAsync(orderId, cancellationToken);
	}
}

// Note: This wrapper provides a domain-specific API over the generic repository.
// In simple cases, you might inject IEventSourcedRepository<Order> directly.
