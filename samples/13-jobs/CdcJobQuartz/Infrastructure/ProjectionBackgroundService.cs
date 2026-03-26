// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

using CdcJobQuartz.Domain;
using CdcJobQuartz.Projections;

using Excalibur.Dispatch.Abstractions;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CdcJobQuartz.Infrastructure;

/// <summary>
/// Configuration options for projection processing.
/// </summary>
public sealed class ProjectionOptions
{
	/// <summary>
	/// Configuration section name.
	/// </summary>
	public const string SectionName = "Projections";

	/// <summary>Gets or sets the polling interval for new events.</summary>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>Gets or sets the batch size for event processing.</summary>
	public int BatchSize { get; set; } = 100;

	/// <summary>Gets or sets whether to rebuild projections on startup.</summary>
	public bool RebuildOnStartup { get; set; }
}

/// <summary>
/// Background service that processes events from the event store and updates projections.
/// This creates materialized views in Elasticsearch from domain events.
/// </summary>
/// <remarks>
/// <para>
/// This service provides asynchronous projection updates using event store position tracking.
/// In production, use <c>GlobalStreamProjectionHost</c> for built-in checkpoint management.
/// </para>
/// </remarks>
public sealed class ProjectionBackgroundService : BackgroundService
{
	private readonly IEventStore _eventStore;
	private readonly CustomerSearchProjectionHandler _searchHandler;
	private readonly CustomerTierSummaryProjectionHandler _tierHandler;
	private readonly ProjectionOptions _options;
	private readonly ILogger<ProjectionBackgroundService> _logger;

	// Track tier state for summary projection (in production, this would be persisted)
	private readonly Dictionary<Guid, string> _customerTiers = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionBackgroundService"/> class.
	/// </summary>
	public ProjectionBackgroundService(
		IEventStore eventStore,
		CustomerSearchProjectionHandler searchHandler,
		CustomerTierSummaryProjectionHandler tierHandler,
		IOptions<ProjectionOptions> options,
		ILogger<ProjectionBackgroundService> logger)
	{
		_eventStore = eventStore;
		_searchHandler = searchHandler;
		_tierHandler = tierHandler;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc/>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation(
			"Projection Background Service starting. Interval: {Interval}, BatchSize: {BatchSize}",
			_options.PollingInterval,
			_options.BatchSize);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(_options.PollingInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during projection processing cycle");
				await Task.Delay(_options.PollingInterval, stoppingToken).ConfigureAwait(false);
			}
		}

		_logger.LogInformation("Projection Background Service stopped");
	}

	private async Task UpdateTierSummaryAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
	{
		switch (domainEvent)
		{
			case CustomerCreated created:
				_customerTiers[created.CustomerId] = "Bronze";
				await _tierHandler.HandleAsync(created, cancellationToken).ConfigureAwait(false);
				break;

			case CustomerOrderPlaced orderPlaced:
				var prevTier = _customerTiers.GetValueOrDefault(orderPlaced.CustomerId, "Bronze");
				var newTier = CalculateTierFromOrder(orderPlaced.Amount, prevTier);
				await _tierHandler.HandleAsync(orderPlaced, prevTier, newTier, cancellationToken).ConfigureAwait(false);
				_customerTiers[orderPlaced.CustomerId] = newTier;
				break;

			case CustomerDeactivated deactivated:
				var tier = _customerTiers.GetValueOrDefault(deactivated.CustomerId, "Bronze");
				await _tierHandler.HandleAsync(deactivated, tier, cancellationToken).ConfigureAwait(false);
				break;
		}
	}

	private static string CalculateTierFromOrder(decimal orderAmount, string currentTier)
	{
		return currentTier;
	}

	private IDomainEvent? DeserializeEvent(StoredEvent storedEvent)
	{
		try
		{
			return storedEvent.EventType switch
			{
				nameof(CustomerCreated) => JsonSerializer.Deserialize<CustomerCreated>(storedEvent.EventData),
				nameof(CustomerInfoUpdated) => JsonSerializer.Deserialize<CustomerInfoUpdated>(storedEvent.EventData),
				nameof(CustomerOrderPlaced) => JsonSerializer.Deserialize<CustomerOrderPlaced>(storedEvent.EventData),
				nameof(CustomerDeactivated) => JsonSerializer.Deserialize<CustomerDeactivated>(storedEvent.EventData),
				_ => null,
			};
		}
		catch (JsonException ex)
		{
			_logger.LogWarning(ex, "Failed to deserialize event {EventType}", storedEvent.EventType);
			return null;
		}
	}
}
