// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using GettingStarted.Messages;

namespace GettingStarted.Handlers;

/// <summary>
/// Handles the GetOrderQuery.
/// </summary>
/// <remarks>
/// Query handlers read data without modifying state.
/// This handler retrieves order details from the store.
/// </remarks>
public class GetOrderHandler : IActionHandler<GetOrderQuery, OrderDetails?>
{
	private readonly IOrderStore _orderStore;
	private readonly ILogger<GetOrderHandler> _logger;

	public GetOrderHandler(IOrderStore orderStore, ILogger<GetOrderHandler> logger)
	{
		_orderStore = orderStore;
		_logger = logger;
	}

	public Task<OrderDetails?> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
	{
		_logger.LogInformation("Retrieving order {OrderId}", query.OrderId);

		var order = _orderStore.GetOrder(query.OrderId);

		if (order is null)
		{
			_logger.LogWarning("Order {OrderId} not found", query.OrderId);
		}

		return Task.FromResult(order);
	}
}
