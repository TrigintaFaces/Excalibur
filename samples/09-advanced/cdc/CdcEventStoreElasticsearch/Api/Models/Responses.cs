// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// API Response DTOs — Controller output contracts
// ============================================================================
//
// These types define what the API returns to consumers. They are decoupled
// from the internal projection models (which may contain persistence metadata,
// Elasticsearch-specific fields, or internal IDs) and from the Elasticsearch
// SearchResponse<T> type.
//
// The controller maps Handler results → Response DTOs → HTTP response.
//
// Paging uses framework types from Excalibur.EventSourcing.Abstractions:
//   PagedResult<T>   — Offset-based paging (page number + page size)
//   CursorPagedResult<T> — Cursor-based paging (opaque continuation token)
// ============================================================================

namespace CdcEventStoreElasticsearch.Api.Models;

/// <summary>
/// API representation of an order.
/// </summary>
public sealed class OrderDto
{
	/// <summary>Gets or sets the order ID (aggregate ID).</summary>
	public Guid OrderId { get; set; }

	/// <summary>Gets or sets the external order ID from the legacy system.</summary>
	public string ExternalOrderId { get; set; } = string.Empty;

	/// <summary>Gets or sets the customer ID.</summary>
	public Guid CustomerId { get; set; }

	/// <summary>Gets or sets the customer external ID.</summary>
	public string CustomerExternalId { get; set; } = string.Empty;

	/// <summary>Gets or sets the denormalized customer name.</summary>
	public string CustomerName { get; set; } = string.Empty;

	/// <summary>Gets or sets the order status.</summary>
	public string Status { get; set; } = "Pending";

	/// <summary>Gets or sets the total order amount.</summary>
	public decimal TotalAmount { get; set; }

	/// <summary>Gets or sets the number of line items.</summary>
	public int ItemCount { get; set; }

	/// <summary>Gets or sets the order date.</summary>
	public DateTime OrderDate { get; set; }

	/// <summary>Gets or sets the shipped date.</summary>
	public DateTime? ShippedDate { get; set; }

	/// <summary>Gets or sets the delivered date.</summary>
	public DateTime? DeliveredDate { get; set; }

	/// <summary>Gets or sets when the order was created.</summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>Gets or sets when the order was last updated.</summary>
	public DateTimeOffset? LastUpdatedAt { get; set; }

	/// <summary>Gets or sets the order line items.</summary>
	public List<OrderLineItemDto> LineItems { get; set; } = [];

	/// <summary>Gets or sets searchable tags.</summary>
	public List<string> Tags { get; set; } = [];
}

/// <summary>
/// API representation of an order line item.
/// </summary>
public sealed class OrderLineItemDto
{
	/// <summary>Gets or sets the line item ID.</summary>
	public Guid ItemId { get; set; }

	/// <summary>Gets or sets the external item ID from the legacy system.</summary>
	public string ExternalItemId { get; set; } = string.Empty;

	/// <summary>Gets or sets the product name.</summary>
	public string ProductName { get; set; } = string.Empty;

	/// <summary>Gets or sets the quantity ordered.</summary>
	public int Quantity { get; set; }

	/// <summary>Gets or sets the unit price.</summary>
	public decimal UnitPrice { get; set; }

	/// <summary>Gets or sets the line total.</summary>
	public decimal LineTotal { get; set; }
}

/// <summary>
/// API representation of aggregated order statistics.
/// </summary>
public sealed class OrderStatisticsDto
{
	/// <summary>Gets or sets the total revenue across all orders.</summary>
	public double TotalRevenue { get; set; }

	/// <summary>Gets or sets the average order value.</summary>
	public double AverageOrderValue { get; set; }

	/// <summary>Gets or sets order counts grouped by status.</summary>
	public List<StatusCountDto> ByStatus { get; set; } = [];

	/// <summary>Gets or sets monthly order trends.</summary>
	public List<MonthlyTrendDto> MonthlyTrend { get; set; } = [];
}

/// <summary>
/// Order count for a specific status.
/// </summary>
public sealed class StatusCountDto
{
	/// <summary>Gets or sets the status name.</summary>
	public string? Status { get; set; }

	/// <summary>Gets or sets the number of orders with this status.</summary>
	public long Count { get; set; }
}

/// <summary>
/// Order count for a specific month.
/// </summary>
public sealed class MonthlyTrendDto
{
	/// <summary>Gets or sets the month label.</summary>
	public string? Month { get; set; }

	/// <summary>Gets or sets the number of orders in this month.</summary>
	public long Count { get; set; }
}
