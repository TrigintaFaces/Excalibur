// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace ProjectionsSample.Projections;

// ============================================================================
// Product Catalog Projection (Read Model)
// ============================================================================
// This projection maintains a denormalized view of products for fast catalog
// queries. It's optimized for read operations like listing, filtering, and
// searching products.

/// <summary>
/// Read model for product catalog queries.
/// </summary>
/// <remarks>
/// This projection is built from ProductCreated, ProductPriceChanged,
/// ProductStockAdded, ProductStockRemoved, and ProductDiscontinued events.
/// It's optimized for catalog listing and search scenarios.
/// </remarks>
public sealed class ProductCatalogProjection : IProjection<string>
{
	/// <summary>Gets or sets the product identifier (projection key).</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the product name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Gets or sets the product category.</summary>
	public string Category { get; set; } = string.Empty;

	/// <summary>Gets or sets the current price.</summary>
	public decimal CurrentPrice { get; set; }

	/// <summary>Gets or sets the original price (for "was $X" displays).</summary>
	public decimal OriginalPrice { get; set; }

	/// <summary>Gets or sets whether the price has been reduced.</summary>
	public bool IsOnSale => CurrentPrice < OriginalPrice;

	/// <summary>Gets or sets the discount percentage if on sale.</summary>
	public decimal DiscountPercentage => OriginalPrice > 0 && IsOnSale
		? Math.Round((1 - CurrentPrice / OriginalPrice) * 100, 2)
		: 0;

	/// <summary>Gets or sets the current stock level.</summary>
	public int StockLevel { get; set; }

	/// <summary>Gets or sets whether the product is in stock.</summary>
	public bool InStock => StockLevel > 0 && IsActive;

	/// <summary>Gets or sets whether the product is low on stock.</summary>
	public bool LowStock => StockLevel is > 0 and <= 10 && IsActive;

	/// <summary>Gets or sets whether the product is active.</summary>
	public bool IsActive { get; set; } = true;

	/// <summary>Gets or sets when the product was created.</summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>Gets or sets when the projection was last updated.</summary>
	public DateTimeOffset LastModified { get; set; }

	/// <summary>Gets or sets the projection version.</summary>
	public long Version { get; set; }
}
