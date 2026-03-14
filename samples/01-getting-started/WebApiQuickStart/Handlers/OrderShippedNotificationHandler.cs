// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using GettingStarted.Messages;

namespace GettingStarted.Handlers;

/// <summary>
/// Handles the OrderShippedEvent - sends a notification.
/// </summary>
/// <remarks>
/// This demonstrates that events can have multiple handlers.
/// While OrderShippedHandler updates the status, this handler
/// would send customer notifications (email, SMS, etc.).
/// </remarks>
public class OrderShippedNotificationHandler : IEventHandler<OrderShippedEvent>
{
	private readonly ILogger<OrderShippedNotificationHandler> _logger;

	public OrderShippedNotificationHandler(ILogger<OrderShippedNotificationHandler> logger)
	{
		_logger = logger;
	}

	public Task HandleAsync(OrderShippedEvent evt, CancellationToken cancellationToken)
	{
		// In a real application, this would send an email, SMS, push notification, etc.
		_logger.LogInformation(
			"[NOTIFICATION] Your order {OrderId} has been shipped!",
			evt.OrderId);

		return Task.CompletedTask;
	}
}
