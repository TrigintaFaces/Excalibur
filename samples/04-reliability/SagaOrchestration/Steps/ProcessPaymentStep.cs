// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

using SagaOrchestration.Sagas;

namespace SagaOrchestration.Steps;

/// <summary>
/// Saga step that processes payment for the order.
/// </summary>
/// <remarks>
/// <para>
/// This step contacts the payment gateway to charge the customer.
/// If successful, it stores the transaction ID in the saga data for later
/// reference or refund during compensation.
/// </para>
/// <para>
/// Compensation issues a refund to the customer.
/// </para>
/// </remarks>
public sealed partial class ProcessPaymentStep : ISagaStep
{
	private readonly ILogger<ProcessPaymentStep> _logger;
	private readonly bool _shouldFail;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProcessPaymentStep"/> class.
	/// </summary>
	public ProcessPaymentStep(ILogger<ProcessPaymentStep> logger, bool shouldFail = false)
	{
		_logger = logger;
		_shouldFail = shouldFail;
	}

	/// <inheritdoc/>
	public string Name => "ProcessPayment";

	/// <inheritdoc/>
	public async Task<bool> ExecuteAsync(OrderSagaData data, CancellationToken cancellationToken)
	{
		LogProcessingPayment(_logger, data.OrderId, data.TotalAmount);

		// Simulate payment gateway call
		await Task.Delay(100, cancellationToken).ConfigureAwait(false);

		// Simulate payment failure for demonstration
		if (_shouldFail)
		{
			LogPaymentDeclined(_logger, data.OrderId, "Insufficient funds");
			data.FailureReason = "Payment declined: Insufficient funds";
			return false;
		}

		// Generate transaction ID (in real implementation, this comes from payment gateway)
		data.PaymentTransactionId = $"TXN-{Guid.NewGuid():N}";

		LogPaymentProcessed(_logger, data.OrderId, data.PaymentTransactionId, data.TotalAmount);

		return true;
	}

	/// <inheritdoc/>
	public async Task<bool> CompensateAsync(OrderSagaData data, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(data.PaymentTransactionId))
		{
			LogNoPaymentToRefund(_logger, data.OrderId);
			return true; // Nothing to compensate
		}

		LogRefundingPayment(_logger, data.OrderId, data.PaymentTransactionId, data.TotalAmount);

		// Simulate payment gateway refund call
		await Task.Delay(80, cancellationToken).ConfigureAwait(false);

		LogPaymentRefunded(_logger, data.OrderId, data.PaymentTransactionId);

		return true;
	}

	[LoggerMessage(Level = LogLevel.Information, Message = "Processing payment for order {OrderId}, Amount: {Amount:C}")]
	private static partial void LogProcessingPayment(ILogger logger, string orderId, decimal amount);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Payment declined for order {OrderId}: {Reason}")]
	private static partial void LogPaymentDeclined(ILogger logger, string orderId, string reason);

	[LoggerMessage(Level = LogLevel.Information,
		Message = "Payment processed for order {OrderId}, TransactionId: {TransactionId}, Amount: {Amount:C}")]
	private static partial void LogPaymentProcessed(ILogger logger, string orderId, string transactionId, decimal amount);

	[LoggerMessage(Level = LogLevel.Debug, Message = "No payment to refund for order {OrderId}")]
	private static partial void LogNoPaymentToRefund(ILogger logger, string orderId);

	[LoggerMessage(Level = LogLevel.Information,
		Message = "Refunding payment for order {OrderId}, TransactionId: {TransactionId}, Amount: {Amount:C}")]
	private static partial void LogRefundingPayment(ILogger logger, string orderId, string transactionId, decimal amount);

	[LoggerMessage(Level = LogLevel.Information, Message = "Payment refunded for order {OrderId}, TransactionId: {TransactionId}")]
	private static partial void LogPaymentRefunded(ILogger logger, string orderId, string transactionId);
}
