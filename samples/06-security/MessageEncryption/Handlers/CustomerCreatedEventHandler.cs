// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Security;

using MessageEncryptionSample.Messages;

using Microsoft.Extensions.Logging;

namespace MessageEncryptionSample.Handlers;

/// <summary>
/// Handler that processes customer events and demonstrates field-level decryption.
/// </summary>
public sealed class CustomerCreatedEventHandler : IEventHandler<CustomerCreatedEvent>
{
	private readonly IMessageEncryptionService _encryptionService;
	private readonly ILogger<CustomerCreatedEventHandler> _logger;

	public CustomerCreatedEventHandler(
		IMessageEncryptionService encryptionService,
		ILogger<CustomerCreatedEventHandler> logger)
	{
		_encryptionService = encryptionService;
		_logger = logger;
	}

	public async Task HandleAsync(CustomerCreatedEvent message, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Processing customer created event for {CustomerId}",
			message.CustomerId);

		// Create PII encryption context for decryption
		var piiContext = new EncryptionContext
		{
			Purpose = "pii-field",
			Classification = DataClassification.Confidential
		};

		// Demonstrate decryption for authorized operations only
		// In production, this would be guarded by authorization checks
		try
		{
			var decryptedEmail = await _encryptionService.DecryptMessageAsync(
				message.EncryptedEmail,
				piiContext,
				cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Customer {CustomerId} email decrypted for notification service",
				message.CustomerId);

			// Simulate sending welcome email (decrypted data used temporarily)
			_logger.LogDebug("Would send welcome email to decrypted address");
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to decrypt customer data for {CustomerId}",
				message.CustomerId);
		}
	}
}
