// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcEventStoreElasticsearch.Domain;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;

namespace CdcEventStoreElasticsearch.Projections;

// ============================================================================
// Customer Search Projection Handlers
// ============================================================================
// These handlers demonstrate the IProjectionEventHandler<T, TEvent> pattern
// (Tier 3) for projection logic requiring DI, async operations, and logging.
//
// Registration in Program.cs:
//   builder.AddProjection<CustomerSearchProjection>(p => p
//       .Inline()
//       .WhenHandledBy<CustomerCreated, CustomerCreatedHandler>()
//       .WhenHandledBy<CustomerOrderPlaced, CustomerOrderPlacedHandler>()
//       .When<CustomerDeactivated>((proj, e) => { proj.IsActive = false; }));
//
// The framework manages projection load/upsert. Handlers just mutate state.

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
/// Handles <see cref="CustomerOrderPlaced"/> events by updating order counts,
/// spending totals, and tier calculations. Demonstrates DI injection for logging.
/// </summary>
public sealed class CustomerOrderPlacedHandler
	: IProjectionEventHandler<CustomerSearchProjection, CustomerOrderPlaced>
{
	private readonly ILogger<CustomerOrderPlacedHandler> _logger;

	public CustomerOrderPlacedHandler(ILogger<CustomerOrderPlacedHandler> logger)
	{
		_logger = logger;
	}

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

		// Update tags based on activity
		if (projection.OrderCount == 1 && !projection.Tags.Contains("first-order"))
		{
			projection.Tags.Add("first-order");
		}

		if (projection.TotalSpent >= 1000 && !projection.Tags.Contains("high-value"))
		{
			projection.Tags.Add("high-value");
		}

		_logger.LogDebug(
			"Updated customer {CustomerId} after order -- tier: {Tier}",
			context.AggregateId,
			projection.Tier);

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
/// Handles domain events to update the customer tier summary projection in Elasticsearch.
/// This is a multi-stream projection that aggregates across all customers.
/// </summary>
/// <remarks>
/// This handler manages its own store operations because it uses custom projection IDs
/// (tier name, not aggregate ID) and requires extra parameters. This is the manual
/// handler pattern for complex multi-stream projections.
/// </remarks>
public sealed class CustomerTierSummaryProjectionHandler
{
	private readonly IProjectionStore<CustomerTierSummaryProjection> _projectionStore;
	private readonly ILogger<CustomerTierSummaryProjectionHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CustomerTierSummaryProjectionHandler"/> class.
	/// </summary>
	public CustomerTierSummaryProjectionHandler(
		IProjectionStore<CustomerTierSummaryProjection> projectionStore,
		ILogger<CustomerTierSummaryProjectionHandler> logger)
	{
		_projectionStore = projectionStore;
		_logger = logger;
	}

	/// <summary>
	/// Handles customer created event.
	/// </summary>
	public async Task HandleAsync(CustomerCreated e, CancellationToken cancellationToken)
	{
		var tier = "Bronze";
		await UpdateTierSummaryAsync(tier, summary =>
		{
			summary.CustomerCount++;
			summary.ActiveCount++;
		}, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Updated tier summary for new customer in {Tier}", tier);
	}

	/// <summary>
	/// Handles customer order to update tier summaries.
	/// </summary>
	public async Task HandleAsync(
		CustomerOrderPlaced e,
		string previousTier,
		string newTier,
		CancellationToken cancellationToken)
	{
		// Update old tier
		await UpdateTierSummaryAsync(previousTier, summary =>
		{
			summary.TotalOrders++;
			summary.TotalRevenue += e.Amount;
			RecalculateAverageSpend(summary);
		}, cancellationToken).ConfigureAwait(false);

		// If tier changed, move customer between tiers
		if (previousTier != newTier)
		{
			await UpdateTierSummaryAsync(previousTier, summary =>
			{
				summary.CustomerCount = Math.Max(0, summary.CustomerCount - 1);
				summary.ActiveCount = Math.Max(0, summary.ActiveCount - 1);
				RecalculateAverageSpend(summary);
			}, cancellationToken).ConfigureAwait(false);

			await UpdateTierSummaryAsync(newTier, summary =>
			{
				summary.CustomerCount++;
				summary.ActiveCount++;
				RecalculateAverageSpend(summary);
			}, cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("Customer moved from {OldTier} to {NewTier}", previousTier, newTier);
		}
	}

	/// <summary>
	/// Handles customer deactivation.
	/// </summary>
	public async Task HandleAsync(CustomerDeactivated e, string tier, CancellationToken cancellationToken)
	{
		await UpdateTierSummaryAsync(tier, summary =>
		{
			summary.ActiveCount = Math.Max(0, summary.ActiveCount - 1);
		}, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Updated tier summary for deactivated customer in {Tier}", tier);
	}

	private static void RecalculateAverageSpend(CustomerTierSummaryProjection summary)
	{
		summary.AverageSpend = summary.CustomerCount > 0
			? summary.TotalRevenue / summary.CustomerCount
			: 0;
	}

	private async Task UpdateTierSummaryAsync(
		string tier,
		Action<CustomerTierSummaryProjection> update,
		CancellationToken cancellationToken)
	{
		var id = tier.ToUpperInvariant();
		var summary = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (summary is null)
		{
			summary = new CustomerTierSummaryProjection { Id = id, Tier = tier };
		}

		update(summary);
		summary.LastUpdatedAt = DateTimeOffset.UtcNow;

		await _projectionStore.UpsertAsync(id, summary, cancellationToken).ConfigureAwait(false);
	}
}
