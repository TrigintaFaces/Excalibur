// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using CdcEventStoreElasticsearch.Domain;
using CdcEventStoreElasticsearch.Projections;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Options;

namespace CdcEventStoreElasticsearch.Infrastructure;

/// <summary>
/// Serializes and deserializes domain events.
/// </summary>
public interface IEventSerializer
{
	/// <summary>
	/// Serializes an event to bytes.
	/// </summary>
	byte[] Serialize<T>(T @event) where T : IDomainEvent;

	/// <summary>
	/// Deserializes an event from bytes.
	/// </summary>
	T? Deserialize<T>(byte[] data) where T : IDomainEvent;
}

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
/// </summary>
/// <remarks>
/// <para>
/// This service provides asynchronous projection updates using the outbox pattern:
/// </para>
/// <list type="bullet">
/// <item>Polls event store for undispatched events (outbox pattern)</item>
/// <item>Processes events in order</item>
/// <item>Updates Elasticsearch projections</item>
/// <item>Marks events as dispatched after successful projection update</item>
/// </list>
/// </remarks>
public sealed class ProjectionBackgroundService : BackgroundService
{
	private readonly IEventStore _eventStore;
	private readonly IEventSerializer _eventSerializer;
	private readonly CustomerSearchProjectionHandler _searchHandler;
	private readonly CustomerTierSummaryProjectionHandler _tierHandler;
	private readonly ProjectionOptions _options;
	private readonly ILogger<ProjectionBackgroundService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionBackgroundService"/> class.
	/// </summary>
	public ProjectionBackgroundService(
		IEventStore eventStore,
		IEventSerializer eventSerializer,
		CustomerSearchProjectionHandler searchHandler,
		CustomerTierSummaryProjectionHandler tierHandler,
		IOptions<ProjectionOptions> options,
		ILogger<ProjectionBackgroundService> logger)
	{
		_eventStore = eventStore;
		_eventSerializer = eventSerializer;
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
				var processed = await ProcessUndispatchedEventsAsync(stoppingToken).ConfigureAwait(false);

				// If we processed events, check again immediately
				// Otherwise, wait for the polling interval
				if (processed == 0)
				{
					await Task.Delay(_options.PollingInterval, stoppingToken).ConfigureAwait(false);
				}
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

	private static string CalculateTierFromAmount(decimal amount, string currentTier)
	{
		// Simplified tier calculation for demonstration
		// In production, this would query the actual aggregate state
		return currentTier;
	}

	private async Task<int> ProcessUndispatchedEventsAsync(CancellationToken cancellationToken)
	{
		// Use the outbox pattern: get events that haven't been dispatched yet
		var events = await _eventStore.GetUndispatchedEventsAsync(
			_options.BatchSize,
			cancellationToken).ConfigureAwait(false);

		if (events.Count == 0)
		{
			return 0;
		}

		_logger.LogDebug("Processing {Count} undispatched events for projections", events.Count);

		// Track tier state for summary projection
		var previousTiers = new Dictionary<Guid, string>();

		foreach (var storedEvent in events)
		{
			try
			{
				// Deserialize the domain event from the stored bytes
				var domainEvent = DeserializeEvent(storedEvent);
				if (domainEvent is null)
				{
					_logger.LogWarning(
						"Could not deserialize event {EventType} at version {Version}",
						storedEvent.EventType,
						storedEvent.Version);

					// Mark as dispatched to avoid reprocessing
					await _eventStore.MarkEventAsDispatchedAsync(storedEvent.EventId, cancellationToken)
						.ConfigureAwait(false);
					continue;
				}

				// Update search projection
				await _searchHandler.HandleEventAsync(domainEvent, cancellationToken).ConfigureAwait(false);

				// Update tier summary projection
				await UpdateTierSummaryAsync(domainEvent, previousTiers, cancellationToken).ConfigureAwait(false);

				// Mark the event as dispatched (outbox pattern)
				await _eventStore.MarkEventAsDispatchedAsync(storedEvent.EventId, cancellationToken)
					.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Error processing event {EventType} at version {Version}",
					storedEvent.EventType,
					storedEvent.Version);
			}
		}

		return events.Count;
	}

	private async Task UpdateTierSummaryAsync(
		IDomainEvent domainEvent,
		Dictionary<Guid, string> previousTiers,
		CancellationToken cancellationToken)
	{
		switch (domainEvent)
		{
			case CustomerCreated created:
				previousTiers[created.CustomerId] = "Bronze";
				await _tierHandler.HandleAsync(created, cancellationToken).ConfigureAwait(false);
				break;

			case CustomerOrderPlaced orderPlaced:
				var prevTier = previousTiers.GetValueOrDefault(orderPlaced.CustomerId, "Bronze");
				var newTier = CalculateTierFromAmount(orderPlaced.Amount, prevTier);
				await _tierHandler.HandleAsync(orderPlaced, prevTier, newTier, cancellationToken).ConfigureAwait(false);
				previousTiers[orderPlaced.CustomerId] = newTier;
				break;

			case CustomerDeactivated deactivated:
				var tier = previousTiers.GetValueOrDefault(deactivated.CustomerId, "Bronze");
				await _tierHandler.HandleAsync(deactivated, tier, cancellationToken).ConfigureAwait(false);
				break;
		}
	}

	private IDomainEvent? DeserializeEvent(StoredEvent storedEvent)
	{
		// Use the event serializer to deserialize the event data
		// The EventType tells us what type to deserialize to
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

/// <summary>
/// JSON-based event serializer.
/// </summary>
public sealed class JsonEventSerializer : IEventSerializer
{
	private readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false, };

	/// <inheritdoc/>
	public byte[] Serialize<T>(T @event) where T : IDomainEvent
	{
		return JsonSerializer.SerializeToUtf8Bytes(@event, _options);
	}

	/// <inheritdoc/>
	public T? Deserialize<T>(byte[] data) where T : IDomainEvent
	{
		return JsonSerializer.Deserialize<T>(data, _options);
	}
}
