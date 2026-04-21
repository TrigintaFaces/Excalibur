// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using CdcJobQuartz.Domain;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;

namespace CdcJobQuartz.Projections;

// ============================================================================
// Customer Search Projection Handlers
// ============================================================================
// These handlers use the IProjectionEventHandler<T, TEvent> pattern.
// The framework manages projection load/upsert automatically.
// Handlers only mutate the projection state passed to them.

/// <summary>
/// Handles <see cref="CustomerCreated"/> events by initializing a new customer
/// search projection with all relevant fields.
/// </summary>
public sealed class CustomerCreatedHandler
	: IProjectionEventHandler<CustomerSearchProjection, CustomerCreated>
{
	private readonly ILogger<CustomerCreatedHandler> _logger;

	public CustomerCreatedHandler(ILogger<CustomerCreatedHandler> logger)
	{
		_logger = logger;
	}

	public Task HandleAsync(
		CustomerSearchProjection projection,
		CustomerCreated @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Id = @event.CustomerId.ToString();
		projection.CustomerId = @event.CustomerId;
		projection.ExternalId = @event.ExternalId;
		projection.Name = @event.Name;
		projection.Email = @event.Email;
		projection.Phone = @event.Phone;
		projection.OrderCount = 0;
		projection.TotalSpent = 0;
		projection.Tier = "Bronze";
		projection.IsActive = true;
		projection.CreatedAt = @event.OccurredAt;
		projection.Tags = ["new-customer"];

		_logger.LogDebug(
			"Created customer search projection for {CustomerId}",
			@event.CustomerId);

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles <see cref="CustomerInfoUpdated"/> events by updating name, email, and phone.
/// </summary>
public sealed class CustomerInfoUpdatedHandler
	: IProjectionEventHandler<CustomerSearchProjection, CustomerInfoUpdated>
{
	public Task HandleAsync(
		CustomerSearchProjection projection,
		CustomerInfoUpdated @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Name = @event.Name;
		projection.Email = @event.Email;
		projection.Phone = @event.Phone;
		projection.LastUpdatedAt = @event.OccurredAt;
		_ = projection.Tags.Remove("new-customer");

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles <see cref="CustomerOrderPlaced"/> events by updating order counts,
/// spending totals, and tier calculations.
/// </summary>
public sealed class CustomerOrderPlacedHandler
	: IProjectionEventHandler<CustomerSearchProjection, CustomerOrderPlaced>
{
	public Task HandleAsync(
		CustomerSearchProjection projection,
		CustomerOrderPlaced @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.OrderCount++;
		projection.TotalSpent += @event.Amount;
		projection.Tier = CalculateTier(projection.TotalSpent);
		projection.LastUpdatedAt = @event.OccurredAt;

		if (projection.OrderCount == 1 && !projection.Tags.Contains("first-order"))
		{
			projection.Tags.Add("first-order");
		}

		if (projection.TotalSpent >= 1000 && !projection.Tags.Contains("high-value"))
		{
			projection.Tags.Add("high-value");
		}

		return Task.CompletedTask;
	}

	private static string CalculateTier(decimal totalSpent) => totalSpent switch
	{
		>= 10000m => "Platinum",
		>= 5000m => "Gold",
		>= 1000m => "Silver",
		_ => "Bronze"
	};
}

/// <summary>
/// Handles <see cref="CustomerTierSummaryProjection"/> for per-tier aggregation.
/// Uses <see cref="ProjectionHandlerContext.OverrideProjectionId"/> to route to
/// tier-specific projections (e.g., "BRONZE", "SILVER").
/// </summary>
/// <remarks>
/// Multi-stream projection: a single customer event updates the relevant tier summary.
/// The framework manages the load/upsert cycle using the overridden projection ID.
/// </remarks>
public sealed class CustomerTierSummaryCreatedHandler
	: IProjectionEventHandler<CustomerTierSummaryProjection, CustomerCreated>
{
	public Task HandleAsync(
		CustomerTierSummaryProjection projection,
		CustomerCreated @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		// Route to the Bronze tier summary (new customers start as Bronze)
		context.OverrideProjectionId = "BRONZE";

		projection.Id = "BRONZE";
		projection.Tier = "Bronze";
		projection.CustomerCount++;
		projection.ActiveCount++;
		projection.LastUpdatedAt = DateTimeOffset.UtcNow;

		return Task.CompletedTask;
	}
}
