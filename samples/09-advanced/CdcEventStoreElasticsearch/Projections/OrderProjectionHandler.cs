// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcEventStoreElasticsearch.Domain;

using Excalibur.EventSourcing.Abstractions;

namespace CdcEventStoreElasticsearch.Projections;

// ============================================================================
// Order Search Projection Handlers
// ============================================================================
// Each event gets its own IProjectionEventHandler<T, TEvent> class.
// The framework manages load/upsert automatically.
// Cross-aggregate lookups (e.g., customer name) use DI constructor injection.

/// <summary>
/// Handles <see cref="OrderCreated"/> by initializing the order search projection
/// with customer name denormalization via DI-injected customer projection store.
/// </summary>
public sealed class OrderCreatedProjectionHandler
	: IProjectionEventHandler<OrderSearchProjection, OrderCreated>
{
	private readonly IProjectionStore<CustomerSearchProjection> _customerStore;
	private readonly ILogger<OrderCreatedProjectionHandler> _logger;

	public OrderCreatedProjectionHandler(
		IProjectionStore<CustomerSearchProjection> customerStore,
		ILogger<OrderCreatedProjectionHandler> logger)
	{
		_customerStore = customerStore;
		_logger = logger;
	}

	public async Task HandleAsync(
		OrderSearchProjection projection,
		OrderCreated @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		// Cross-aggregate lookup for customer name denormalization
		var customerName = "Unknown Customer";
		var customer = await _customerStore
			.GetByIdAsync(@event.CustomerId.ToString(), cancellationToken)
			.ConfigureAwait(false);
		if (customer is not null)
		{
			customerName = customer.Name;
		}

		projection.Id = @event.OrderId.ToString();
		projection.OrderId = @event.OrderId;
		projection.ExternalOrderId = @event.ExternalOrderId;
		projection.CustomerId = @event.CustomerId;
		projection.CustomerExternalId = @event.CustomerExternalId;
		projection.CustomerName = customerName;
		projection.Status = "Pending";
		projection.TotalAmount = 0;
		projection.ItemCount = 0;
		projection.OrderDate = @event.OrderDate;
		projection.CreatedAt = @event.OccurredAt;
		projection.Tags = ["new-order"];

		_logger.LogDebug("Created order search projection for {OrderId}", @event.OrderId);
	}
}

/// <summary>
/// Handles <see cref="OrderLineItemAdded"/> by adding a line item and recalculating totals.
/// </summary>
public sealed class OrderLineItemAddedProjectionHandler
	: IProjectionEventHandler<OrderSearchProjection, OrderLineItemAdded>
{
	public Task HandleAsync(
		OrderSearchProjection projection,
		OrderLineItemAdded @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.LineItems.Add(new OrderLineItemProjection
		{
			ItemId = @event.ItemId,
			ExternalItemId = @event.ExternalItemId,
			ProductName = @event.ProductName,
			Quantity = @event.Quantity,
			UnitPrice = @event.UnitPrice,
			LineTotal = @event.LineTotal
		});

		projection.ItemCount = projection.LineItems.Count;
		projection.TotalAmount = projection.LineItems.Sum(li => li.LineTotal);
		projection.LastUpdatedAt = @event.OccurredAt;

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles <see cref="OrderLineItemUpdated"/> by updating quantity and recalculating totals.
/// </summary>
public sealed class OrderLineItemUpdatedProjectionHandler
	: IProjectionEventHandler<OrderSearchProjection, OrderLineItemUpdated>
{
	public Task HandleAsync(
		OrderSearchProjection projection,
		OrderLineItemUpdated @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		var lineItem = projection.LineItems.FirstOrDefault(li => li.ItemId == @event.ItemId);
		if (lineItem is not null)
		{
			lineItem.Quantity = @event.NewQuantity;
			lineItem.LineTotal = lineItem.Quantity * lineItem.UnitPrice;
		}

		projection.TotalAmount = projection.LineItems.Sum(li => li.LineTotal);
		projection.LastUpdatedAt = @event.OccurredAt;

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles <see cref="OrderLineItemRemoved"/> by removing the item and recalculating totals.
/// </summary>
public sealed class OrderLineItemRemovedProjectionHandler
	: IProjectionEventHandler<OrderSearchProjection, OrderLineItemRemoved>
{
	public Task HandleAsync(
		OrderSearchProjection projection,
		OrderLineItemRemoved @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		var lineItem = projection.LineItems.FirstOrDefault(li => li.ItemId == @event.ItemId);
		if (lineItem is not null)
		{
			_ = projection.LineItems.Remove(lineItem);
		}

		projection.ItemCount = projection.LineItems.Count;
		projection.TotalAmount = projection.LineItems.Sum(li => li.LineTotal);
		projection.LastUpdatedAt = @event.OccurredAt;

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles <see cref="OrderStatusUpdated"/> by updating status and tags.
/// </summary>
public sealed class OrderStatusUpdatedProjectionHandler
	: IProjectionEventHandler<OrderSearchProjection, OrderStatusUpdated>
{
	public Task HandleAsync(
		OrderSearchProjection projection,
		OrderStatusUpdated @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Status = @event.NewStatus.ToString();
		projection.LastUpdatedAt = @event.OccurredAt;

		_ = projection.Tags.Remove("new-order");
		if (@event.NewStatus == OrderStatus.Confirmed && !projection.Tags.Contains("confirmed"))
		{
			projection.Tags.Add("confirmed");
		}

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles <see cref="OrderShipped"/> by updating status and shipment date.
/// </summary>
public sealed class OrderShippedProjectionHandler
	: IProjectionEventHandler<OrderSearchProjection, OrderShipped>
{
	public Task HandleAsync(
		OrderSearchProjection projection,
		OrderShipped @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Status = "Shipped";
		projection.ShippedDate = @event.ShippedDate;
		projection.LastUpdatedAt = @event.OccurredAt;

		if (!projection.Tags.Contains("shipped"))
		{
			projection.Tags.Add("shipped");
		}

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles <see cref="OrderDelivered"/> by updating status and delivery date.
/// </summary>
public sealed class OrderDeliveredProjectionHandler
	: IProjectionEventHandler<OrderSearchProjection, OrderDelivered>
{
	public Task HandleAsync(
		OrderSearchProjection projection,
		OrderDelivered @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Status = "Delivered";
		projection.DeliveredDate = @event.DeliveredDate;
		projection.LastUpdatedAt = @event.OccurredAt;

		if (!projection.Tags.Contains("delivered"))
		{
			projection.Tags.Add("delivered");
		}

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles <see cref="OrderCancelled"/> by updating status.
/// </summary>
public sealed class OrderCancelledProjectionHandler
	: IProjectionEventHandler<OrderSearchProjection, OrderCancelled>
{
	public Task HandleAsync(
		OrderSearchProjection projection,
		OrderCancelled @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Status = "Cancelled";
		projection.LastUpdatedAt = @event.OccurredAt;

		if (!projection.Tags.Contains("cancelled"))
		{
			projection.Tags.Add("cancelled");
		}

		return Task.CompletedTask;
	}
}
