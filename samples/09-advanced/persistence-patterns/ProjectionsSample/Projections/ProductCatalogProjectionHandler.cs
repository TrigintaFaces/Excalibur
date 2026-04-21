// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;

using ProjectionsSample.Domain;

namespace ProjectionsSample.Projections;

// ============================================================================
// Tier 3: DI-Resolved Typed Handler (IProjectionEventHandler<T, TEvent>)
// ============================================================================
// This demonstrates the NEW IProjectionEventHandler pattern for projection logic
// that needs dependency injection, async operations, or custom projection IDs.
//
// Registration: builder.WhenHandledBy<ProductCreated, ProductCreatedHandler>()
// The framework resolves the handler from DI, passes the projection instance,
// and handles load/upsert automatically.
//
// For simple projections, prefer When<T> lambdas (see Program.cs).
// Mix and match both approaches within the same projection.

/// <summary>
/// Handles <see cref="ProductCreated"/> events for the <see cref="ProductCatalogProjection"/>.
/// Demonstrates DI-resolved handler with constructor injection.
/// </summary>
/// <remarks>
/// Registered via <c>WhenHandledBy&lt;ProductCreated, ProductCreatedHandler&gt;()</c>.
/// The framework loads the projection, passes it to this handler, then upserts the result.
/// </remarks>
public sealed class ProductCreatedHandler
	: IProjectionEventHandler<ProductCatalogProjection, ProductCreated>
{
	private readonly ILogger<ProductCreatedHandler> _logger;

	public ProductCreatedHandler(ILogger<ProductCreatedHandler> logger)
	{
		_logger = logger;
	}

	public Task HandleAsync(
		ProductCatalogProjection projection,
		ProductCreated @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Id = @event.ProductId.ToString();
		projection.Name = @event.Name;
		projection.Category = @event.Category;
		projection.CurrentPrice = @event.Price;
		projection.OriginalPrice = @event.Price;
		projection.StockLevel = @event.InitialStock;
		projection.IsActive = true;
		projection.CreatedAt = @event.OccurredAt;
		projection.LastModified = context.Timestamp;
		projection.Version = @event.Version;

		_logger.LogDebug(
			"Created product catalog projection for {ProductId} ({Name})",
			@event.ProductId,
			@event.Name);

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles <see cref="ProductDiscontinued"/> events for the <see cref="ProductCatalogProjection"/>.
/// Demonstrates DI-resolved handler for events that need logging or external service calls.
/// </summary>
public sealed class ProductDiscontinuedHandler
	: IProjectionEventHandler<ProductCatalogProjection, ProductDiscontinued>
{
	private readonly ILogger<ProductDiscontinuedHandler> _logger;

	public ProductDiscontinuedHandler(ILogger<ProductDiscontinuedHandler> logger)
	{
		_logger = logger;
	}

	public Task HandleAsync(
		ProductCatalogProjection projection,
		ProductDiscontinued @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.IsActive = false;
		projection.LastModified = context.Timestamp;
		projection.Version = @event.Version;

		_logger.LogInformation(
			"Product {ProductId} discontinued: {Reason}",
			@event.ProductId,
			@event.Reason);

		return Task.CompletedTask;
	}
}
