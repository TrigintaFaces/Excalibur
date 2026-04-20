// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace ProjectionsSample.Projections;

// ============================================================================
// Category Summary Projection (Multi-Stream Projection)
// ============================================================================
// This projection aggregates data across multiple product streams to provide
// category-level summaries. It demonstrates how projections can combine data
// from multiple aggregates into a single read model.

/// <summary>
/// Read model for category-level statistics.
/// </summary>
/// <remarks>
/// This is a multi-stream projection that aggregates data from all products
/// in a category. It's useful for dashboard displays and category navigation.
/// </remarks>
public sealed class CategorySummaryProjection : IProjection<string>
{
	/// <summary>Gets or sets the category name (projection key).</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the category name.</summary>
	public string CategoryName { get; set; } = string.Empty;

	/// <summary>Gets or sets the total number of products in this category.</summary>
	public int TotalProducts { get; set; }

	/// <summary>Gets or sets the number of active products.</summary>
	public int ActiveProducts { get; set; }

	/// <summary>Gets or sets the number of products in stock.</summary>
	public int ProductsInStock { get; set; }

	/// <summary>Gets or sets the number of products with low stock.</summary>
	public int ProductsLowStock { get; set; }

	/// <summary>Gets or sets the total inventory value (sum of price * stock for all products).</summary>
	public decimal TotalInventoryValue { get; set; }

	/// <summary>Gets or sets the average product price in this category.</summary>
	public decimal AveragePrice { get; set; }

	/// <summary>Gets or sets the minimum product price in this category.</summary>
	public decimal MinPrice { get; set; } = decimal.MaxValue;

	/// <summary>Gets or sets the maximum product price in this category.</summary>
	public decimal MaxPrice { get; set; }

	/// <summary>Gets or sets when the projection was last updated.</summary>
	public DateTimeOffset LastModified { get; set; }

	/// <summary>Gets or sets the projection version.</summary>
	public long Version { get; set; }

	/// <summary>Gets or sets the product IDs in this category (for incremental updates).</summary>
	public HashSet<string> ProductIds { get; set; } = [];

	/// <summary>Gets or sets per-product price tracking (for average calculation).</summary>
	public Dictionary<string, decimal> ProductPrices { get; set; } = [];

	/// <summary>Gets or sets per-product stock tracking (for inventory value calculation).</summary>
	public Dictionary<string, int> ProductStocks { get; set; } = [];
}
