// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcEventStoreElasticsearch.Domain;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace CdcEventStoreElasticsearch.Projections;

/// <summary>
/// Handles domain events to update the customer search projection in Elasticsearch.
/// </summary>
public sealed class CustomerSearchProjectionHandler
{
	private readonly IProjectionStore<CustomerSearchProjection> _projectionStore;
	private readonly ILogger<CustomerSearchProjectionHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CustomerSearchProjectionHandler"/> class.
	/// </summary>
	public CustomerSearchProjectionHandler(
		IProjectionStore<CustomerSearchProjection> projectionStore,
		ILogger<CustomerSearchProjectionHandler> logger)
	{
		_projectionStore = projectionStore;
		_logger = logger;
	}

	/// <summary>
	/// Handles any domain event by routing to the appropriate handler method.
	/// </summary>
	public Task HandleEventAsync(IDomainEvent @event, CancellationToken cancellationToken) => @event switch
	{
		CustomerCreated e => HandleAsync(e, cancellationToken),
		CustomerInfoUpdated e => HandleAsync(e, cancellationToken),
		CustomerOrderPlaced e => HandleAsync(e, cancellationToken),
		CustomerDeactivated e => HandleAsync(e, cancellationToken),
		_ => Task.CompletedTask
	};

	private static string CalculateTier(decimal totalSpent) => totalSpent switch
	{
		>= 10000m => "Platinum",
		>= 5000m => "Gold",
		>= 1000m => "Silver",
		_ => "Bronze"
	};

	private async Task HandleAsync(CustomerCreated e, CancellationToken cancellationToken)
	{
		var projection = new CustomerSearchProjection
		{
			Id = e.CustomerId.ToString(),
			CustomerId = e.CustomerId,
			ExternalId = e.ExternalId,
			Name = e.Name,
			Email = e.Email,
			Phone = e.Phone,
			OrderCount = 0,
			TotalSpent = 0,
			Tier = "Bronze",
			IsActive = true,
			CreatedAt = e.OccurredAt,
			Tags = ["new-customer"]
		};

		await _projectionStore.UpsertAsync(projection.Id, projection, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Created customer search projection for {CustomerId}",
			e.CustomerId);
	}

	private async Task HandleAsync(CustomerInfoUpdated e, CancellationToken cancellationToken)
	{
		var id = e.CustomerId.ToString();
		var existing = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (existing is null)
		{
			_logger.LogWarning(
				"Customer search projection not found for {CustomerId} during info update",
				e.CustomerId);
			return;
		}

		existing.Name = e.Name;
		existing.Email = e.Email;
		existing.Phone = e.Phone;
		existing.LastUpdatedAt = e.OccurredAt;

		// Remove new-customer tag after first update
		_ = existing.Tags.Remove("new-customer");

		await _projectionStore.UpsertAsync(id, existing, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Updated customer search projection for {CustomerId}",
			e.CustomerId);
	}

	private async Task HandleAsync(CustomerOrderPlaced e, CancellationToken cancellationToken)
	{
		var id = e.CustomerId.ToString();
		var existing = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (existing is null)
		{
			_logger.LogWarning(
				"Customer search projection not found for {CustomerId} during order placement",
				e.CustomerId);
			return;
		}

		existing.OrderCount++;
		existing.TotalSpent += e.Amount;
		existing.Tier = CalculateTier(existing.TotalSpent);
		existing.LastUpdatedAt = e.OccurredAt;

		// Update tags based on activity
		if (existing.OrderCount == 1 && !existing.Tags.Contains("first-order"))
		{
			existing.Tags.Add("first-order");
		}

		if (existing.TotalSpent >= 1000 && !existing.Tags.Contains("high-value"))
		{
			existing.Tags.Add("high-value");
		}

		await _projectionStore.UpsertAsync(id, existing, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Updated customer search projection for {CustomerId} after order",
			e.CustomerId);
	}

	private async Task HandleAsync(CustomerDeactivated e, CancellationToken cancellationToken)
	{
		var id = e.CustomerId.ToString();
		var existing = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (existing is null)
		{
			_logger.LogWarning(
				"Customer search projection not found for {CustomerId} during deactivation",
				e.CustomerId);
			return;
		}

		existing.IsActive = false;
		existing.LastUpdatedAt = e.OccurredAt;

		if (!existing.Tags.Contains("deactivated"))
		{
			existing.Tags.Add("deactivated");
		}

		await _projectionStore.UpsertAsync(id, existing, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Deactivated customer search projection for {CustomerId}",
			e.CustomerId);
	}
}

/// <summary>
/// Handles domain events to update the customer tier summary projection in Elasticsearch.
/// This is a multi-stream projection that aggregates across all customers.
/// </summary>
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
