// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using EnterpriseOrderProcessing.Domain;

using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;

namespace EnterpriseOrderProcessing.Commands;

/// <summary>
/// Handles <see cref="CreateOrderCommand"/> by creating an <see cref="OrderAggregate"/>
/// and persisting it to the event store via <see cref="IEventSourcedRepository{TAggregate, TKey}"/>.
/// </summary>
public sealed class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _orderRepository;
	private readonly ILogger<CreateOrderHandler> _logger;

	public CreateOrderHandler(
		IEventSourcedRepository<OrderAggregate, Guid> orderRepository,
		ILogger<CreateOrderHandler> logger)
	{
		_orderRepository = orderRepository;
		_logger = logger;
	}

	public async Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		var orderId = Guid.NewGuid();
		var order = new OrderAggregate();
		order.Create(orderId, action.CustomerId, action.CustomerName);

		foreach (var line in action.Lines)
		{
			order.AddLine(line.ProductId, line.Quantity, line.UnitPrice);
		}

		order.Submit();

		await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Order {OrderId} created for customer {CustomerName} with {LineCount} lines, total {Total:C}",
			orderId,
			action.CustomerName,
			action.Lines.Count,
			order.Total);

		return orderId;
	}
}
