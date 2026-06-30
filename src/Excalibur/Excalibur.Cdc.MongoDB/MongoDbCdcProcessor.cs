// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Diagnostics;
using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// MongoDB CDC processor using Change Streams.
/// </summary>
public sealed partial class MongoDbCdcProcessor : IMongoDbCdcProcessor
{
	private readonly MongoDbCdcOptions _options;
	private readonly IMongoDbCdcStateStore _stateStore;
	private readonly ILogger<MongoDbCdcProcessor> _logger;
	private readonly IMongoClient _client;

	private MongoDbCdcPosition _currentPosition;
	private MongoDbCdcPosition _confirmedPosition;
	private volatile bool _disposed;

	// 14z4ao: optional fatal-handoff. A fatal (non-retryable) error stops the processor loudly instead of
	// an infinite silent reconnect loop (ADR-338). _onFatalError receives the in-flight event for a
	// per-event fatal, or null for a connection/stream-level fatal.
	private readonly CdcFatalErrorHandler<MongoDbDataChangeEvent>? _onFatalError;
	private readonly IMessageFailureClassifier? _failureClassifier;
	private MongoDbDataChangeEvent? _inFlightEvent;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbCdcProcessor"/> class.
	/// </summary>
	/// <param name="client">The MongoDB client from DI.</param>
	/// <param name="options">The CDC options.</param>
	/// <param name="stateStore">The state store for position tracking.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="fatalErrorOptions">
	/// Optional fatal-error handling. When omitted (or its handler is <see langword="null"/>), a fatal
	/// error rethrows and stops the processor (fail-loud — never an infinite silent reconnect loop).
	/// </param>
	/// <param name="failureClassifier">
	/// Optional shared classifier deciding whether a processing error is fatal (non-retryable) or
	/// transient. When omitted, a conservative built-in fallback is used.
	/// </param>
	public MongoDbCdcProcessor(
		IMongoClient client,
		IOptions<MongoDbCdcOptions> options,
		IMongoDbCdcStateStore stateStore,
		ILogger<MongoDbCdcProcessor> logger,
		IOptions<CdcFatalErrorOptions<MongoDbDataChangeEvent>>? fatalErrorOptions = null,
		IMessageFailureClassifier? failureClassifier = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(stateStore);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();

		_client = client;
		_stateStore = stateStore;
		_logger = logger;
		_onFatalError = fatalErrorOptions?.Value.OnFatalError;
		_failureClassifier = failureClassifier;
		_currentPosition = MongoDbCdcPosition.Start;
		_confirmedPosition = MongoDbCdcPosition.Start;
	}

	/// <inheritdoc/>
	public async Task StartAsync(
		Func<MongoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(eventHandler);

		LogStarting(_options.ProcessorId);

		// Load last confirmed position
		_confirmedPosition = await _stateStore
			.GetLastPositionAsync(_options.ProcessorId, cancellationToken)
			.ConfigureAwait(false);

		_currentPosition = _confirmedPosition;

		LogResuming(_confirmedPosition.TokenString ?? "<start>");

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await ProcessChangesAsync(eventHandler, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				LogStopping();
				throw;
			}
			catch (Exception ex)
			{
				// pxhqri: delegate the fatal-vs-transient decision to the single shared guard
				// (CdcFatalGuard.Decide) — the same unit the regression lock binds — instead of an inline
				// `catch when IsFatal` filter. The durable checkpoint is advanced ONLY on the success path
				// (SavePositionAsync inside ProcessChangesAsync); this catch never advances, and the stream
				// unwinds before that confirm on a fault, so a fault (fatal or transient) never advances the
				// checkpoint past the failing change (decision.AdvanceCheckpoint is false on every fault).
				// 14z4ao behavior is byte-preserved.
				var decision = CdcFatalGuard.Decide(ex, _failureClassifier);

				if (decision.Stop)
				{
					// Fatal (non-retryable) — stop loud, never an infinite silent reconnect.
					LogFatalError(ex);

					if (_onFatalError is not null)
					{
						// In-flight event for a per-event fatal; null for a stream/connection-level fatal.
						await _onFatalError(ex, _inFlightEvent).ConfigureAwait(false);
						return; // handler took over → terminal; do not reconnect.
					}

					throw; // default: fail-loud — propagate and stop.
				}

				// Transient (non-fatal: decision.Stop == false) — reconnect and retry from the un-advanced checkpoint.
				LogError(ex);
				_inFlightEvent = null;

				// Wait before reconnecting
				await Task.Delay(_options.ReconnectInterval, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc/>
	public async Task<int> ProcessBatchAsync(
		Func<MongoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(eventHandler);

		using var pollActivity = CdcActivitySource.StartPollActivity("MongoDB");

		// Load last confirmed position
		_confirmedPosition = await _stateStore
			.GetLastPositionAsync(_options.ProcessorId, cancellationToken)
			.ConfigureAwait(false);

		_currentPosition = _confirmedPosition;

		var count = 0;
		var changeStreamOptions = BuildChangeStreamOptions();

		using var cursor = await WatchAsync(changeStreamOptions, cancellationToken).ConfigureAwait(false);

		// Use a timeout for batch processing
		using var batchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		batchCts.CancelAfter(_options.ChangeStream.MaxAwaitTime * 2);

		try
		{
			while (count < _options.BatchSize &&
				   await cursor.MoveNextAsync(batchCts.Token).ConfigureAwait(false))
			{
				foreach (var change in cursor.Current)
				{
					var changeEvent = ConvertToChangeEvent(change);
					if (changeEvent is not null && ShouldProcessChange(changeEvent))
					{
						await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);
						count++;
					}

					// Update position
					_currentPosition = new MongoDbCdcPosition(change.ResumeToken);

					if (count >= _options.BatchSize)
					{
						break;
					}
				}
			}
		}
		catch (OperationCanceledException) when (batchCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			// Batch timeout - normal behavior
		}

		// Save position if we processed anything
		if (count > 0)
		{
			using var batchActivity = CdcActivitySource.StartProcessBatchActivity("MongoDB", count);

			await _stateStore
				.SavePositionAsync(_options.ProcessorId, _currentPosition, cancellationToken)
				.ConfigureAwait(false);

			_confirmedPosition = _currentPosition;
		}

		return count;
	}

	/// <inheritdoc/>
	public Task<MongoDbCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return Task.FromResult(_currentPosition);
	}

	/// <inheritdoc/>
	public async Task ConfirmPositionAsync(
		MongoDbCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await _stateStore
			.SavePositionAsync(_options.ProcessorId, position, cancellationToken)
			.ConfigureAwait(false);

		_confirmedPosition = position;

		LogConfirmed(position.TokenString ?? "<unknown>");
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Client is injected via DI — do not dispose; the DI container owns its lifetime.
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;

		// Client is injected via DI — do not dispose; the DI container owns its lifetime.
		return ValueTask.CompletedTask;
	}

	private static string NormalizeOperationType(string operationType)
	{
		// MongoDB operation types are lowercase in change streams
		// This mapping ensures user-provided values are correctly normalized
		return operationType.ToUpperInvariant() switch
		{
			"INSERT" => "insert",
			"UPDATE" => "update",
			"REPLACE" => "replace",
			"DELETE" => "delete",
			"DROP" => "drop",
			"DROPDATABASE" => "dropDatabase",
			"RENAME" => "rename",
			"INVALIDATE" => "invalidate",
			_ => operationType,
		};
	}

	private static MongoDbDataChangeEvent? ConvertToChangeEvent(ChangeStreamDocument<BsonDocument> change)
	{
		var position = new MongoDbCdcPosition(change.ResumeToken);
		var databaseName = change.DatabaseNamespace?.DatabaseName ?? string.Empty;
		var collectionName = change.CollectionNamespace?.CollectionName ?? string.Empty;
		var clusterTime = change.ClusterTime;
		var wallTime = change.WallTime;

		return change.OperationType switch
		{
			ChangeStreamOperationType.Insert => MongoDbDataChangeEvent.CreateInsert(
				position,
				databaseName,
				collectionName,
				change.DocumentKey,
				change.FullDocument,
				clusterTime,
				wallTime),

			ChangeStreamOperationType.Update => MongoDbDataChangeEvent.CreateUpdate(
				position,
				databaseName,
				collectionName,
				change.DocumentKey,
				change.FullDocument,
				change.FullDocumentBeforeChange,
				ConvertUpdateDescription(change.UpdateDescription),
				clusterTime,
				wallTime),

			ChangeStreamOperationType.Replace => MongoDbDataChangeEvent.CreateReplace(
				position,
				databaseName,
				collectionName,
				change.DocumentKey,
				change.FullDocument,
				change.FullDocumentBeforeChange,
				clusterTime,
				wallTime),

			ChangeStreamOperationType.Delete => MongoDbDataChangeEvent.CreateDelete(
				position,
				databaseName,
				collectionName,
				change.DocumentKey,
				change.FullDocumentBeforeChange,
				clusterTime,
				wallTime),

			ChangeStreamOperationType.Drop => MongoDbDataChangeEvent.CreateDrop(
				position,
				databaseName,
				collectionName,
				clusterTime,
				wallTime),

			ChangeStreamOperationType.Invalidate => MongoDbDataChangeEvent.CreateInvalidate(
				position,
				clusterTime,
				wallTime),

			_ => null,
		};
	}

	private static MongoDbUpdateDescription? ConvertUpdateDescription(ChangeStreamUpdateDescription? description)
	{
		if (description is null)
		{
			return null;
		}

		return new MongoDbUpdateDescription
		{
			UpdatedFields = description.UpdatedFields,
			RemovedFields = description.RemovedFields?.ToList() ?? [],
			TruncatedArrays = ConvertTruncatedArrays(description.TruncatedArrays),
		};
	}

	private static IReadOnlyList<MongoDbArrayTruncation> ConvertTruncatedArrays(BsonArray? truncatedArrays)
	{
		if (truncatedArrays is null || truncatedArrays.Count == 0)
		{
			return [];
		}

		var result = new List<MongoDbArrayTruncation>();

		foreach (var item in truncatedArrays)
		{
			if (item is BsonDocument doc)
			{
				result.Add(new MongoDbArrayTruncation
				{
					Field = doc.GetValue("field", string.Empty).AsString,
					NewSize = doc.GetValue("newSize", 0).AsInt32,
				});
			}
		}

		return result;
	}

	private ChangeStreamOptions BuildChangeStreamOptions()
	{
		var options = new ChangeStreamOptions { BatchSize = _options.BatchSize, MaxAwaitTime = _options.ChangeStream.MaxAwaitTime, };

		if (_confirmedPosition.IsValid)
		{
			options.ResumeAfter = _confirmedPosition.ResumeToken;
		}

		if (_options.ChangeStream.FullDocument)
		{
			options.FullDocument = ChangeStreamFullDocumentOption.UpdateLookup;
		}

		if (_options.ChangeStream.FullDocumentBeforeChange)
		{
			options.FullDocumentBeforeChange = ChangeStreamFullDocumentBeforeChangeOption.WhenAvailable;
		}

		return options;
	}

	private async Task<IAsyncCursor<ChangeStreamDocument<BsonDocument>>> WatchAsync(
		ChangeStreamOptions options,
		CancellationToken cancellationToken)
	{
		// Build pipeline for filtering operation types
		var pipeline = BuildPipeline();

		// Watch at appropriate level based on configuration
		if (!string.IsNullOrEmpty(_options.DatabaseName))
		{
			var database = _client.GetDatabase(_options.DatabaseName);

			if (_options.CollectionNames.Length == 1)
			{
				// Watch single collection
				var collection = database.GetCollection<BsonDocument>(_options.CollectionNames[0]);
				return await collection
					.WatchAsync(pipeline, options, cancellationToken)
					.ConfigureAwait(false);
			}

			// Watch entire database
			return await database
				.WatchAsync(pipeline, options, cancellationToken)
				.ConfigureAwait(false);
		}

		// Watch entire cluster
		return await _client
			.WatchAsync(pipeline, options, cancellationToken)
			.ConfigureAwait(false);
	}

	private PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>> BuildPipeline()
	{
		var stages = new List<IPipelineStageDefinition>();

		// Filter by operation types if specified
		if (_options.ChangeStream.OperationTypes.Length > 0)
		{
			// MongoDB change stream operation types are lowercase (insert, update, replace, delete, etc.)
			var operationTypes = new BsonArray(_options.ChangeStream.OperationTypes.Select(NormalizeOperationType));
			var matchStage = PipelineStageDefinitionBuilder.Match(
				Builders<ChangeStreamDocument<BsonDocument>>.Filter.In("operationType", operationTypes));
			stages.Add(matchStage);
		}

		// Filter by collection names if watching a database
		if (!string.IsNullOrEmpty(_options.DatabaseName) && _options.CollectionNames.Length > 1)
		{
			var collectionNames = new BsonArray(_options.CollectionNames);
			var matchStage = PipelineStageDefinitionBuilder.Match(
				Builders<ChangeStreamDocument<BsonDocument>>.Filter.In("ns.coll", collectionNames));
			stages.Add(matchStage);
		}

		if (stages.Count == 0)
		{
			// Return an EMPTY (non-null) pipeline so the driver's WatchAsync(pipeline, options, ct) never
			// receives null. A null pipeline throws ArgumentNullException on EVERY real change stream with
			// the default config (no operation-type filter, single/zero collection) — the mock-hidden bug
			// 6idsbx. An empty pipeline watches all change events unfiltered, which is the intended default.
			return new EmptyPipelineDefinition<ChangeStreamDocument<BsonDocument>>();
		}

		return PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>
			.Create(stages);
	}

	private async Task ProcessChangesAsync(
		Func<MongoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		using var pollActivity = CdcActivitySource.StartPollActivity("MongoDB");

		var options = BuildChangeStreamOptions();

		using var cursor = await WatchAsync(options, cancellationToken).ConfigureAwait(false);

		LogConnected();

		var count = 0;

		while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (var change in cursor.Current)
			{
				var changeEvent = ConvertToChangeEvent(change);

				if (changeEvent is not null)
				{
					// Handle invalidation
					if (changeEvent.ChangeType == MongoDbDataChangeType.Invalidate)
					{
						LogInvalidate();

						// Save position and break to restart the stream
						await _stateStore
							.SavePositionAsync(_options.ProcessorId, _currentPosition, cancellationToken)
							.ConfigureAwait(false);

						return;
					}

					if (ShouldProcessChange(changeEvent))
					{
						// Track the in-flight event so a fatal raised by the handler is attributed to it and
						// the fatal path unwinds before any durable SavePositionAsync (checkpoint not advanced).
						_inFlightEvent = changeEvent;
						await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);
						_inFlightEvent = null;
						count++;

						LogProcessed(changeEvent.ChangeType, changeEvent.FullNamespace);
					}
				}

				// Update position
				_currentPosition = new MongoDbCdcPosition(change.ResumeToken);
			}
		}

		if (count > 0)
		{
			using var batchActivity = CdcActivitySource.StartProcessBatchActivity("MongoDB", count);
		}
	}

	private bool ShouldProcessChange(MongoDbDataChangeEvent changeEvent)
	{
		// If no collections configured, process all
		if (_options.CollectionNames.Length == 0)
		{
			return true;
		}

		// For invalidate events, always process
		if (changeEvent.ChangeType == MongoDbDataChangeType.Invalidate)
		{
			return true;
		}

		return _options.CollectionNames.Any(c =>
			c.Equals(changeEvent.CollectionName, StringComparison.OrdinalIgnoreCase) ||
			c.Equals(changeEvent.FullNamespace, StringComparison.OrdinalIgnoreCase));
	}

	[LoggerMessage(DataMongoDbEventId.CdcStarting, LogLevel.Information, "Starting MongoDB CDC processor '{ProcessorId}'")]
	private partial void LogStarting(string processorId);

	[LoggerMessage(DataMongoDbEventId.CdcResumingFromToken, LogLevel.Information, "Resuming from token {Token}")]
	private partial void LogResuming(string token);

	[LoggerMessage(DataMongoDbEventId.CdcStreamWatching, LogLevel.Information, "Connected to MongoDB change stream")]
	private partial void LogConnected();

	[LoggerMessage(DataMongoDbEventId.CdcEventProcessed, LogLevel.Debug, "Processed {ChangeType} on {Namespace}")]
	private partial void LogProcessed(MongoDbDataChangeType changeType, string @namespace);

	[LoggerMessage(DataMongoDbEventId.CdcPositionConfirmed, LogLevel.Debug, "Confirmed position {Token}")]
	private partial void LogConfirmed(string token);

	[LoggerMessage(DataMongoDbEventId.CdcStreamInvalidated, LogLevel.Warning, "Received invalidate event, restarting change stream")]
	private partial void LogInvalidate();

	[LoggerMessage(DataMongoDbEventId.CdcStopping, LogLevel.Information, "Stopping MongoDB CDC processor")]
	private partial void LogStopping();

	[LoggerMessage(DataMongoDbEventId.CdcProcessingError, LogLevel.Error, "Error in MongoDB CDC processor")]
	private partial void LogError(Exception ex);

	[LoggerMessage(DataMongoDbEventId.CdcFatalError, LogLevel.Critical,
		"Fatal (non-retryable) error in MongoDB CDC processor — stopping; the failure is surfaced to the configured handler or rethrown (no silent reconnect)")]
	private partial void LogFatalError(Exception ex);
}
