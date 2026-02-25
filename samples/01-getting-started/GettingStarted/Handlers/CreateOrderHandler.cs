// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using GettingStarted.Messages;

namespace GettingStarted.Handlers;

/// <summary>
/// Handles the CreateOrderCommand.
/// </summary>
/// <remarks>
/// Command handlers process intent to change state and return a result.
/// This handler creates a new order and returns the generated order ID.
/// </remarks>
public class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
{
	private readonly IOrderStore _orderStore;
	private readonly ILogger<CreateOrderHandler> _logger;

	public CreateOrderHandler(IOrderStore orderStore, ILogger<CreateOrderHandler> logger)
	{
		_orderStore = orderStore;
		_logger = logger;
	}

	public Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Creating order for product {ProductId} with quantity {Quantity}",
			action.ProductId,
			action.Quantity);

		var orderId = _orderStore.CreateOrder(action.ProductId, action.Quantity);

		_logger.LogInformation("Order {OrderId} created successfully", orderId);

		return Task.FromResult(orderId);
	}
}
