// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace ProjectionsSample.Domain;

// ============================================================================
// Domain Events for Product Catalog
// ============================================================================
// These events represent state changes in our domain that projections will
// listen to and use to build read models.

/// <summary>
/// Event raised when a new product is created.
/// </summary>
public sealed record ProductCreated : DomainEvent
{
	public ProductCreated(Guid productId, string name, string category, decimal price, int stock, long version)
		: base(productId.ToString(), version)
	{
		ProductId = productId;
		Name = name;
		Category = category;
		Price = price;
		InitialStock = stock;
	}

	/// <summary>Gets the product identifier.</summary>
	public Guid ProductId { get; init; }

	/// <summary>Gets the product name.</summary>
	public string Name { get; init; }

	/// <summary>Gets the product category.</summary>
	public string Category { get; init; }

	/// <summary>Gets the initial price.</summary>
	public decimal Price { get; init; }

	/// <summary>Gets the initial stock level.</summary>
	public int InitialStock { get; init; }
}

/// <summary>
/// Event raised when a product's price changes.
/// </summary>
public sealed record ProductPriceChanged : DomainEvent
{
	public ProductPriceChanged(Guid productId, decimal oldPrice, decimal newPrice, long version)
		: base(productId.ToString(), version)
	{
		ProductId = productId;
		OldPrice = oldPrice;
		NewPrice = newPrice;
	}

	/// <summary>Gets the product identifier.</summary>
	public Guid ProductId { get; init; }

	/// <summary>Gets the previous price.</summary>
	public decimal OldPrice { get; init; }

	/// <summary>Gets the new price.</summary>
	public decimal NewPrice { get; init; }
}

/// <summary>
/// Event raised when stock is added to a product.
/// </summary>
public sealed record ProductStockAdded : DomainEvent
{
	public ProductStockAdded(Guid productId, int quantity, int newStockLevel, long version)
		: base(productId.ToString(), version)
	{
		ProductId = productId;
		Quantity = quantity;
		NewStockLevel = newStockLevel;
	}

	/// <summary>Gets the product identifier.</summary>
	public Guid ProductId { get; init; }

	/// <summary>Gets the quantity added.</summary>
	public int Quantity { get; init; }

	/// <summary>Gets the new stock level.</summary>
	public int NewStockLevel { get; init; }
}

/// <summary>
/// Event raised when stock is removed from a product (e.g., sale).
/// </summary>
public sealed record ProductStockRemoved : DomainEvent
{
	public ProductStockRemoved(Guid productId, int quantity, int newStockLevel, string reason, long version)
		: base(productId.ToString(), version)
	{
		ProductId = productId;
		Quantity = quantity;
		NewStockLevel = newStockLevel;
		Reason = reason;
	}

	/// <summary>Gets the product identifier.</summary>
	public Guid ProductId { get; init; }

	/// <summary>Gets the quantity removed.</summary>
	public int Quantity { get; init; }

	/// <summary>Gets the new stock level.</summary>
	public int NewStockLevel { get; init; }

	/// <summary>Gets the reason for removal.</summary>
	public string Reason { get; init; }
}

/// <summary>
/// Event raised when a product is discontinued.
/// </summary>
public sealed record ProductDiscontinued : DomainEvent
{
	public ProductDiscontinued(Guid productId, string reason, long version)
		: base(productId.ToString(), version)
	{
		ProductId = productId;
		Reason = reason;
	}

	/// <summary>Gets the product identifier.</summary>
	public Guid ProductId { get; init; }

	/// <summary>Gets the discontinuation reason.</summary>
	public string Reason { get; init; }
}
