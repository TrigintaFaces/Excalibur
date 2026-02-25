// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text.Json;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Observability;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Observability;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.CosmosDb;

/// <summary>
/// Azure Cosmos DB implementation of the cloud-native event store.
/// </summary>
public sealed partial class CosmosDbEventStore : ICloudNativeEventStore, IEventStore, IAsyncDisposable
{
	private readonly CosmosClient _cosmosClient;
	private readonly IOptions<CosmosDbEventStoreOptions> _options;
	private readonly ILogger<CosmosDbEventStore> _logger;

	private Container? _container;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbEventStore"/> class.
	/// </summary>
	/// <param name="cosmosClient">The Cosmos DB client.</param>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger.</param>
	public CosmosDbEventStore(
		CosmosClient cosmosClient,
		IOptions<CosmosDbEventStoreOptions> options,
		ILogger<CosmosDbEventStore> logger)
	{
		_cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public CloudProviderType ProviderType => CloudProviderType.CosmosDb;

	/// <inheritdoc/>
	public async Task<CloudEventLoadResult> LoadAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType);

		try
		{
			var streamId = BuildStreamId(aggregateType, aggregateId);
			var query = new QueryDefinition("SELECT * FROM c WHERE c.streamId = @streamId ORDER BY c.version")
				.WithParameter("@streamId", streamId);

			var events = new List<CloudStoredEvent>();
			double totalRu = 0;
			string? sessionToken = null;

			var queryOptions = new QueryRequestOptions { PartitionKey = new Microsoft.Azure.Cosmos.PartitionKey(streamId) };

			using var iterator = _container.GetItemQueryIterator<EventDocument>(query, requestOptions: queryOptions);

			while (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				totalRu += response.RequestCharge;
				sessionToken = response.Headers.Session;

				foreach (var doc in response)
				{
					events.Add(ToCloudStoredEvent(doc));
				}
			}

			LogLoadingEvents(streamId, events.Count);

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return new CloudEventLoadResult(events, totalRu, sessionToken);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudEventLoadResult> LoadFromVersionAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		long fromVersion,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType, fromVersion);

		try
		{
			var streamId = BuildStreamId(aggregateType, aggregateId);
			var query = new QueryDefinition(
					"SELECT * FROM c WHERE c.streamId = @streamId AND c.version > @fromVersion ORDER BY c.version")
				.WithParameter("@streamId", streamId)
				.WithParameter("@fromVersion", fromVersion);

			var events = new List<CloudStoredEvent>();
			double totalRu = 0;
			string? sessionToken = null;

			var queryOptions = new QueryRequestOptions { PartitionKey = new Microsoft.Azure.Cosmos.PartitionKey(streamId) };

			using var iterator = _container.GetItemQueryIterator<EventDocument>(query, requestOptions: queryOptions);

			while (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				totalRu += response.RequestCharge;
				sessionToken = response.Headers.Session;

				foreach (var doc in response)
				{
					events.Add(ToCloudStoredEvent(doc));
				}
			}

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return new CloudEventLoadResult(events, totalRu, sessionToken);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"load_from_version",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudAppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var eventList = events.ToList();
		var correlationId = ExtractCorrelationId(eventList);
		var messageId = ExtractEventId(eventList);
		if (eventList.Count == 0)
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"append",
				result,
				stopwatch.Elapsed);
			return CloudAppendResult.CreateSuccess(expectedVersion, 0);
		}

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventList.Count, expectedVersion);

		var streamId = BuildStreamId(aggregateType, aggregateId);
		LogAppendingEvents(streamId, aggregateType);

		var pk = new Microsoft.Azure.Cosmos.PartitionKey(streamId);

		try
		{
			CloudAppendResult appendResult;
			if (_options.Value.UseTransactionalBatch && eventList.Count > 1)
			{
				appendResult = await AppendWithTransactionAsync(
						streamId, aggregateId, aggregateType, eventList, expectedVersion, pk, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				appendResult = await AppendSequentiallyAsync(
						streamId, aggregateId, aggregateType, eventList, expectedVersion, pk, cancellationToken)
					.ConfigureAwait(false);
			}

			if (appendResult.Success)
			{
				_ = (activity?.SetTag(EventSourcingTags.Version, appendResult.NextExpectedVersion));
				activity.SetOperationResult(EventSourcingTagValues.Success);
			}
			else
			{
				result = WriteStoreTelemetry.Results.Failure;
				activity.SetOperationResult(EventSourcingTagValues.Failure);
			}

			return appendResult;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
		{
			var currentVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, partitionKey, cancellationToken)
				.ConfigureAwait(false);
			LogConcurrencyConflict(streamId, expectedVersion);
			result = WriteStoreTelemetry.Results.Conflict;
			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
			return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion, ex.RequestCharge);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"append",
				messageId,
				correlationId);
			_logger.LogError(ex, "Failed to append events to {AggregateType}/{AggregateId}", aggregateType, aggregateId);
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"append",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<IChangeFeedSubscription<CloudStoredEvent>> SubscribeToChangesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var subscription = new CosmosDbEventStoreChangeFeedSubscription(
			_container,
			_options.Value,
			_logger);

		return subscription;
	}

	/// <inheritdoc/>
	public async Task<long> GetCurrentVersionAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var streamId = BuildStreamId(aggregateType, aggregateId);
		var query = new QueryDefinition(
				"SELECT VALUE MAX(c.version) FROM c WHERE c.streamId = @streamId")
			.WithParameter("@streamId", streamId);

		var queryOptions = new QueryRequestOptions { PartitionKey = new Microsoft.Azure.Cosmos.PartitionKey(streamId) };

		using var iterator = _container.GetItemQueryIterator<long?>(query, requestOptions: queryOptions);

		if (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			var version = response.FirstOrDefault();
			return version ?? -1;
		}

		return -1;
	}

	// IEventStore implementation
	/// <inheritdoc/>
	async ValueTask<IReadOnlyList<StoredEvent>> IEventStore.LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var partitionKey = new Data.Abstractions.CloudNative.PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await LoadAsync(aggregateId, aggregateType, partitionKey, null, cancellationToken)
			.ConfigureAwait(false);
		return result.Events.Select(ToStoredEvent).ToList();
	}

	/// <inheritdoc/>
	async ValueTask<IReadOnlyList<StoredEvent>> IEventStore.LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var partitionKey = new Data.Abstractions.CloudNative.PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await LoadFromVersionAsync(aggregateId, aggregateType, partitionKey, fromVersion, null, cancellationToken)
			.ConfigureAwait(false);
		return result.Events.Select(ToStoredEvent).ToList();
	}

	/// <inheritdoc/>
	async ValueTask<AppendResult> IEventStore.AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var partitionKey = new Data.Abstractions.CloudNative.PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await AppendAsync(aggregateId, aggregateType, partitionKey, events, expectedVersion, cancellationToken)
			.ConfigureAwait(false);

		if (result.Success)
		{
			return AppendResult.CreateSuccess(result.NextExpectedVersion, 0);
		}

		if (result.IsConcurrencyConflict)
		{
			return AppendResult.CreateConcurrencyConflict(expectedVersion, result.NextExpectedVersion);
		}

		return AppendResult.CreateFailure(result.ErrorMessage ?? "Unknown error");
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
		int batchSize,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var activity = EventSourcingActivitySource.StartGetUndispatchedActivity(batchSize);

		try
		{
			var query = new QueryDefinition(
					"SELECT TOP @batchSize * FROM c WHERE c.isDispatched = false ORDER BY c.timestamp")
				.WithParameter("@batchSize", batchSize);

			var events = new List<StoredEvent>();

			using var iterator = _container.GetItemQueryIterator<EventDocument>(query);

			while (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);

				foreach (var doc in response)
				{
					events.Add(ToStoredEvent(ToCloudStoredEvent(doc)));
				}
			}

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return events;
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"get_undispatched",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkEventAsDispatchedAsync(
		string eventId,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var activity = EventSourcingActivitySource.StartMarkDispatchedActivity(eventId);

		try
		{
			// We need to find and update the event
			var query = new QueryDefinition("SELECT * FROM c WHERE c.eventId = @eventId")
				.WithParameter("@eventId", eventId);

			using var iterator = _container.GetItemQueryIterator<EventDocument>(query);

			if (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				var doc = response.FirstOrDefault();

				if (doc != null)
				{
					doc.IsDispatched = true;
					_ = await _container.ReplaceItemAsync(
						doc,
						doc.Id,
						new Microsoft.Azure.Cosmos.PartitionKey(doc.StreamId),
						cancellationToken: cancellationToken).ConfigureAwait(false);
				}
			}

			activity.SetOperationResult(EventSourcingTagValues.Success);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"mark_dispatched",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		await Task.CompletedTask.ConfigureAwait(false);
	}

	private static EventDocument CreateEventDocument(
		string streamId,
		string aggregateId,
		string aggregateType,
		IDomainEvent evt,
		long version)
	{
		var eventTypeName = EventTypeNameHelper.GetEventTypeName(evt.GetType());

		return new EventDocument
		{
			Id = $"{streamId}:{version}",
			StreamId = streamId,
			EventId = evt.EventId.ToString(),
			AggregateId = aggregateId,
			AggregateType = aggregateType,
			EventType = eventTypeName,
			Version = version,
			Timestamp = evt.OccurredAt,
			EventData = JsonSerializer.SerializeToUtf8Bytes(evt),
			Metadata = evt.Metadata != null ? JsonSerializer.SerializeToUtf8Bytes(evt.Metadata) : null,
			IsDispatched = false
		};
	}

	private static string BuildStreamId(string aggregateType, string aggregateId) =>
		$"{aggregateType}:{aggregateId}";

	private static string? ExtractCorrelationId(IEnumerable<IDomainEvent> events)
	{
		foreach (var @event in events)
		{
			if (@event.Metadata == null)
			{
				continue;
			}

			if (@event.Metadata.TryGetValue("CorrelationId", out var correlationId) ||
			    @event.Metadata.TryGetValue("correlationId", out correlationId))
			{
				return correlationId?.ToString();
			}
		}

		return null;
	}

	private static string? ExtractEventId(IEnumerable<IDomainEvent> events)
	{
		foreach (var @event in events)
		{
			if (!string.IsNullOrWhiteSpace(@event.EventId))
			{
				return @event.EventId;
			}
		}

		return null;
	}

	private static CloudStoredEvent ToCloudStoredEvent(EventDocument doc) =>
		new()
		{
			EventId = doc.EventId,
			AggregateId = doc.AggregateId,
			AggregateType = doc.AggregateType,
			EventType = doc.EventType,
			Version = doc.Version,
			Timestamp = doc.Timestamp,
			EventData = doc.EventData,
			Metadata = doc.Metadata,
			PartitionKeyValue = doc.StreamId,
			DocumentId = doc.Id,
			ETag = doc.ETag,
			IsDispatched = doc.IsDispatched
		};

	private static StoredEvent ToStoredEvent(CloudStoredEvent cloudEvent) =>
		new(
			cloudEvent.EventId,
			cloudEvent.AggregateId,
			cloudEvent.AggregateType,
			cloudEvent.EventType,
			cloudEvent.EventData,
			cloudEvent.Metadata,
			cloudEvent.Version,
			cloudEvent.Timestamp,
			cloudEvent.IsDispatched);

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		var database = _cosmosClient.GetDatabase("events");

		if (_options.Value.CreateContainerIfNotExists)
		{
			var containerProperties = new ContainerProperties(
				_options.Value.EventsContainerName,
				_options.Value.PartitionKeyPath);

			if (_options.Value.DefaultTimeToLiveSeconds > 0)
			{
				containerProperties.DefaultTimeToLive = _options.Value.DefaultTimeToLiveSeconds;
			}

			var response = await database.CreateContainerIfNotExistsAsync(
				containerProperties,
				ThroughputProperties.CreateManualThroughput(_options.Value.ContainerThroughput),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			_container = response.Container;
		}
		else
		{
			_container = database.GetContainer(_options.Value.EventsContainerName);
		}

		_initialized = true;
	}

	private async Task<CloudAppendResult> AppendWithTransactionAsync(
		string streamId,
		string aggregateId,
		string aggregateType,
		List<IDomainEvent> events,
		long expectedVersion,
		Microsoft.Azure.Cosmos.PartitionKey pk,
		CancellationToken cancellationToken)
	{
		var batch = _container.CreateTransactionalBatch(pk);
		var version = expectedVersion;

		foreach (var evt in events)
		{
			version++;
			var doc = CreateEventDocument(streamId, aggregateId, aggregateType, evt, version);
			_ = batch.CreateItem(doc);
		}

		using var response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
		var totalRu = response.RequestCharge;

		if (!response.IsSuccessStatusCode)
		{
			return CloudAppendResult.CreateFailure(
				$"Transactional batch failed with status {response.StatusCode}",
				totalRu);
		}

		LogEventsAppended(streamId, events.Count, totalRu);
		return CloudAppendResult.CreateSuccess(version, totalRu, response.Headers.Session);
	}

	private async Task<CloudAppendResult> AppendSequentiallyAsync(
		string streamId,
		string aggregateId,
		string aggregateType,
		List<IDomainEvent> events,
		long expectedVersion,
		Microsoft.Azure.Cosmos.PartitionKey pk,
		CancellationToken cancellationToken)
	{
		var version = expectedVersion;
		double totalRu = 0;
		string? sessionToken = null;

		foreach (var evt in events)
		{
			version++;
			var doc = CreateEventDocument(streamId, aggregateId, aggregateType, evt, version);

			var response = await _container.CreateItemAsync(
				doc,
				pk,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			totalRu += response.RequestCharge;
			sessionToken = response.Headers.Session;
		}

		LogEventsAppended(streamId, events.Count, totalRu);
		return CloudAppendResult.CreateSuccess(version, totalRu, sessionToken);
	}
}

/// <summary>
/// Internal document model for Cosmos DB event storage.
/// </summary>
internal sealed class EventDocument
{
	/// <summary>
	/// Gets or sets the document ID.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the stream ID (partition key).
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("streamId")]
	public string StreamId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the unique event ID.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("eventId")]
	public string EventId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the aggregate ID.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("aggregateId")]
	public string AggregateId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the aggregate type.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("aggregateType")]
	public string AggregateType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the event type.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("eventType")]
	public string EventType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the event version.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("version")]
	public long Version { get; set; }

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("timestamp")]
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the serialized event data.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("eventData")]
	public byte[] EventData { get; set; } = [];

	/// <summary>
	/// Gets or sets the serialized metadata.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("metadata")]
	public byte[]? Metadata { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the event has been dispatched.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("isDispatched")]
	public bool IsDispatched { get; set; }

	/// <summary>
	/// Gets or sets the ETag for concurrency control.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("_etag")]
	public string? ETag { get; set; }
}
