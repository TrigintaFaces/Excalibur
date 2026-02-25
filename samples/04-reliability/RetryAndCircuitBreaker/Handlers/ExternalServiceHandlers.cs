// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.Logging;

using RetryAndCircuitBreaker.Messages;
using RetryAndCircuitBreaker.Services;

namespace RetryAndCircuitBreaker.Handlers;

/// <summary>
/// Handles payment processing commands using the flaky external service.
/// Demonstrates retry pattern with exponential backoff.
/// </summary>
public sealed class ProcessPaymentHandler : IActionHandler<ProcessPaymentCommand>
{
	private readonly FlakyPaymentService _paymentService;
	private readonly ILogger<ProcessPaymentHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProcessPaymentHandler"/> class.
	/// </summary>
	public ProcessPaymentHandler(
		FlakyPaymentService paymentService,
		ILogger<ProcessPaymentHandler> logger)
	{
		_paymentService = paymentService;
		_logger = logger;
	}

	/// <inheritdoc/>
	public async Task HandleAsync(ProcessPaymentCommand message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Processing payment {PaymentId} for {Amount:C}",
			message.PaymentId,
			message.Amount);

		var transactionId = await _paymentService.ProcessPaymentAsync(
			message.PaymentId,
			message.Amount,
			cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"[Handler] Payment {PaymentId} completed with transaction {TransactionId}",
			message.PaymentId,
			transactionId);
	}
}

/// <summary>
/// Handles inventory check commands using the unreliable external service.
/// Demonstrates circuit breaker pattern.
/// </summary>
public sealed class CheckInventoryHandler : IActionHandler<CheckInventoryCommand>
{
	private readonly UnreliableInventoryService _inventoryService;
	private readonly ILogger<CheckInventoryHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CheckInventoryHandler"/> class.
	/// </summary>
	public CheckInventoryHandler(
		UnreliableInventoryService inventoryService,
		ILogger<CheckInventoryHandler> logger)
	{
		_inventoryService = inventoryService;
		_logger = logger;
	}

	/// <inheritdoc/>
	public async Task HandleAsync(CheckInventoryCommand message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Checking inventory for {Sku}, quantity {Quantity}",
			message.ProductSku,
			message.RequestedQuantity);

		var (available, quantity) = await _inventoryService.CheckInventoryAsync(
			message.ProductSku,
			message.RequestedQuantity,
			cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"[Handler] Inventory check complete: {Sku} has {Quantity} available, requested {Requested}",
			message.ProductSku,
			quantity,
			message.RequestedQuantity);
	}
}

/// <summary>
/// Handles notification sending commands using the slow external service.
/// Demonstrates timeout handling.
/// </summary>
public sealed class SendNotificationHandler : IActionHandler<SendNotificationCommand>
{
	private readonly SlowNotificationService _notificationService;
	private readonly ILogger<SendNotificationHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SendNotificationHandler"/> class.
	/// </summary>
	public SendNotificationHandler(
		SlowNotificationService notificationService,
		ILogger<SendNotificationHandler> logger)
	{
		_notificationService = notificationService;
		_logger = logger;
	}

	/// <inheritdoc/>
	public async Task HandleAsync(SendNotificationCommand message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Sending {Type} notification to {Recipient}",
			message.NotificationType,
			message.Recipient);

		_ = await _notificationService.SendNotificationAsync(
			message.NotificationType,
			message.Recipient,
			message.Message,
			cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"[Handler] Notification sent to {Recipient}",
			message.Recipient);
	}
}

/// <summary>
/// Handles payment succeeded events.
/// </summary>
public sealed class PaymentSucceededHandler : IEventHandler<PaymentSucceededEvent>
{
	private readonly ILogger<PaymentSucceededHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="PaymentSucceededHandler"/> class.
	/// </summary>
	public PaymentSucceededHandler(ILogger<PaymentSucceededHandler> logger) => _logger = logger;

	/// <inheritdoc/>
	public Task HandleAsync(PaymentSucceededEvent message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Payment succeeded: {PaymentId} -> {TransactionId}",
			message.PaymentId,
			message.TransactionId);

		return Task.CompletedTask;
	}
}
