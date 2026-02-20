// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Security;

using MessageEncryptionSample.Messages;

using Microsoft.Extensions.Logging;

namespace MessageEncryptionSample.Handlers;

/// <summary>
/// Handler that processes payment events and demonstrates decryption of sensitive data.
/// </summary>
public sealed class PaymentProcessedEventHandler : IEventHandler<PaymentProcessedEvent>
{
	private readonly IMessageEncryptionService _encryptionService;
	private readonly ILogger<PaymentProcessedEventHandler> _logger;

	public PaymentProcessedEventHandler(
		IMessageEncryptionService encryptionService,
		ILogger<PaymentProcessedEventHandler> logger)
	{
		_encryptionService = encryptionService;
		_logger = logger;
	}

	public async Task HandleAsync(PaymentProcessedEvent message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Processing payment {PaymentId} for customer {CustomerId}",
			message.PaymentId,
			message.CustomerId);

		// In a real scenario, decrypt sensitive data only when needed
		// This demonstrates secure handling of encrypted fields
		_logger.LogInformation(
			"Payment amount: {Amount} {Currency}, Card: {MaskedCard}",
			message.Amount,
			message.Currency,
			message.MaskedCardNumber);

		// The encrypted card data would only be decrypted for specific operations
		// like fraud detection or refund processing
		_logger.LogDebug(
			"Encrypted card data available for authorized processing");

		await Task.CompletedTask.ConfigureAwait(false);
	}
}
