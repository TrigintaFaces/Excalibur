// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcEventStoreElasticsearch.Domain;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace CdcEventStoreElasticsearch.Projections;

/// <summary>
/// Handles domain events to update the order search projection in Elasticsearch.
/// </summary>
public sealed class OrderSearchProjectionHandler
{
	private readonly IProjectionStore<OrderSearchProjection> _projectionStore;
	private readonly IProjectionStore<CustomerSearchProjection> _customerProjectionStore;
	private readonly ILogger<OrderSearchProjectionHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderSearchProjectionHandler"/> class.
	/// </summary>
	public OrderSearchProjectionHandler(
		IProjectionStore<OrderSearchProjection> projectionStore,
		IProjectionStore<CustomerSearchProjection> customerProjectionStore,
		ILogger<OrderSearchProjectionHandler> logger)
	{
		_projectionStore = projectionStore;
		_customerProjectionStore = customerProjectionStore;
		_logger = logger;
	}

	/// <summary>
	/// Handles any domain event by routing to the appropriate handler method.
	/// </summary>
	public Task HandleEventAsync(IDomainEvent @event, CancellationToken cancellationToken) => @event switch
	{
		OrderCreated e => HandleAsync(e, cancellationToken),
		OrderLineItemAdded e => HandleAsync(e, cancellationToken),
		OrderLineItemUpdated e => HandleAsync(e, cancellationToken),
		OrderLineItemRemoved e => HandleAsync(e, cancellationToken),
		OrderStatusUpdated e => HandleAsync(e, cancellationToken),
		OrderShipped e => HandleAsync(e, cancellationToken),
		OrderDelivered e => HandleAsync(e, cancellationToken),
		OrderCancelled e => HandleAsync(e, cancellationToken),
		_ => Task.CompletedTask
	};

	private async Task HandleAsync(OrderCreated e, CancellationToken cancellationToken)
	{
		// Try to get customer name for denormalization
		var customerName = "Unknown Customer";
		var customerProjection = await _customerProjectionStore
			.GetByIdAsync(e.CustomerId.ToString(), cancellationToken)
			.ConfigureAwait(false);
		if (customerProjection is not null)
		{
			customerName = customerProjection.Name;
		}

		var projection = new OrderSearchProjection
		{
			Id = e.OrderId.ToString(),
			OrderId = e.OrderId,
			ExternalOrderId = e.ExternalOrderId,
			CustomerId = e.CustomerId,
			CustomerExternalId = e.CustomerExternalId,
			CustomerName = customerName,
			Status = "Pending",
			TotalAmount = 0,
			ItemCount = 0,
			OrderDate = e.OrderDate,
			CreatedAt = e.OccurredAt,
			Tags = ["new-order"]
		};

		await _projectionStore.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Created order search projection for {OrderId}",
			e.OrderId);
	}

	private async Task HandleAsync(OrderLineItemAdded e, CancellationToken cancellationToken)
	{
		var id = e.OrderId.ToString();
		var existing = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (existing is null)
		{
			_logger.LogWarning(
				"Order search projection not found for {OrderId} during line item add",
				e.OrderId);
			return;
		}

		existing.LineItems.Add(new OrderLineItemProjection
		{
			ItemId = e.ItemId,
			ExternalItemId = e.ExternalItemId,
			ProductName = e.ProductName,
			Quantity = e.Quantity,
			UnitPrice = e.UnitPrice,
			LineTotal = e.LineTotal
		});

		existing.ItemCount = existing.LineItems.Count;
		existing.TotalAmount = existing.LineItems.Sum(li => li.LineTotal);
		existing.LastUpdatedAt = e.OccurredAt;

		await _projectionStore.UpsertAsync(id, existing, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Added line item to order search projection for {OrderId}",
			e.OrderId);
	}

	private async Task HandleAsync(OrderLineItemUpdated e, CancellationToken cancellationToken)
	{
		var id = e.OrderId.ToString();
		var existing = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (existing is null)
		{
			_logger.LogWarning(
				"Order search projection not found for {OrderId} during line item update",
				e.OrderId);
			return;
		}

		var lineItem = existing.LineItems.FirstOrDefault(li => li.ItemId == e.ItemId);
		if (lineItem is not null)
		{
			lineItem.Quantity = e.NewQuantity;
			lineItem.LineTotal = lineItem.Quantity * lineItem.UnitPrice;
		}

		existing.TotalAmount = existing.LineItems.Sum(li => li.LineTotal);
		existing.LastUpdatedAt = e.OccurredAt;

		await _projectionStore.UpsertAsync(id, existing, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Updated line item in order search projection for {OrderId}",
			e.OrderId);
	}

	private async Task HandleAsync(OrderLineItemRemoved e, CancellationToken cancellationToken)
	{
		var id = e.OrderId.ToString();
		var existing = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (existing is null)
		{
			_logger.LogWarning(
				"Order search projection not found for {OrderId} during line item remove",
				e.OrderId);
			return;
		}

		var lineItem = existing.LineItems.FirstOrDefault(li => li.ItemId == e.ItemId);
		if (lineItem is not null)
		{
			_ = existing.LineItems.Remove(lineItem);
		}

		existing.ItemCount = existing.LineItems.Count;
		existing.TotalAmount = existing.LineItems.Sum(li => li.LineTotal);
		existing.LastUpdatedAt = e.OccurredAt;

		await _projectionStore.UpsertAsync(id, existing, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Removed line item from order search projection for {OrderId}",
			e.OrderId);
	}

	private async Task HandleAsync(OrderStatusUpdated e, CancellationToken cancellationToken)
	{
		var id = e.OrderId.ToString();
		var existing = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (existing is null)
		{
			_logger.LogWarning(
				"Order search projection not found for {OrderId} during status update",
				e.OrderId);
			return;
		}

		existing.Status = e.NewStatus.ToString();
		existing.LastUpdatedAt = e.OccurredAt;

		// Update tags based on status
		_ = existing.Tags.Remove("new-order");
		if (e.NewStatus == OrderStatus.Confirmed && !existing.Tags.Contains("confirmed"))
		{
			existing.Tags.Add("confirmed");
		}

		await _projectionStore.UpsertAsync(id, existing, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Updated status in order search projection for {OrderId}",
			e.OrderId);
	}

	private async Task HandleAsync(OrderShipped e, CancellationToken cancellationToken)
	{
		var id = e.OrderId.ToString();
		var existing = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (existing is null)
		{
			_logger.LogWarning(
				"Order search projection not found for {OrderId} during shipment",
				e.OrderId);
			return;
		}

		existing.Status = "Shipped";
		existing.ShippedDate = e.ShippedDate;
		existing.LastUpdatedAt = e.OccurredAt;

		if (!existing.Tags.Contains("shipped"))
		{
			existing.Tags.Add("shipped");
		}

		await _projectionStore.UpsertAsync(id, existing, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Updated shipment in order search projection for {OrderId}",
			e.OrderId);
	}

	private async Task HandleAsync(OrderDelivered e, CancellationToken cancellationToken)
	{
		var id = e.OrderId.ToString();
		var existing = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (existing is null)
		{
			_logger.LogWarning(
				"Order search projection not found for {OrderId} during delivery",
				e.OrderId);
			return;
		}

		existing.Status = "Delivered";
		existing.DeliveredDate = e.DeliveredDate;
		existing.LastUpdatedAt = e.OccurredAt;

		if (!existing.Tags.Contains("delivered"))
		{
			existing.Tags.Add("delivered");
		}

		await _projectionStore.UpsertAsync(id, existing, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Updated delivery in order search projection for {OrderId}",
			e.OrderId);
	}

	private async Task HandleAsync(OrderCancelled e, CancellationToken cancellationToken)
	{
		var id = e.OrderId.ToString();
		var existing = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (existing is null)
		{
			_logger.LogWarning(
				"Order search projection not found for {OrderId} during cancellation",
				e.OrderId);
			return;
		}

		existing.Status = "Cancelled";
		existing.LastUpdatedAt = e.OccurredAt;

		if (!existing.Tags.Contains("cancelled"))
		{
			existing.Tags.Add("cancelled");
		}

		await _projectionStore.UpsertAsync(id, existing, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Updated cancellation in order search projection for {OrderId}",
			e.OrderId);
	}
}

/// <summary>
/// Handles domain events to update order analytics projections.
/// Updates both the global analytics and daily summary projections.
/// </summary>
public sealed class OrderAnalyticsProjectionHandler
{
	private const string GlobalAnalyticsId = "global";
	private readonly IProjectionStore<OrderAnalyticsProjection> _analyticsStore;
	private readonly IProjectionStore<DailyOrderSummaryProjection> _dailySummaryStore;
	private readonly ILogger<OrderAnalyticsProjectionHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderAnalyticsProjectionHandler"/> class.
	/// </summary>
	public OrderAnalyticsProjectionHandler(
		IProjectionStore<OrderAnalyticsProjection> analyticsStore,
		IProjectionStore<DailyOrderSummaryProjection> dailySummaryStore,
		ILogger<OrderAnalyticsProjectionHandler> logger)
	{
		_analyticsStore = analyticsStore;
		_dailySummaryStore = dailySummaryStore;
		_logger = logger;
	}

	/// <summary>
	/// Handles order created event to update analytics.
	/// </summary>
	public async Task HandleAsync(OrderCreated e, bool isNewCustomer, CancellationToken cancellationToken)
	{
		// Update global analytics
		await UpdateGlobalAnalyticsAsync(analytics =>
		{
			analytics.TotalOrders++;
			analytics.OrdersByStatus.TryGetValue("Pending", out var pending);
			analytics.OrdersByStatus["Pending"] = pending + 1;
		}, cancellationToken).ConfigureAwait(false);

		// Update daily summary
		await UpdateDailySummaryAsync(e.OrderDate, summary =>
		{
			summary.OrderCount++;

			var hour = e.OrderDate.Hour;
			summary.OrdersByHour.TryGetValue(hour, out var hourCount);
			summary.OrdersByHour[hour] = hourCount + 1;

			summary.OrdersByStatus.TryGetValue("Pending", out var pending);
			summary.OrdersByStatus["Pending"] = pending + 1;

			if (isNewCustomer)
			{
				summary.NewCustomerOrders++;
			}
			else
			{
				summary.ReturningCustomerOrders++;
			}
		}, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Updated analytics for new order {OrderId}", e.OrderId);
	}

	/// <summary>
	/// Handles line item added to update revenue analytics.
	/// </summary>
	public async Task HandleAsync(OrderLineItemAdded e, DateTime orderDate, CancellationToken cancellationToken)
	{
		var revenue = e.LineTotal;
		var monthKey = orderDate.ToString("yyyy-MM");

		// Update global analytics
		await UpdateGlobalAnalyticsAsync(analytics =>
		{
			analytics.TotalRevenue += revenue;
			analytics.AverageOrderValue = analytics.TotalOrders > 0
				? analytics.TotalRevenue / analytics.TotalOrders
				: 0;

			analytics.RevenueByMonth.TryGetValue(monthKey, out var monthRevenue);
			analytics.RevenueByMonth[monthKey] = monthRevenue + revenue;

			// Update top products
			var existingProduct = analytics.TopProducts.FirstOrDefault(p => p.ProductName == e.ProductName);
			if (existingProduct is not null)
			{
				existingProduct.TotalQuantity += e.Quantity;
				existingProduct.TotalRevenue += revenue;
				existingProduct.OrderCount++;
			}
			else
			{
				analytics.TopProducts.Add(new TopProductProjection
				{
					ProductName = e.ProductName,
					TotalQuantity = e.Quantity,
					TotalRevenue = revenue,
					OrderCount = 1
				});
			}

			// Keep only top 10 products
			analytics.TopProducts =
			[
				.. analytics.TopProducts
					.OrderByDescending(p => p.TotalRevenue)
					.Take(10)
			];
		}, cancellationToken).ConfigureAwait(false);

		// Update daily summary
		await UpdateDailySummaryAsync(orderDate, summary =>
		{
			summary.TotalRevenue += revenue;
			summary.AverageOrderValue = summary.OrderCount > 0
				? summary.TotalRevenue / summary.OrderCount
				: 0;
		}, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Updated analytics for line item on order {OrderId}", e.OrderId);
	}

	/// <summary>
	/// Handles status change to update status counts.
	/// </summary>
	public async Task HandleAsync(OrderStatusUpdated e, DateTime orderDate, CancellationToken cancellationToken)
	{
		var oldStatus = e.OldStatus.ToString();
		var newStatus = e.NewStatus.ToString();

		// Update global analytics
		await UpdateGlobalAnalyticsAsync(analytics =>
		{
			// Decrement old status
			if (analytics.OrdersByStatus.TryGetValue(oldStatus, out var oldCount) && oldCount > 0)
			{
				analytics.OrdersByStatus[oldStatus] = oldCount - 1;
			}

			// Increment new status
			analytics.OrdersByStatus.TryGetValue(newStatus, out var newCount);
			analytics.OrdersByStatus[newStatus] = newCount + 1;
		}, cancellationToken).ConfigureAwait(false);

		// Update daily summary
		await UpdateDailySummaryAsync(orderDate, summary =>
		{
			// Decrement old status
			if (summary.OrdersByStatus.TryGetValue(oldStatus, out var oldCount) && oldCount > 0)
			{
				summary.OrdersByStatus[oldStatus] = oldCount - 1;
			}

			// Increment new status
			summary.OrdersByStatus.TryGetValue(newStatus, out var newCount);
			summary.OrdersByStatus[newStatus] = newCount + 1;
		}, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Updated analytics for status change on order {OrderId}", e.OrderId);
	}

	private async Task UpdateGlobalAnalyticsAsync(
		Action<OrderAnalyticsProjection> update,
		CancellationToken cancellationToken)
	{
		var analytics = await _analyticsStore
			.GetByIdAsync(GlobalAnalyticsId, cancellationToken)
			.ConfigureAwait(false);

		if (analytics is null)
		{
			analytics = new OrderAnalyticsProjection { Id = GlobalAnalyticsId };
		}

		update(analytics);
		analytics.LastUpdatedAt = DateTimeOffset.UtcNow;

		await _analyticsStore.UpsertAsync(GlobalAnalyticsId, analytics, cancellationToken).ConfigureAwait(false);
	}

	private async Task UpdateDailySummaryAsync(
		DateTime date,
		Action<DailyOrderSummaryProjection> update,
		CancellationToken cancellationToken)
	{
		var id = date.ToString("yyyy-MM-dd");
		var summary = await _dailySummaryStore
			.GetByIdAsync(id, cancellationToken)
			.ConfigureAwait(false);

		if (summary is null)
		{
			summary = new DailyOrderSummaryProjection { Id = id, Date = date.Date };
		}

		update(summary);
		summary.LastUpdatedAt = DateTimeOffset.UtcNow;

		await _dailySummaryStore.UpsertAsync(id, summary, cancellationToken).ConfigureAwait(false);
	}
}
