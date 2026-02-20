// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using MessagePackSample.Messages;

using Microsoft.Extensions.Logging;

namespace MessagePackSample.Handlers;

/// <summary>
/// Handles order placed events.
/// </summary>
public sealed class OrderPlacedEventHandler : IEventHandler<OrderPlacedEvent>
{
	private readonly ILogger<OrderPlacedEventHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderPlacedEventHandler"/> class.
	/// </summary>
	public OrderPlacedEventHandler(ILogger<OrderPlacedEventHandler> logger) => _logger = logger;

	/// <inheritdoc/>
	public Task HandleAsync(OrderPlacedEvent message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Order placed: {OrderId} for customer {CustomerId}, Total: {Amount:C}, Items: {ItemCount}",
			message.OrderId,
			message.CustomerId,
			message.TotalAmount,
			message.Items.Count);

		foreach (var item in message.Items)
		{
			_logger.LogInformation(
				"  - {ProductName} (SKU: {Sku}) x{Quantity} @ {Price:C}",
				item.ProductName,
				item.ProductSku,
				item.Quantity,
				item.UnitPrice);
		}

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles order cancelled events.
/// </summary>
public sealed class OrderCancelledEventHandler : IEventHandler<OrderCancelledEvent>
{
	private readonly ILogger<OrderCancelledEventHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderCancelledEventHandler"/> class.
	/// </summary>
	public OrderCancelledEventHandler(ILogger<OrderCancelledEventHandler> logger) => _logger = logger;

	/// <inheritdoc/>
	public Task HandleAsync(OrderCancelledEvent message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Order cancelled: {OrderId}, Reason: {Reason}, By: {CancelledBy}",
			message.OrderId,
			message.Reason,
			message.CancelledBy);

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles order shipped events.
/// </summary>
public sealed class OrderShippedEventHandler : IEventHandler<OrderShippedEvent>
{
	private readonly ILogger<OrderShippedEventHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderShippedEventHandler"/> class.
	/// </summary>
	public OrderShippedEventHandler(ILogger<OrderShippedEventHandler> logger) => _logger = logger;

	/// <inheritdoc/>
	public Task HandleAsync(OrderShippedEvent message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Order shipped: {OrderId}, Tracking: {TrackingNumber}, Carrier: {Carrier}, ETA: {ETA}",
			message.OrderId,
			message.TrackingNumber,
			message.Carrier,
			message.EstimatedDelivery);

		return Task.CompletedTask;
	}
}
