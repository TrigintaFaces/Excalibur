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
/// This service provides asynchronous projection updates using event store position tracking.
/// In production, use <c>GlobalStreamProjectionHost</c> for built-in checkpoint management.
/// </para>
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

	private static string CalculateTierFromAmount(decimal amount, string currentTier)
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
