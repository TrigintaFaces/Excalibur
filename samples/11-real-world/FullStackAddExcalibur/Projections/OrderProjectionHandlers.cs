// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Delivery;

using FullStackAddExcalibur.Domain;

using Microsoft.Extensions.Logging;

namespace FullStackAddExcalibur.Projections;

/// <summary>
/// Projects <see cref="OrderCreated"/> into the read model store.
/// </summary>
public sealed class OrderCreatedProjectionHandler : IEventHandler<OrderCreated>
{
	private readonly IOrderProjectionStore _store;
	private readonly ILogger<OrderCreatedProjectionHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderCreatedProjectionHandler"/> class.
	/// </summary>
	public OrderCreatedProjectionHandler(
		IOrderProjectionStore store,
		ILogger<OrderCreatedProjectionHandler> logger)
	{
		_store = store;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task HandleAsync(OrderCreated eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		var model = new OrderReadModel
		{
			Id = eventMessage.OrderId.ToString("D"),
			OrderId = eventMessage.OrderId,
			ExternalOrderId = eventMessage.ExternalOrderId,
			CustomerId = eventMessage.CustomerId,
			Status = "Pending",
			TotalAmount = 0m,
			ItemCount = 0,
		};

		await _store.UpsertAsync(model, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Projected OrderCreated for order {OrderId} (external {ExternalOrderId})",
			eventMessage.OrderId,
			eventMessage.ExternalOrderId);
	}
}

/// <summary>
/// Projects <see cref="OrderLineItemAdded"/> into the read model store,
/// accumulating the total amount and item count.
/// </summary>
public sealed class OrderLineItemAddedProjectionHandler : IEventHandler<OrderLineItemAdded>
{
	private readonly IOrderProjectionStore _store;
	private readonly ILogger<OrderLineItemAddedProjectionHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderLineItemAddedProjectionHandler"/> class.
	/// </summary>
	public OrderLineItemAddedProjectionHandler(
		IOrderProjectionStore store,
		ILogger<OrderLineItemAddedProjectionHandler> logger)
	{
		_store = store;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task HandleAsync(OrderLineItemAdded eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		var existing = await _store.GetAsync(eventMessage.OrderId, cancellationToken).ConfigureAwait(false);
		if (existing is null)
		{
			_logger.LogWarning(
				"OrderLineItemAdded received for unknown order {OrderId} — projection may be out of order",
				eventMessage.OrderId);
			return;
		}

		existing.ItemCount += 1;
		existing.TotalAmount += eventMessage.Quantity * eventMessage.UnitPrice;

		await _store.UpsertAsync(existing, cancellationToken).ConfigureAwait(false);
	}
}
