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
public sealed class PaymentProcessedEventHandler : IEventHandler<PaymentProcessedEvent>
{
	private readonly ILogger<PaymentProcessedEventHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="PaymentProcessedEventHandler"/> class.
	/// </summary>
	public PaymentProcessedEventHandler(ILogger<PaymentProcessedEventHandler> logger) => _logger = logger;

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
public sealed class InventoryReservedEventHandler : IEventHandler<InventoryReservedEvent>
{
	private readonly ILogger<InventoryReservedEventHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="InventoryReservedEventHandler"/> class.
	/// </summary>
	public InventoryReservedEventHandler(ILogger<InventoryReservedEventHandler> logger) => _logger = logger;

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
public sealed class OrderFailedEventHandler : IEventHandler<OrderFailedEvent>
{
	private readonly ILogger<OrderFailedEventHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderFailedEventHandler"/> class.
	/// </summary>
	public OrderFailedEventHandler(ILogger<OrderFailedEventHandler> logger) => _logger = logger;

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
