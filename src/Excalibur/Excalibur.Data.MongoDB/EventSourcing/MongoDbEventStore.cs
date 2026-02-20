// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Data.Abstractions.Observability;
using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.EventSourcing;

/// <summary>
/// MongoDB implementation of <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides atomic event appends with optimistic concurrency control using MongoDB's UNIQUE index.
/// A UNIQUE compound index on (streamId, aggregateType, version) enforces version uniqueness.
/// </para>
/// <para>
/// When a version conflict occurs, MongoDB returns error code 11000 (duplicate key),
/// which is caught and translated to a concurrency conflict result.
/// </para>
/// <para>
/// Global ordering is provided via an atomic counter document using FindOneAndUpdate.
/// </para>
/// <para>
/// Supports pluggable serialization via <see cref="IPayloadSerializer"/> for event payloads,
/// with backward compatibility for existing JSON-serialized events.
/// </para>
/// </remarks>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Event store implementations inherently couple with many SDK and abstraction types.")]
public sealed partial class MongoDbEventStore : IEventStore, IAsyncDisposable
{
	// MongoDB error code for duplicate key (unique constraint violation)
	private const int DuplicateKeyErrorCode = 11000;

	// Counter document ID for global sequence
	private const string GlobalSequenceCounterId = "global_sequence";

	// Format markers for envelope detection
	private const byte EnvelopeFormatMarker = 0x01;

	private readonly MongoDbEventStoreOptions _options;
	private readonly ILogger<MongoDbEventStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly IInternalSerializer? _internalSerializer;
	private readonly IPayloadSerializer? _payloadSerializer;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<MongoDbEventDocument>? _eventsCollection;
	private IMongoCollection<MongoDbCounterDocument>? _countersCollection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbEventStore"/> class.
	/// </summary>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbEventStore(
		IOptions<MongoDbEventStoreOptions> options,
		ILogger<MongoDbEventStore> logger)
		: this(options, logger, null, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbEventStore"/> class with optional serializers.
	/// </summary>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="internalSerializer">Optional internal serializer for envelope support.</param>
	/// <param name="payloadSerializer">Optional pluggable serializer for event payloads.</param>
	public MongoDbEventStore(
		IOptions<MongoDbEventStoreOptions> options,
		ILogger<MongoDbEventStore> logger,
		IInternalSerializer? internalSerializer,
		IPayloadSerializer? payloadSerializer)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		_internalSerializer = internalSerializer;
		_payloadSerializer = payloadSerializer;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbEventStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="internalSerializer">Optional internal serializer for envelope support.</param>
	/// <param name="payloadSerializer">Optional pluggable serializer for event payloads.</param>
	public MongoDbEventStore(
		IMongoClient client,
		IOptions<MongoDbEventStoreOptions> options,
		ILogger<MongoDbEventStore> logger,
		IInternalSerializer? internalSerializer = null,
		IPayloadSerializer? payloadSerializer = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		_internalSerializer = internalSerializer;
		_payloadSerializer = payloadSerializer;
		_database = client.GetDatabase(_options.DatabaseName);
		_eventsCollection = _database.GetCollection<MongoDbEventDocument>(_options.CollectionName);
		_countersCollection = _database.GetCollection<MongoDbCounterDocument>(_options.CounterCollectionName);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		return await LoadAsync(aggregateId, aggregateType, -1, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType, fromVersion);

		try
		{
			await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

			var filterBuilder = Builders<MongoDbEventDocument>.Filter;
			var filter = filterBuilder.And(
				filterBuilder.Eq(d => d.StreamId, aggregateId),
				filterBuilder.Eq(d => d.AggregateType, aggregateType));

			if (fromVersion >= 0)
			{
				filter = filterBuilder.And(filter, filterBuilder.Gt(d => d.Version, fromVersion));
			}

			var sort = Builders<MongoDbEventDocument>.Sort.Ascending(d => d.Version);

			var documents = await _eventsCollection
				.Find(filter)
				.Sort(sort)
				.ToListAsync(cancellationToken)
				.ConfigureAwait(false);

			var loadedEvents = documents.Select(d => d.ToStoredEvent()).ToList();

			_ = (activity?.SetTag(EventSourcingTags.EventCount, loadedEvents.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return loadedEvents;
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
				WriteStoreTelemetry.Providers.MongoDb,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var eventList = events.ToList();
		var correlationId = ExtractCorrelationId(eventList);
		var messageId = ExtractEventId(eventList);
		if (eventList.Count == 0)
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.MongoDb,
				"append",
				result,
				stopwatch.Elapsed);
			return AppendResult.CreateSuccess(expectedVersion, 0);
		}

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventList.Count, expectedVersion);

		try
		{
			await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

			// Check current version first for optimistic concurrency
			var currentVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			if (currentVersion != expectedVersion)
			{
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				result = WriteStoreTelemetry.Results.Conflict;
				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion);
			}

			// Build documents with sequential versions
			var documents = new List<MongoDbEventDocument>();
			long firstPosition = 0;
			var version = currentVersion;

			foreach (var @event in eventList)
			{
				version++;
				var globalSequence = await GetNextGlobalSequenceAsync(cancellationToken).ConfigureAwait(false);
				var eventTypeName = EventTypeNameHelper.GetEventTypeName(@event.GetType());

				if (firstPosition == 0)
				{
					firstPosition = globalSequence;
				}

				var eventData = SerializeEventWithEnvelopeSupport(@event, aggregateId, aggregateType, version);
				var metadata = @event.Metadata != null ? SerializeMetadata(@event.Metadata) : null;

				documents.Add(new MongoDbEventDocument
				{
					EventId = @event.EventId,
					StreamId = aggregateId,
					AggregateType = aggregateType,
					EventType = eventTypeName,
					Payload = eventData,
					Metadata = metadata,
					Version = version,
					OccurredAt = @event.OccurredAt,
					IsDispatched = false,
					GlobalSequence = globalSequence
				});
			}

			// Use ordered insert - stops at first failure
			await _eventsCollection.InsertManyAsync(
				documents,
				new InsertManyOptions { IsOrdered = true },
				cancellationToken).ConfigureAwait(false);

			LogEventsAppended(eventList.Count, aggregateType, aggregateId, version);

			_ = (activity?.SetTag(EventSourcingTags.Version, version));
			activity.SetOperationResult(EventSourcingTagValues.Success);
			return AppendResult.CreateSuccess(version, firstPosition);
		}
		catch (MongoBulkWriteException<MongoDbEventDocument> ex)
			when (ex.WriteErrors.Any(e => e.Code == DuplicateKeyErrorCode))
		{
			// Duplicate key error - version conflict detected
			LogConcurrencyConflict(aggregateType, aggregateId, expectedVersion);

			// Re-read current version to report accurate conflict
			var actualVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
			result = WriteStoreTelemetry.Results.Conflict;
			return AppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion);
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Code == DuplicateKeyErrorCode)
		{
			// Duplicate key error - version conflict detected (single document case)
			LogConcurrencyConflict(aggregateType, aggregateId, expectedVersion);

			var actualVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
			result = WriteStoreTelemetry.Results.Conflict;
			return AppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.MongoDb,
				"append",
				messageId,
				correlationId);
			LogAppendError(aggregateType, aggregateId, ex);
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			return AppendResult.CreateFailure(GetFullExceptionMessage(ex));
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.MongoDb,
				"append",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		using var activity = EventSourcingActivitySource.StartGetUndispatchedActivity(batchSize);

		try
		{
			await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

			var filter = Builders<MongoDbEventDocument>.Filter.Eq(d => d.IsDispatched, false);
			var sort = Builders<MongoDbEventDocument>.Sort.Ascending(d => d.GlobalSequence);

			var documents = await _eventsCollection
				.Find(filter)
				.Sort(sort)
				.Limit(batchSize)
				.ToListAsync(cancellationToken)
				.ConfigureAwait(false);

			var undispatchedEvents = documents.Select(d => d.ToStoredEvent()).ToList();

			_ = (activity?.SetTag(EventSourcingTags.EventCount, undispatchedEvents.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return undispatchedEvents;
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
				WriteStoreTelemetry.Providers.MongoDb,
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
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		using var activity = EventSourcingActivitySource.StartMarkDispatchedActivity(eventId);

		try
		{
			await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

			var filter = Builders<MongoDbEventDocument>.Filter.Eq(d => d.EventId, eventId);
			var update = Builders<MongoDbEventDocument>.Update
				.Set(d => d.IsDispatched, true)
				.Set(d => d.DispatchedAt, DateTimeOffset.UtcNow);

			_ = await _eventsCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);

			LogEventDispatched(eventId);
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
				WriteStoreTelemetry.Providers.MongoDb,
				"mark_dispatched",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		// MongoDB client doesn't implement IDisposable - it manages connections internally
		return ValueTask.CompletedTask;
	}

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

	private static string GetFullExceptionMessage(Exception ex)
	{
		var messages = new List<string>();
		var current = ex;
		while (current != null)
		{
			messages.Add(current.Message);
			current = current.InnerException;
		}

		return string.Join(" -> ", messages);
	}

	private async Task<long> GetCurrentVersionAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var filterBuilder = Builders<MongoDbEventDocument>.Filter;
		var filter = filterBuilder.And(
			filterBuilder.Eq(d => d.StreamId, aggregateId),
			filterBuilder.Eq(d => d.AggregateType, aggregateType));

		var sort = Builders<MongoDbEventDocument>.Sort.Descending(d => d.Version);

		var latestEvent = await _eventsCollection
			.Find(filter)
			.Sort(sort)
			.Limit(1)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		return latestEvent?.Version ?? -1;
	}

	private async Task<long> GetNextGlobalSequenceAsync(CancellationToken cancellationToken)
	{
		var filter = Builders<MongoDbCounterDocument>.Filter.Eq(d => d.Id, GlobalSequenceCounterId);
		var update = Builders<MongoDbCounterDocument>.Update.Inc(d => d.Sequence, 1);
		var options = new FindOneAndUpdateOptions<MongoDbCounterDocument> { ReturnDocument = ReturnDocument.After, IsUpsert = true };

		var result = await _countersCollection.FindOneAndUpdateAsync(
			filter,
			update,
			options,
			cancellationToken).ConfigureAwait(false);

		return result.Sequence;
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		if (_client == null)
		{
			var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
			settings.ServerSelectionTimeout = TimeSpan.FromSeconds(_options.ServerSelectionTimeoutSeconds);
			settings.ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds);
			settings.MaxConnectionPoolSize = _options.MaxPoolSize;

			if (_options.UseSsl)
			{
				settings.UseTls = true;
			}

			_client = new MongoClient(settings);
			_database = _client.GetDatabase(_options.DatabaseName);
			_eventsCollection = _database.GetCollection<MongoDbEventDocument>(_options.CollectionName);
			_countersCollection = _database.GetCollection<MongoDbCounterDocument>(_options.CounterCollectionName);
		}

		// Create indexes
		var indexBuilder = Builders<MongoDbEventDocument>.IndexKeys;

		// UNIQUE compound index for optimistic concurrency
		// This enforces that (streamId, aggregateType, version) is unique
		var uniqueVersionIndex = new CreateIndexModel<MongoDbEventDocument>(
			indexBuilder.Combine(
				indexBuilder.Ascending(d => d.StreamId),
				indexBuilder.Ascending(d => d.AggregateType),
				indexBuilder.Ascending(d => d.Version)),
			new CreateIndexOptions { Unique = true, Name = "ix_stream_version_unique" });

		// Index on globalSequence for ordering
		var globalSequenceIndex = new CreateIndexModel<MongoDbEventDocument>(
			indexBuilder.Ascending(d => d.GlobalSequence),
			new CreateIndexOptions { Name = "ix_global_sequence" });

		// Partial index for undispatched events
		var undispatchedIndex = new CreateIndexModel<MongoDbEventDocument>(
			indexBuilder.Ascending(d => d.IsDispatched),
			new CreateIndexOptions<MongoDbEventDocument>
			{
				Name = "ix_undispatched",
				PartialFilterExpression = Builders<MongoDbEventDocument>.Filter.Eq(d => d.IsDispatched, false)
			});

		// Index on eventId for dispatch marking
		var eventIdIndex = new CreateIndexModel<MongoDbEventDocument>(
			indexBuilder.Ascending(d => d.EventId),
			new CreateIndexOptions { Name = "ix_event_id" });

		_ = await _eventsCollection.Indexes.CreateManyAsync(
			[uniqueVersionIndex, globalSequenceIndex, undispatchedIndex, eventIdIndex],
			cancellationToken).ConfigureAwait(false);

		_initialized = true;
	}

	private byte[] SerializeEvent(IDomainEvent @event)
	{
		if (_payloadSerializer != null)
		{
			return _payloadSerializer.Serialize(@event);
		}

		// Fallback to System.Text.Json for backward compatibility
		return JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _jsonOptions);
	}

	private byte[] SerializeMetadata(IDictionary<string, object> metadata) =>
		JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);

	private byte[] SerializeEventWithEnvelopeSupport(
		IDomainEvent @event,
		string aggregateId,
		string aggregateType,
		long version)
	{
		var eventTypeName = EventTypeNameHelper.GetEventTypeName(@event.GetType());

		if (_internalSerializer is null)
		{
			return SerializeEvent(@event);
		}

		// Create envelope with event data
		var eventBytes = SerializeEvent(@event);

		var envelope = new EventEnvelope
		{
			EventId = Guid.TryParse(@event.EventId, out var guid) ? guid : Guid.NewGuid(),
			AggregateId = Guid.TryParse(aggregateId, out var aggGuid) ? aggGuid : Guid.NewGuid(),
			AggregateType = aggregateType,
			EventType = eventTypeName,
			Version = version,
			Payload = eventBytes,
			OccurredAt = @event.OccurredAt,
			Metadata = @event.Metadata?.ToDictionary(
				kvp => kvp.Key,
				kvp => kvp.Value?.ToString() ?? string.Empty,
				StringComparer.OrdinalIgnoreCase),
			SchemaVersion = 1,
		};

		var envelopeData = _internalSerializer.Serialize(envelope);

		// Prepend format marker
		var result = new byte[envelopeData.Length + 1];
		result[0] = EnvelopeFormatMarker;
		envelopeData.CopyTo(result, 1);
		return result;
	}

	[LoggerMessage(DataMongoDbEventId.EventsAppended, LogLevel.Debug,
		"Appended {Count} events to {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogEventsAppended(int count, string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataMongoDbEventId.ConcurrencyConflict, LogLevel.Warning,
		"Concurrency conflict detected for {AggregateType}/{AggregateId} at expected version {ExpectedVersion}")]
	private partial void LogConcurrencyConflict(string aggregateType, string aggregateId, long expectedVersion);

	[LoggerMessage(DataMongoDbEventId.AppendError, LogLevel.Error, "Failed to append events to {AggregateType}/{AggregateId}")]
	private partial void LogAppendError(string aggregateType, string aggregateId, Exception ex);

	[LoggerMessage(DataMongoDbEventId.EventDispatched, LogLevel.Debug, "Marked event {EventId} as dispatched")]
	private partial void LogEventDispatched(string eventId);
}
