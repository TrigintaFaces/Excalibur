// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace RetryAndCircuitBreaker.Services;

/// <summary>
/// Simulates a flaky payment service for retry pattern demonstration.
/// </summary>
public sealed class FlakyPaymentService
{
	private readonly ILogger<FlakyPaymentService> _logger;
	private readonly Random _random = new();
	private int _callCount;
	private int _successThreshold = 3; // Succeed after this many attempts

	/// <summary>
	/// Initializes a new instance of the <see cref="FlakyPaymentService"/> class.
	/// </summary>
	public FlakyPaymentService(ILogger<FlakyPaymentService> logger)
	{
		_logger = logger;
	}

	/// <summary>
	/// Sets the number of failures before success.
	/// </summary>
	public void SetFailuresBeforeSuccess(int failures)
	{
		_successThreshold = failures + 1;
		_callCount = 0;
	}

	/// <summary>
	/// Resets the call counter.
	/// </summary>
	public void Reset() => _callCount = 0;

	/// <summary>
	/// Processes a payment, failing the first few times.
	/// </summary>
	public async Task<string> ProcessPaymentAsync(string paymentId, decimal amount, CancellationToken cancellationToken)
	{
		await Task.Delay(50, cancellationToken).ConfigureAwait(false); // Simulate network latency

		_callCount++;

		if (_callCount < _successThreshold)
		{
			_logger.LogWarning(
				"[PaymentService] Payment {PaymentId} failed (attempt {Attempt}/{Threshold})",
				paymentId,
				_callCount,
				_successThreshold);

			throw new PaymentServiceException($"Payment processing failed for {paymentId} - temporary error");
		}

		var transactionId = $"TXN-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
		_logger.LogInformation(
			"[PaymentService] Payment {PaymentId} succeeded on attempt {Attempt}: Transaction {TransactionId}",
			paymentId,
			_callCount,
			transactionId);

		return transactionId;
	}
}

/// <summary>
/// Simulates an unreliable inventory service for circuit breaker demonstration.
/// </summary>
public sealed class UnreliableInventoryService
{
	private readonly ILogger<UnreliableInventoryService> _logger;
	private bool _isHealthy = true;
	private int _consecutiveFailures;
	private readonly int _failureThreshold = 3;

	/// <summary>
	/// Initializes a new instance of the <see cref="UnreliableInventoryService"/> class.
	/// </summary>
	public UnreliableInventoryService(ILogger<UnreliableInventoryService> logger)
	{
		_logger = logger;
	}

	/// <summary>
	/// Gets the current service health status.
	/// </summary>
	public bool IsHealthy => _isHealthy;

	/// <summary>
	/// Sets the service health status.
	/// </summary>
	public void SetHealth(bool healthy)
	{
		_isHealthy = healthy;
		if (healthy)
		{
			_consecutiveFailures = 0;
		}
	}

	/// <summary>
	/// Checks inventory, potentially failing if service is unhealthy.
	/// </summary>
	public async Task<(bool Available, int Quantity)> CheckInventoryAsync(string sku, int requested, CancellationToken cancellationToken)
	{
		await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Simulate network latency

		if (!_isHealthy)
		{
			_consecutiveFailures++;
			_logger.LogError(
				"[InventoryService] Service unhealthy - consecutive failures: {Failures}",
				_consecutiveFailures);

			throw new InventoryServiceException($"Inventory service unavailable for SKU {sku}");
		}

		_consecutiveFailures = 0;
		var available = new Random().Next(100) + requested;

		_logger.LogInformation(
			"[InventoryService] SKU {Sku}: {Available} available, {Requested} requested",
			sku,
			available,
			requested);

		return (true, available);
	}

	/// <summary>
	/// Simulates service degradation for demo purposes.
	/// </summary>
	public void SimulateDegradation()
	{
		_consecutiveFailures++;
		if (_consecutiveFailures >= _failureThreshold)
		{
			_isHealthy = false;
			_logger.LogWarning("[InventoryService] Service marked unhealthy after {Failures} failures", _consecutiveFailures);
		}
	}
}

/// <summary>
/// Simulates a slow notification service for timeout demonstration.
/// </summary>
public sealed class SlowNotificationService
{
	private readonly ILogger<SlowNotificationService> _logger;
	private TimeSpan _responseDelay = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Initializes a new instance of the <see cref="SlowNotificationService"/> class.
	/// </summary>
	public SlowNotificationService(ILogger<SlowNotificationService> logger)
	{
		_logger = logger;
	}

	/// <summary>
	/// Sets the simulated response delay.
	/// </summary>
	public void SetResponseDelay(TimeSpan delay)
	{
		_responseDelay = delay;
		_logger.LogInformation("[NotificationService] Response delay set to {Delay}ms", delay.TotalMilliseconds);
	}

	/// <summary>
	/// Sends a notification with configurable delay.
	/// </summary>
	public async Task<bool> SendNotificationAsync(string type, string recipient, string message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"[NotificationService] Sending {Type} to {Recipient}... (delay: {Delay}ms)",
			type,
			recipient,
			_responseDelay.TotalMilliseconds);

		await Task.Delay(_responseDelay, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"[NotificationService] {Type} notification sent to {Recipient}",
			type,
			recipient);

		return true;
	}
}

/// <summary>
/// Exception thrown by payment service.
/// </summary>
public sealed class PaymentServiceException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PaymentServiceException"/> class.
	/// </summary>
	public PaymentServiceException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown by inventory service.
/// </summary>
public sealed class InventoryServiceException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InventoryServiceException"/> class.
	/// </summary>
	public InventoryServiceException(string message) : base(message) { }
}
