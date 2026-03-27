// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.EventSourcing.Abstractions;

using ProjectionsSample.Domain;

namespace ProjectionsSample.Projections;

// ============================================================================
// Product Catalog Projection Handler (Alternative: Manual Handler Approach)
// ============================================================================
// This handler demonstrates the MANUAL projection pattern where events are
// iterated explicitly and handlers are called directly. This is the OLD pattern.
//
// ** The PREFERRED approach is AddProjection<T>().Inline() **
// See Program.cs for the inline projection registration that replaces this handler.
// The framework automatically runs inline projections during SaveAsync(),
// eliminating the need for manual event iteration.
//
// This file is kept as a reference for the handler-based approach, which is still
// useful for multi-stream projections (like CategorySummaryProjectionHandler)
// where the projection key differs from the aggregate ID.

/// <summary>
/// Handles events for the ProductCatalogProjection.
/// </summary>
/// <remarks>
/// <para>
/// This is the handler-based projection approach. For single-stream projections
/// keyed by aggregate ID, prefer <c>AddProjection&lt;T&gt;().Inline()</c> instead,
/// which runs automatically during <c>SaveAsync()</c>.
/// </para>
/// <para>
/// See <c>Program.cs</c> for the inline projection registration that replaces
/// direct usage of this handler.
/// </para>
/// </remarks>
public sealed class ProductCatalogProjectionHandler : IProjectionHandler
{
	private readonly IProjectionStore<ProductCatalogProjection> _store;

	public ProductCatalogProjectionHandler(IProjectionStore<ProductCatalogProjection> store)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
	}

	/// <summary>
	/// Handles a ProductCreated event.
	/// </summary>
	public async Task HandleAsync(ProductCreated @event, CancellationToken cancellationToken)
	{
		var projection = new ProductCatalogProjection
		{
			Id = @event.ProductId.ToString(),
			Name = @event.Name,
			Category = @event.Category,
			CurrentPrice = @event.Price,
			OriginalPrice = @event.Price,
			StockLevel = @event.InitialStock,
			IsActive = true,
			CreatedAt = @event.OccurredAt,
			LastModified = DateTimeOffset.UtcNow,
			Version = @event.Version
		};

		await _store.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Handles a ProductPriceChanged event.
	/// </summary>
	public async Task HandleAsync(ProductPriceChanged @event, CancellationToken cancellationToken)
	{
		var projection = await _store.GetByIdAsync(@event.ProductId.ToString(), cancellationToken)
			.ConfigureAwait(false);

		if (projection != null)
		{
			projection.CurrentPrice = @event.NewPrice;
			projection.LastModified = DateTimeOffset.UtcNow;
			projection.Version = @event.Version;

			await _store.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Handles a ProductStockAdded event.
	/// </summary>
	public async Task HandleAsync(ProductStockAdded @event, CancellationToken cancellationToken)
	{
		var projection = await _store.GetByIdAsync(@event.ProductId.ToString(), cancellationToken)
			.ConfigureAwait(false);

		if (projection != null)
		{
			projection.StockLevel = @event.NewStockLevel;
			projection.LastModified = DateTimeOffset.UtcNow;
			projection.Version = @event.Version;

			await _store.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Handles a ProductStockRemoved event.
	/// </summary>
	public async Task HandleAsync(ProductStockRemoved @event, CancellationToken cancellationToken)
	{
		var projection = await _store.GetByIdAsync(@event.ProductId.ToString(), cancellationToken)
			.ConfigureAwait(false);

		if (projection != null)
		{
			projection.StockLevel = @event.NewStockLevel;
			projection.LastModified = DateTimeOffset.UtcNow;
			projection.Version = @event.Version;

			await _store.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Handles a ProductDiscontinued event.
	/// </summary>
	public async Task HandleAsync(ProductDiscontinued @event, CancellationToken cancellationToken)
	{
		var projection = await _store.GetByIdAsync(@event.ProductId.ToString(), cancellationToken)
			.ConfigureAwait(false);

		if (projection != null)
		{
			projection.IsActive = false;
			projection.LastModified = DateTimeOffset.UtcNow;
			projection.Version = @event.Version;

			await _store.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Dispatches an event to the appropriate handler method.
	/// </summary>
	public Task HandleEventAsync(IDomainEvent @event, CancellationToken cancellationToken) => @event switch
	{
		ProductCreated e => HandleAsync(e, cancellationToken),
		ProductPriceChanged e => HandleAsync(e, cancellationToken),
		ProductStockAdded e => HandleAsync(e, cancellationToken),
		ProductStockRemoved e => HandleAsync(e, cancellationToken),
		ProductDiscontinued e => HandleAsync(e, cancellationToken),
		_ => Task.CompletedTask // Unknown event, ignore
	};
}
