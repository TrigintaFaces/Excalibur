// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace CdcEventStoreElasticsearch.Projections;

/// <summary>
/// Elasticsearch projection for order search and display.
/// This read model is optimized for full-text search and filtering.
/// </summary>
public sealed class OrderSearchProjection
{
	/// <summary>Gets or sets the unique identifier.</summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the order ID (aggregate ID).</summary>
	[JsonPropertyName("orderId")]
	public Guid OrderId { get; set; }

	/// <summary>Gets or sets the external order ID from the legacy system.</summary>
	[JsonPropertyName("externalOrderId")]
	public string ExternalOrderId { get; set; } = string.Empty;

	/// <summary>Gets or sets the customer ID.</summary>
	[JsonPropertyName("customerId")]
	public Guid CustomerId { get; set; }

	/// <summary>Gets or sets the customer external ID.</summary>
	[JsonPropertyName("customerExternalId")]
	public string CustomerExternalId { get; set; } = string.Empty;

	/// <summary>Gets or sets the denormalized customer name for display.</summary>
	[JsonPropertyName("customerName")]
	public string CustomerName { get; set; } = string.Empty;

	/// <summary>Gets or sets the order status.</summary>
	[JsonPropertyName("status")]
	public string Status { get; set; } = "Pending";

	/// <summary>Gets or sets the total order amount.</summary>
	[JsonPropertyName("totalAmount")]
	public decimal TotalAmount { get; set; }

	/// <summary>Gets or sets the number of line items.</summary>
	[JsonPropertyName("itemCount")]
	public int ItemCount { get; set; }

	/// <summary>Gets or sets the order date.</summary>
	[JsonPropertyName("orderDate")]
	public DateTime OrderDate { get; set; }

	/// <summary>Gets or sets the shipped date.</summary>
	[JsonPropertyName("shippedDate")]
	public DateTime? ShippedDate { get; set; }

	/// <summary>Gets or sets the delivered date.</summary>
	[JsonPropertyName("deliveredDate")]
	public DateTime? DeliveredDate { get; set; }

	/// <summary>Gets or sets when the order was created.</summary>
	[JsonPropertyName("createdAt")]
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>Gets or sets when the order was last updated.</summary>
	[JsonPropertyName("lastUpdatedAt")]
	public DateTimeOffset? LastUpdatedAt { get; set; }

	/// <summary>Gets or sets the order line items for display.</summary>
	[JsonPropertyName("lineItems")]
	public List<OrderLineItemProjection> LineItems { get; set; } = [];

	/// <summary>Gets or sets searchable tags for filtering.</summary>
	[JsonPropertyName("tags")]
	public List<string> Tags { get; set; } = [];
}

/// <summary>
/// Nested projection for order line items.
/// </summary>
public sealed class OrderLineItemProjection
{
	/// <summary>Gets or sets the line item ID.</summary>
	[JsonPropertyName("itemId")]
	public Guid ItemId { get; set; }

	/// <summary>Gets or sets the external item ID from the legacy system.</summary>
	[JsonPropertyName("externalItemId")]
	public string ExternalItemId { get; set; } = string.Empty;

	/// <summary>Gets or sets the product name.</summary>
	[JsonPropertyName("productName")]
	public string ProductName { get; set; } = string.Empty;

	/// <summary>Gets or sets the quantity ordered.</summary>
	[JsonPropertyName("quantity")]
	public int Quantity { get; set; }

	/// <summary>Gets or sets the unit price.</summary>
	[JsonPropertyName("unitPrice")]
	public decimal UnitPrice { get; set; }

	/// <summary>Gets or sets the line total.</summary>
	[JsonPropertyName("lineTotal")]
	public decimal LineTotal { get; set; }
}

/// <summary>
/// Elasticsearch projection for global order analytics.
/// This is a singleton document that aggregates metrics across all orders.
/// </summary>
public sealed class OrderAnalyticsProjection
{
	/// <summary>Gets or sets the unique identifier (always "global").</summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = "global";

	/// <summary>Gets or sets the total number of orders.</summary>
	[JsonPropertyName("totalOrders")]
	public int TotalOrders { get; set; }

	/// <summary>Gets or sets the total revenue across all orders.</summary>
	[JsonPropertyName("totalRevenue")]
	public decimal TotalRevenue { get; set; }

	/// <summary>Gets or sets the average order value.</summary>
	[JsonPropertyName("averageOrderValue")]
	public decimal AverageOrderValue { get; set; }

	/// <summary>Gets or sets the count of orders by status.</summary>
	[JsonPropertyName("ordersByStatus")]
	public Dictionary<string, int> OrdersByStatus { get; set; } = [];

	/// <summary>Gets or sets the top selling products.</summary>
	[JsonPropertyName("topProducts")]
	public List<TopProductProjection> TopProducts { get; set; } = [];

	/// <summary>Gets or sets revenue by month (key: YYYY-MM).</summary>
	[JsonPropertyName("revenueByMonth")]
	public Dictionary<string, decimal> RevenueByMonth { get; set; } = [];

	/// <summary>Gets or sets when this analytics was last updated.</summary>
	[JsonPropertyName("lastUpdatedAt")]
	public DateTimeOffset LastUpdatedAt { get; set; }
}

/// <summary>
/// Projection for top selling products.
/// </summary>
public sealed class TopProductProjection
{
	/// <summary>Gets or sets the product name.</summary>
	[JsonPropertyName("productName")]
	public string ProductName { get; set; } = string.Empty;

	/// <summary>Gets or sets the total quantity sold.</summary>
	[JsonPropertyName("totalQuantity")]
	public int TotalQuantity { get; set; }

	/// <summary>Gets or sets the total revenue from this product.</summary>
	[JsonPropertyName("totalRevenue")]
	public decimal TotalRevenue { get; set; }

	/// <summary>Gets or sets the number of orders containing this product.</summary>
	[JsonPropertyName("orderCount")]
	public int OrderCount { get; set; }
}

/// <summary>
/// Elasticsearch projection for daily order summaries.
/// One document per day for time-series analytics.
/// </summary>
public sealed class DailyOrderSummaryProjection
{
	/// <summary>Gets or sets the unique identifier (date string: YYYY-MM-DD).</summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the date.</summary>
	[JsonPropertyName("date")]
	public DateTime Date { get; set; }

	/// <summary>Gets or sets the number of orders on this day.</summary>
	[JsonPropertyName("orderCount")]
	public int OrderCount { get; set; }

	/// <summary>Gets or sets the total revenue for this day.</summary>
	[JsonPropertyName("totalRevenue")]
	public decimal TotalRevenue { get; set; }

	/// <summary>Gets or sets the average order value for this day.</summary>
	[JsonPropertyName("averageOrderValue")]
	public decimal AverageOrderValue { get; set; }

	/// <summary>Gets or sets orders from new customers (first-time buyers).</summary>
	[JsonPropertyName("newCustomerOrders")]
	public int NewCustomerOrders { get; set; }

	/// <summary>Gets or sets orders from returning customers.</summary>
	[JsonPropertyName("returningCustomerOrders")]
	public int ReturningCustomerOrders { get; set; }

	/// <summary>Gets or sets the count of orders by hour (0-23).</summary>
	[JsonPropertyName("ordersByHour")]
	public Dictionary<int, int> OrdersByHour { get; set; } = [];

	/// <summary>Gets or sets the count of orders by status.</summary>
	[JsonPropertyName("ordersByStatus")]
	public Dictionary<string, int> OrdersByStatus { get; set; } = [];

	/// <summary>Gets or sets when this summary was last updated.</summary>
	[JsonPropertyName("lastUpdatedAt")]
	public DateTimeOffset LastUpdatedAt { get; set; }
}
