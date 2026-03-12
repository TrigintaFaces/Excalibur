// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using GettingStarted.Messages;

namespace GettingStarted.Handlers;

/// <summary>
/// Handles the OrderShippedEvent - updates the order status.
/// </summary>
/// <remarks>
/// Events can have multiple handlers. This handler updates the order status
/// in the store. Another handler could send notifications, update analytics, etc.
/// </remarks>
public class OrderShippedHandler : IEventHandler<OrderShippedEvent>
{
	private readonly IOrderStore _orderStore;
	private readonly ILogger<OrderShippedHandler> _logger;

	public OrderShippedHandler(IOrderStore orderStore, ILogger<OrderShippedHandler> logger)
	{
		_orderStore = orderStore;
		_logger = logger;
	}

	public Task HandleAsync(OrderShippedEvent evt, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Order {OrderId} shipped at {ShippedAt}",
			evt.OrderId,
			evt.ShippedAt);

		_orderStore.UpdateOrderStatus(evt.OrderId, "Shipped");

		return Task.CompletedTask;
	}
}
