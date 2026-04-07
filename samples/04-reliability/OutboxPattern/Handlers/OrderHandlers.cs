// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.Logging;

using OutboxPattern.Messages;

namespace OutboxPattern.Handlers;

/// <summary>
/// Handles order placed events.
/// Demonstrates basic event handling with outbox.
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
			"[Handler] Order placed: {OrderId} for customer {CustomerId}, Total: {Amount:C}",
			message.OrderId,
			message.CustomerId,
			message.TotalAmount);

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles payment processed events.
/// </summary>
public sealed class PaymentProcessedHandler : IEventHandler<PaymentProcessedEvent>
{
	private readonly ILogger<PaymentProcessedHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="PaymentProcessedHandler"/> class.
	/// </summary>
	public PaymentProcessedHandler(ILogger<PaymentProcessedHandler> logger) => _logger = logger;

	/// <inheritdoc/>
	public Task HandleAsync(PaymentProcessedEvent message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Payment processed for order {OrderId}: Transaction {TransactionId}, Amount: {Amount:C}",
			message.OrderId,
			message.TransactionId,
			message.Amount);

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles inventory reserved events.
/// </summary>
public sealed class InventoryReservedHandler : IEventHandler<InventoryReservedEvent>
{
	private readonly ILogger<InventoryReservedHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="InventoryReservedHandler"/> class.
	/// </summary>
	public InventoryReservedHandler(ILogger<InventoryReservedHandler> logger) => _logger = logger;

	/// <inheritdoc/>
	public Task HandleAsync(InventoryReservedEvent message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Inventory reserved for order {OrderId}: {Quantity}x {ProductSku}",
			message.OrderId,
			message.Quantity,
			message.ProductSku);

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles order failed events.
/// </summary>
public sealed class OrderFailedHandler : IEventHandler<OrderFailedEvent>
{
	private readonly ILogger<OrderFailedHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderFailedHandler"/> class.
	/// </summary>
	public OrderFailedHandler(ILogger<OrderFailedHandler> logger) => _logger = logger;

	/// <inheritdoc/>
	public Task HandleAsync(OrderFailedEvent message, CancellationToken cancellationToken)
	{
		_logger.LogWarning(
			"[Handler] Order failed: {OrderId}, Reason: {Reason}",
			message.OrderId,
			message.Reason);

		return Task.CompletedTask;
	}
}
