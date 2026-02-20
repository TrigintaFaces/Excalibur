// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.Logging;

using RabbitMQSample.Messages;

namespace RabbitMQSample.Handlers;

/// <summary>
/// Handles <see cref="OrderPlacedEvent"/> messages received from RabbitMQ.
/// </summary>
/// <remarks>
/// This handler demonstrates how to process messages consumed from RabbitMQ.
/// In a real application, this might trigger notifications, update read models,
/// or initiate downstream processes.
/// </remarks>
public sealed class OrderPlacedEventHandler : IEventHandler<OrderPlacedEvent>
{
	private readonly ILogger<OrderPlacedEventHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderPlacedEventHandler"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public OrderPlacedEventHandler(ILogger<OrderPlacedEventHandler> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task HandleAsync(OrderPlacedEvent eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		_logger.LogInformation(
			"Received OrderPlacedEvent: OrderId={OrderId}, CustomerId={CustomerId}, Amount={Amount:C}",
			eventMessage.OrderId,
			eventMessage.CustomerId,
			eventMessage.TotalAmount);

		// In a real application, you might:
		// - Send confirmation emails
		// - Update inventory
		// - Trigger payment processing
		// - Update analytics dashboards

		return Task.CompletedTask;
	}
}
