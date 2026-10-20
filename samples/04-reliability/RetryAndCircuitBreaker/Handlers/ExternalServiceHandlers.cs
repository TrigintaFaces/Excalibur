// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

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
	private readonly IRetryPolicy _retry;
	private readonly ILogger<ProcessPaymentHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProcessPaymentHandler"/> class.
	/// </summary>
	public ProcessPaymentHandler(
		FlakyPaymentService paymentService,
		[FromKeyedServices("payment-retry")] IRetryPolicy retry,
		ILogger<ProcessPaymentHandler> logger)
	{
		_paymentService = paymentService;
		_retry = retry;
		_logger = logger;
	}

	/// <inheritdoc/>
	public async Task HandleAsync(ProcessPaymentCommand message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Processing payment {PaymentId} for {Amount:C}",
			message.PaymentId,
			message.Amount);

		var transactionId = await _retry.ExecuteAsync(
			token => _paymentService.ProcessPaymentAsync(message.PaymentId, message.Amount, token),
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
	private readonly ICircuitBreakerPolicy _circuitBreaker;
	private readonly ILogger<CheckInventoryHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CheckInventoryHandler"/> class.
	/// </summary>
	public CheckInventoryHandler(
		UnreliableInventoryService inventoryService,
		[FromKeyedServices("inventory-circuit")] ICircuitBreakerPolicy circuitBreaker,
		ILogger<CheckInventoryHandler> logger)
	{
		_inventoryService = inventoryService;
		_circuitBreaker = circuitBreaker;
		_logger = logger;
	}

	/// <inheritdoc/>
	public async Task HandleAsync(CheckInventoryCommand message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Checking inventory for {Sku}, quantity {Quantity}",
			message.ProductSku,
			message.RequestedQuantity);

		var (available, quantity) = await _circuitBreaker.ExecuteAsync(
			token => _inventoryService.CheckInventoryAsync(message.ProductSku, message.RequestedQuantity, token),
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
	private readonly IBulkheadPolicy _bulkhead;
	private readonly ILogger<SendNotificationHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SendNotificationHandler"/> class.
	/// </summary>
	public SendNotificationHandler(
		SlowNotificationService notificationService,
		[FromKeyedServices("notification-bulkhead")] IBulkheadPolicy bulkhead,
		ILogger<SendNotificationHandler> logger)
	{
		_notificationService = notificationService;
		_bulkhead = bulkhead;
		_logger = logger;
	}

	/// <inheritdoc/>
	public async Task HandleAsync(SendNotificationCommand message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[Handler] Sending {Type} notification to {Recipient}",
			message.NotificationType,
			message.Recipient);

		_ = await _bulkhead.ExecuteAsync(
			token => _notificationService.SendNotificationAsync(
				message.NotificationType, message.Recipient, message.Message, token),
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
