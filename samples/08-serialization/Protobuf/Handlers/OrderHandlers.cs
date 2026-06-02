// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Logging;

using ProtobufSample.Messages;

namespace ProtobufSample.Handlers;

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
			"[Handler] Order placed: {OrderId} for customer {CustomerId}, Total: ${Amount:F2}, Product: {Product} x{Qty}",
			message.OrderId,
			message.CustomerId,
			message.TotalAmount,
			message.ProductName,
			message.Quantity);

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
