// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Observability;
using Excalibur.Data.Firestore;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Observability;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Firestore;

/// <summary>
/// Google Cloud Firestore implementation of the event store.
/// </summary>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Event store implementations inherently couple with many SDK and abstraction types.")]
public sealed partial class FirestoreEventStore : ICloudNativeEventStore, IEventStore, IAsyncDisposable
{
	private readonly FirestoreEventStoreOptions _options;
	private readonly ILogger<FirestoreEventStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private FirestoreDb? _db;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreEventStore" /> class.
	/// </summary>
	/// <param name="options"> The event store options. </param>
	/// <param name="logger"> The logger instance. </param>
	public FirestoreEventStore(
		IOptions<FirestoreEventStoreOptions> options,
		ILogger<FirestoreEventStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreEventStore" /> class with an existing database.
	/// </summary>
	/// <param name="db"> The Firestore database. </param>
	/// <param name="options"> The event store options. </param>
	/// <param name="logger"> The logger instance. </param>
	public FirestoreEventStore(
		FirestoreDb db,
		IOptions<FirestoreEventStoreOptions> options,
		ILogger<FirestoreEventStore> logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_initialized = true;
	}

	/// <inheritdoc />
	public CloudProviderType ProviderType => CloudProviderType.Firestore;

	/// <summary>
	/// Gets the Firestore database instance.
	/// </summary>
	internal FirestoreDb? Database => _db;

	/// <summary>
	/// Initializes the Firestore client.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_initialized)
			{
				return;
			}

			LogInitializing(_options.EventsCollectionName);
			_options.Validate();

			_db = await CreateDatabaseAsync().ConfigureAwait(false);
			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc />
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

		var streamId = BuildStreamId(aggregateType, aggregateId);

		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType);

		try
		{
			var events = new List<CloudStoredEvent>();
			var query = _db.Collection(_options.EventsCollectionName)
				.WhereEqualTo("streamId", streamId)
				.OrderBy("version");

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			foreach (var doc in snapshot.Documents)
			{
				var cloudEvent = ToCloudStoredEvent(doc);
				events.Add(cloudEvent);
			}

			LogLoadedEvents(streamId, events.Count);

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return new CloudEventLoadResult(events, 0, null);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"load");
			_logger.LogError(ex, "Error loading events for stream {StreamId}", streamId);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
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

		var streamId = BuildStreamId(aggregateType, aggregateId);

		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType, fromVersion);

		try
		{
			var events = new List<CloudStoredEvent>();
			var query = _db.Collection(_options.EventsCollectionName)
				.WhereEqualTo("streamId", streamId)
				.WhereGreaterThan("version", fromVersion)
				.OrderBy("version");

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			foreach (var doc in snapshot.Documents)
			{
				var cloudEvent = ToCloudStoredEvent(doc);
				events.Add(cloudEvent);
			}

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return new CloudEventLoadResult(events, 0, null);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"load_from_version");
			_logger.LogError(ex, "Error loading events from version for stream {StreamId}", streamId);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"load_from_version",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task<CloudAppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var operationResult = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var eventsList = events.ToList();
		var correlationId = ExtractCorrelationId(eventsList);
		var messageId = ExtractEventId(eventsList);
		if (eventsList.Count == 0)
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"append",
				operationResult,
				stopwatch.Elapsed);
			return CloudAppendResult.CreateSuccess(expectedVersion, 0);
		}

		var streamId = BuildStreamId(aggregateType, aggregateId);
		LogAppendingEvents(streamId, eventsList.Count);

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventsList.Count, expectedVersion);

		try
		{
			// Use transaction for optimistic concurrency
			var newVersion = expectedVersion;
			var conflictDetected = false;
			var currentActualVersion = expectedVersion;

			await _db.RunTransactionAsync(async transaction =>
			{
				// Check current version
				var versionQuery = _db.Collection(_options.EventsCollectionName)
					.WhereEqualTo("streamId", streamId)
					.OrderByDescending("version")
					.Limit(1);

				var versionSnapshot = await transaction.GetSnapshotAsync(versionQuery, cancellationToken)
					.ConfigureAwait(false);

				var currentVersion = -1L;
				if (versionSnapshot.Count > 0)
				{
					currentVersion = versionSnapshot.Documents[0].GetValue<long>("version");
				}

				currentActualVersion = currentVersion;

				if (currentVersion != expectedVersion)
				{
					conflictDetected = true;
					return;
				}

				// Append events
				var version = expectedVersion;
				foreach (var evt in eventsList)
				{
					version++;
					var eventTypeName = EventTypeNameHelper.GetEventTypeName(evt.GetType());
					var docId = $"{streamId}:{version}";
					var docRef = _db.Collection(_options.EventsCollectionName).Document(docId);

					var data = new Dictionary<string, object>
					{
						["eventId"] = evt.EventId.ToString(),
						["streamId"] = streamId,
						["aggregateId"] = aggregateId,
						["aggregateType"] = aggregateType,
						["eventType"] = eventTypeName,
						["version"] = version,
						["timestamp"] = evt.OccurredAt.ToString("O"),
						["eventData"] = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(evt)),
						["isDispatched"] = false
					};

					if (evt.Metadata != null)
					{
						data["metadata"] = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(evt.Metadata));
					}

					transaction.Create(docRef, data);
				}

				newVersion = version;
			}, cancellationToken: cancellationToken).ConfigureAwait(false);

			if (conflictDetected)
			{
				LogConcurrencyConflict(streamId, $"Expected version {expectedVersion}");
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				operationResult = WriteStoreTelemetry.Results.Conflict;
				return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, currentActualVersion, 0);
			}

			_ = (activity?.SetTag(EventSourcingTags.Version, newVersion));
			activity.SetOperationResult(EventSourcingTagValues.Success);
			operationResult = WriteStoreTelemetry.Results.Success;
			return CloudAppendResult.CreateSuccess(newVersion, 0);
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
		{
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
			LogConcurrencyConflict(streamId, "Document already exists");
			operationResult = WriteStoreTelemetry.Results.Conflict;
			var currentVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, partitionKey, cancellationToken)
				.ConfigureAwait(false);
			return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion, 0);
		}
		catch (Exception ex)
		{
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			operationResult = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"append",
				messageId,
				correlationId);
			_logger.LogError(ex, "Error appending events to stream {StreamId}", streamId);
			return CloudAppendResult.CreateFailure(ex.Message, 0);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"append",
				operationResult,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task<IChangeFeedSubscription<CloudStoredEvent>> SubscribeToChangesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var subscription = new FirestoreEventStoreListenerSubscription(
			_db,
			_options,
			_logger);

		return subscription;
	}

	/// <inheritdoc />
	public async Task<long> GetCurrentVersionAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var streamId = BuildStreamId(aggregateType, aggregateId);

		var query = _db.Collection(_options.EventsCollectionName)
			.WhereEqualTo("streamId", streamId)
			.OrderByDescending("version")
			.Limit(1);

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (snapshot.Count == 0)
		{
			return -1;
		}

		return snapshot.Documents[0].GetValue<long>("version");
	}

	#region IEventStore Explicit Implementation

	/// <inheritdoc />
	async ValueTask<IReadOnlyList<StoredEvent>> IEventStore.LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var partitionKey = new PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await LoadAsync(aggregateId, aggregateType, partitionKey, null, cancellationToken)
			.ConfigureAwait(false);
		return result.Events.Select(ToStoredEvent).ToList();
	}

	/// <inheritdoc />
	async ValueTask<IReadOnlyList<StoredEvent>> IEventStore.LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var partitionKey = new PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await LoadFromVersionAsync(aggregateId, aggregateType, partitionKey, fromVersion, null, cancellationToken)
			.ConfigureAwait(false);
		return result.Events.Select(ToStoredEvent).ToList();
	}

	/// <inheritdoc />
	async ValueTask<AppendResult> IEventStore.AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var partitionKey = new PartitionKey(BuildStreamId(aggregateType, aggregateId));
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

	/// <inheritdoc />
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
			var query = _db.Collection(_options.EventsCollectionName)
				.WhereEqualTo("isDispatched", false)
				.OrderBy("timestamp")
				.Limit(batchSize);

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
			var events = new List<StoredEvent>();

			foreach (var doc in snapshot.Documents)
			{
				events.Add(ToStoredEvent(ToCloudStoredEvent(doc)));
			}

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return events;
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"get_undispatched",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask MarkEventAsDispatchedAsync(
		string eventId,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		using var activity = EventSourcingActivitySource.StartMarkDispatchedActivity(eventId);

		try
		{
			await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

			// Query for the event by eventId
			var query = _db.Collection(_options.EventsCollectionName)
				.WhereEqualTo("eventId", eventId)
				.Limit(1);

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (snapshot.Count > 0)
			{
				var docRef = snapshot.Documents[0].Reference;
				_ = await docRef.UpdateAsync("isDispatched", true, cancellationToken: cancellationToken)
					.ConfigureAwait(false);
			}

			activity.SetOperationResult(EventSourcingTagValues.Success);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"mark_dispatched",
				result,
				stopwatch.Elapsed);
		}
	}

	#endregion IEventStore Explicit Implementation

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_initLock.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private static string BuildStreamId(string aggregateType, string aggregateId)
		=> $"{aggregateType}:{aggregateId}";

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

	private static CloudStoredEvent ToCloudStoredEvent(DocumentSnapshot doc)
	{
		var streamId = doc.GetValue<string>("streamId");

		return new CloudStoredEvent
		{
			EventId = doc.GetValue<string>("eventId"),
			AggregateId = doc.GetValue<string>("aggregateId"),
			AggregateType = doc.GetValue<string>("aggregateType"),
			EventType = doc.GetValue<string>("eventType"),
			Version = doc.GetValue<long>("version"),
			Timestamp = DateTimeOffset.Parse(doc.GetValue<string>("timestamp"), CultureInfo.InvariantCulture),
			EventData = Convert.FromBase64String(doc.GetValue<string>("eventData")),
			Metadata = doc.ContainsField("metadata") && doc.GetValue<string?>("metadata") != null
				? Convert.FromBase64String(doc.GetValue<string>("metadata"))
				: null,
			PartitionKeyValue = streamId,
			DocumentId = doc.Id,
			IsDispatched = doc.ContainsField("isDispatched") && doc.GetValue<bool>("isDispatched")
		};
	}

	private async Task<FirestoreDb> CreateDatabaseAsync()
	{
		if (!string.IsNullOrWhiteSpace(_options.EmulatorHost))
		{
			_ = FirestoreEmulatorHelper.TryConfigureEmulatorHost(_options.EmulatorHost);
			return await FirestoreDb.CreateAsync(_options.ProjectId ?? "test-project").ConfigureAwait(false);
		}

		FirestoreDbBuilder builder;

		if (!string.IsNullOrWhiteSpace(_options.CredentialsJson))
		{
			builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId, JsonCredentials = _options.CredentialsJson };
		}
		else if (!string.IsNullOrWhiteSpace(_options.CredentialsPath))
		{
			builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId, CredentialsPath = _options.CredentialsPath };
		}
		else
		{
			builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId };
		}

		return await builder.BuildAsync().ConfigureAwait(false);
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}

		if (_db == null)
		{
			throw new InvalidOperationException("Firestore event store has not been initialized.");
		}
	}
}
