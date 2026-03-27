// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

namespace ProjectionsSample.Domain;

/// <summary>
/// Product aggregate demonstrating event sourcing with projections.
/// </summary>
/// <remarks>
/// This aggregate represents a product in a catalog with:
/// - Creation with initial stock
/// - Price changes
/// - Stock management (add/remove)
/// - Discontinuation
/// </remarks>
public class ProductAggregate : AggregateRoot<Guid>
{
	/// <summary>
	/// Initializes a new instance for rehydration from events.
	/// </summary>
	public ProductAggregate()
	{
	}

	/// <summary>
	/// Initializes a new instance with an identifier.
	/// </summary>
	public ProductAggregate(Guid id) : base(id)
	{
	}

	/// <summary>Gets the product name.</summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>Gets the product category.</summary>
	public string Category { get; private set; } = string.Empty;

	/// <summary>Gets the current price.</summary>
	public decimal Price { get; private set; }

	/// <summary>Gets the current stock level.</summary>
	public int StockLevel { get; private set; }

	/// <summary>Gets whether the product is active.</summary>
	public bool IsActive { get; private set; }

	/// <summary>Gets when the product was created.</summary>
	public DateTimeOffset? CreatedAt { get; private set; }

	/// <summary>Gets when the product was discontinued (if applicable).</summary>
	public DateTimeOffset? DiscontinuedAt { get; private set; }

	/// <summary>
	/// Creates a new product.
	/// </summary>
	public static ProductAggregate Create(Guid id, string name, string category, decimal price, int initialStock)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(category);
		ArgumentOutOfRangeException.ThrowIfNegative(price);
		ArgumentOutOfRangeException.ThrowIfNegative(initialStock);

		var product = new ProductAggregate(id);
		product.RaiseEvent(new ProductCreated(id, name, category, price, initialStock));
		return product;
	}

	/// <summary>
	/// Changes the product price.
	/// </summary>
	public void ChangePrice(decimal newPrice)
	{
		EnsureActive();
		ArgumentOutOfRangeException.ThrowIfNegative(newPrice);

		if (newPrice == Price)
		{
			return;
		}

		RaiseEvent(new ProductPriceChanged(Id, Price, newPrice));
	}

	/// <summary>
	/// Adds stock to the product.
	/// </summary>
	public void AddStock(int quantity)
	{
		EnsureActive();
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

		RaiseEvent(new ProductStockAdded(Id, quantity, StockLevel + quantity));
	}

	/// <summary>
	/// Removes stock from the product.
	/// </summary>
	public void RemoveStock(int quantity, string reason = "Sale")
	{
		EnsureActive();
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

		if (quantity > StockLevel)
		{
			throw new InvalidOperationException(
				$"Cannot remove {quantity} units. Only {StockLevel} in stock.");
		}

		RaiseEvent(new ProductStockRemoved(Id, quantity, StockLevel - quantity, reason));
	}

	/// <summary>
	/// Discontinues the product.
	/// </summary>
	public void Discontinue(string reason)
	{
		EnsureActive();
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		RaiseEvent(new ProductDiscontinued(Id, reason));
	}

	/// <inheritdoc/>
	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case ProductCreated e: Apply(e); break;
			case ProductPriceChanged e: Apply(e); break;
			case ProductStockAdded e: Apply(e); break;
			case ProductStockRemoved e: Apply(e); break;
			case ProductDiscontinued e: Apply(e); break;
		}
	}

	private void EnsureActive()
	{
		if (!IsActive)
		{
			throw new InvalidOperationException("Product has been discontinued");
		}
	}

	private void Apply(ProductCreated e)
	{
		Id = e.ProductId;
		Name = e.Name;
		Category = e.Category;
		Price = e.Price;
		StockLevel = e.InitialStock;
		IsActive = true;
		CreatedAt = e.OccurredAt;
	}

	private void Apply(ProductPriceChanged e)
	{
		Price = e.NewPrice;
	}

	private void Apply(ProductStockAdded e)
	{
		StockLevel = e.NewStockLevel;
	}

	private void Apply(ProductStockRemoved e)
	{
		StockLevel = e.NewStockLevel;
	}

	private void Apply(ProductDiscontinued e)
	{
		IsActive = false;
		DiscontinuedAt = e.OccurredAt;
	}
}
