// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

using ProjectionsSample.Domain;

namespace ProjectionsSample.Projections;

// ============================================================================
// Category Summary Projection Handler (Multi-Stream Projection)
// ============================================================================
// This handler demonstrates a multi-stream projection that aggregates data
// from multiple product streams into category-level summaries. It tracks
// individual product data internally to enable accurate recalculations.

/// <summary>
/// Handles events for the CategorySummaryProjection.
/// </summary>
/// <remarks>
/// This is a multi-stream projection that aggregates data from all products
/// within a category. It maintains internal state to accurately calculate
/// statistics like average price and total inventory value.
/// </remarks>
public sealed class CategorySummaryProjectionHandler
{
	private readonly IProjectionStore<CategorySummaryProjection> _store;

	public CategorySummaryProjectionHandler(IProjectionStore<CategorySummaryProjection> store)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
	}

	/// <summary>
	/// Handles a ProductCreated event.
	/// </summary>
	public async Task HandleAsync(ProductCreated @event, CancellationToken cancellationToken)
	{
		var categoryId = @event.Category.ToUpperInvariant().Replace(" ", "-", StringComparison.Ordinal);
		var projection = await _store.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false)
						 ?? new CategorySummaryProjection { Id = categoryId, CategoryName = @event.Category };

		// Add product to tracking
		var productId = @event.ProductId.ToString();
		_ = projection.ProductIds.Add(productId);
		projection.ProductPrices[productId] = @event.Price;
		projection.ProductStocks[productId] = @event.InitialStock;

		// Update statistics
		projection.TotalProducts++;
		projection.ActiveProducts++;

		if (@event.InitialStock > 0)
		{
			projection.ProductsInStock++;
			if (@event.InitialStock <= 10)
			{
				projection.ProductsLowStock++;
			}
		}

		RecalculatePriceStatistics(projection);
		RecalculateInventoryValue(projection);

		projection.LastModified = DateTimeOffset.UtcNow;
		projection.Version++;

		await _store.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Handles a ProductPriceChanged event.
	/// </summary>
	public async Task HandleAsync(ProductPriceChanged @event, string category, CancellationToken cancellationToken)
	{
		var categoryId = category.ToUpperInvariant().Replace(" ", "-", StringComparison.Ordinal);
		var projection = await _store.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);

		if (projection == null)
		{
			return;
		}

		var productId = @event.ProductId.ToString();
		projection.ProductPrices[productId] = @event.NewPrice;

		RecalculatePriceStatistics(projection);
		RecalculateInventoryValue(projection);

		projection.LastModified = DateTimeOffset.UtcNow;
		projection.Version++;

		await _store.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Handles a ProductStockAdded event.
	/// </summary>
	public async Task HandleAsync(ProductStockAdded @event, string category, CancellationToken cancellationToken)
	{
		var categoryId = category.ToUpperInvariant().Replace(" ", "-", StringComparison.Ordinal);
		var projection = await _store.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);

		if (projection == null)
		{
			return;
		}

		var productId = @event.ProductId.ToString();
		var oldStock = projection.ProductStocks.GetValueOrDefault(productId, 0);
		projection.ProductStocks[productId] = @event.NewStockLevel;

		// Update stock counters
		if (oldStock == 0 && @event.NewStockLevel > 0)
		{
			projection.ProductsInStock++;
		}

		if (oldStock <= 10 && @event.NewStockLevel > 10)
		{
			projection.ProductsLowStock--;
		}
		else if (oldStock > 10 && @event.NewStockLevel <= 10)
		{
			projection.ProductsLowStock++;
		}

		RecalculateInventoryValue(projection);

		projection.LastModified = DateTimeOffset.UtcNow;
		projection.Version++;

		await _store.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Handles a ProductStockRemoved event.
	/// </summary>
	public async Task HandleAsync(ProductStockRemoved @event, string category, CancellationToken cancellationToken)
	{
		var categoryId = category.ToUpperInvariant().Replace(" ", "-", StringComparison.Ordinal);
		var projection = await _store.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);

		if (projection == null)
		{
			return;
		}

		var productId = @event.ProductId.ToString();
		var oldStock = projection.ProductStocks.GetValueOrDefault(productId, 0);
		projection.ProductStocks[productId] = @event.NewStockLevel;

		// Update stock counters
		if (oldStock > 0 && @event.NewStockLevel == 0)
		{
			projection.ProductsInStock--;
		}

		if (oldStock > 10 && @event.NewStockLevel <= 10 && @event.NewStockLevel > 0)
		{
			projection.ProductsLowStock++;
		}
		else if (oldStock <= 10 && @event.NewStockLevel == 0)
		{
			projection.ProductsLowStock--;
		}

		RecalculateInventoryValue(projection);

		projection.LastModified = DateTimeOffset.UtcNow;
		projection.Version++;

		await _store.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Handles a ProductDiscontinued event.
	/// </summary>
	public async Task HandleAsync(ProductDiscontinued @event, string category, CancellationToken cancellationToken)
	{
		var categoryId = category.ToUpperInvariant().Replace(" ", "-", StringComparison.Ordinal);
		var projection = await _store.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);

		if (projection == null)
		{
			return;
		}

		var productId = @event.ProductId.ToString();
		var stock = projection.ProductStocks.GetValueOrDefault(productId, 0);

		projection.ActiveProducts--;

		if (stock > 0)
		{
			projection.ProductsInStock--;
			if (stock <= 10)
			{
				projection.ProductsLowStock--;
			}
		}

		// Remove from tracking (product is discontinued)
		_ = projection.ProductPrices.Remove(productId);
		_ = projection.ProductStocks.Remove(productId);

		RecalculatePriceStatistics(projection);
		RecalculateInventoryValue(projection);

		projection.LastModified = DateTimeOffset.UtcNow;
		projection.Version++;

		await _store.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);
	}

	private static void RecalculatePriceStatistics(CategorySummaryProjection projection)
	{
		if (projection.ProductPrices.Count == 0)
		{
			projection.AveragePrice = 0;
			projection.MinPrice = 0;
			projection.MaxPrice = 0;
			return;
		}

		var prices = projection.ProductPrices.Values.ToList();
		projection.AveragePrice = Math.Round(prices.Average(), 2);
		projection.MinPrice = prices.Min();
		projection.MaxPrice = prices.Max();
	}

	private static void RecalculateInventoryValue(CategorySummaryProjection projection)
	{
		projection.TotalInventoryValue = projection.ProductStocks
			.Sum(ps => ps.Value * projection.ProductPrices.GetValueOrDefault(ps.Key, 0));
	}
}
