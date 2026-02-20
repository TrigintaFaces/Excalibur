// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.Logging;

using ProtobufSample.Messages;

namespace ProtobufSample.Handlers;

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
