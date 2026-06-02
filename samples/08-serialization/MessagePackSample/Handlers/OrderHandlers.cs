// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Delivery;

using MessagePackSample.Messages;

using Microsoft.Extensions.Logging;

namespace MessagePackSample.Handlers;

/// <summary>
/// Handles order placed events.
/// </summary>
public sealed class OrderPlacedHandler : IEventHandler<OrderPlacedEvent>
{
	private readonly ILogger<OrderPlacedHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderPlacedHandler"/> class.
	/// </summary>
	public OrderPlacedHandler(ILogger<OrderPlacedHandler> logger) => _logger = logger;

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
public sealed class OrderCancelledHandler : IEventHandler<OrderCancelledEvent>
{
	private readonly ILogger<OrderCancelledHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderCancelledHandler"/> class.
	/// </summary>
	public OrderCancelledHandler(ILogger<OrderCancelledHandler> logger) => _logger = logger;

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
public sealed class OrderShippedHandler : IEventHandler<OrderShippedEvent>
{
	private readonly ILogger<OrderShippedHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderShippedHandler"/> class.
	/// </summary>
	public OrderShippedHandler(ILogger<OrderShippedHandler> logger) => _logger = logger;

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
