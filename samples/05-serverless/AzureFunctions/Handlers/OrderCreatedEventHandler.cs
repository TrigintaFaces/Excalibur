// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using AzureFunctionsSample.Messages;

using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.Logging;

namespace AzureFunctionsSample.Handlers;

/// <summary>
/// Handles order created events.
/// </summary>
public sealed class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
	private readonly ILogger<OrderCreatedEventHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderCreatedEventHandler"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public OrderCreatedEventHandler(ILogger<OrderCreatedEventHandler> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public Task HandleAsync(OrderCreatedEvent eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		_logger.LogInformation(
			"Order {OrderId} created for customer {CustomerId} at {CreatedAt}, total: {TotalAmount:C}",
			eventMessage.OrderId,
			eventMessage.CustomerId,
			eventMessage.CreatedAt,
			eventMessage.TotalAmount);

		// In a real application, you might:
		// 1. Send confirmation email
		// 2. Update analytics
		// 3. Notify warehouse for fulfillment

		return Task.CompletedTask;
	}
}
