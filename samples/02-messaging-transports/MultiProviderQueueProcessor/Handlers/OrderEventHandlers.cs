// Order Event Handlers

using Excalibur.Dispatch.Abstractions.Delivery;

using MultiProviderQueueProcessor.Events;
using MultiProviderQueueProcessor.Projections;

namespace MultiProviderQueueProcessor.Handlers;

/// <summary>
/// Handles order created events for read model updates.
/// </summary>
public sealed class OrderCreatedHandler(
	IOrderProjectionUpdater projectionUpdater,
	ILogger<OrderCreatedHandler> logger) : IEventHandler<OrderCreatedEvent>
{
	public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken)
	{
		logger.LogInformation(
			"Processing OrderCreatedEvent for order {OrderId}, customer {CustomerId}",
			@event.AggregateId,
			@event.CustomerId);

		await projectionUpdater.CreateOrderProjectionAsync(
			@event.AggregateId,
			@event.CustomerId,
			@event.TotalAmount,
			@event.Currency,
			cancellationToken);
	}
}

/// <summary>
/// Handles order item added events for read model updates.
/// </summary>
public sealed class OrderItemAddedHandler(
	IOrderProjectionUpdater projectionUpdater,
	ILogger<OrderItemAddedHandler> logger) : IEventHandler<OrderItemAddedEvent>
{
	public async Task HandleAsync(OrderItemAddedEvent @event, CancellationToken cancellationToken)
	{
		logger.LogInformation(
			"Processing OrderItemAddedEvent for order {OrderId}, product {ProductId}",
			@event.AggregateId,
			@event.ProductId);

		await projectionUpdater.AddOrderItemAsync(
			@event.AggregateId,
			@event.ProductId,
			@event.ProductName,
			@event.Quantity,
			@event.UnitPrice,
			cancellationToken);
	}
}

/// <summary>
/// Handles order submitted events for read model updates.
/// </summary>
public sealed class OrderSubmittedHandler(
	IOrderProjectionUpdater projectionUpdater,
	ILogger<OrderSubmittedHandler> logger) : IEventHandler<OrderSubmittedEvent>
{
	public async Task HandleAsync(OrderSubmittedEvent @event, CancellationToken cancellationToken)
	{
		logger.LogInformation(
			"Processing OrderSubmittedEvent for order {OrderId}",
			@event.AggregateId);

		await projectionUpdater.UpdateOrderStatusAsync(
			@event.AggregateId,
			"Submitted",
			cancellationToken);
	}
}

/// <summary>
/// Handles order shipped events for read model updates.
/// </summary>
public sealed class OrderShippedHandler(
	IOrderProjectionUpdater projectionUpdater,
	ILogger<OrderShippedHandler> logger) : IEventHandler<OrderShippedEvent>
{
	public async Task HandleAsync(OrderShippedEvent @event, CancellationToken cancellationToken)
	{
		logger.LogInformation(
			"Processing OrderShippedEvent for order {OrderId}, tracking {TrackingNumber}",
			@event.AggregateId,
			@event.TrackingNumber);

		await projectionUpdater.MarkOrderShippedAsync(
			@event.AggregateId,
			@event.TrackingNumber,
			@event.Carrier,
			cancellationToken);
	}
}

/// <summary>
/// Handles order cancelled events for read model updates.
/// </summary>
public sealed class OrderCancelledHandler(
	IOrderProjectionUpdater projectionUpdater,
	ILogger<OrderCancelledHandler> logger) : IEventHandler<OrderCancelledEvent>
{
	public async Task HandleAsync(OrderCancelledEvent @event, CancellationToken cancellationToken)
	{
		logger.LogInformation(
			"Processing OrderCancelledEvent for order {OrderId}, reason: {Reason}",
			@event.AggregateId,
			@event.Reason);

		await projectionUpdater.MarkOrderCancelledAsync(
			@event.AggregateId,
			@event.Reason,
			cancellationToken);
	}
}
