// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;

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

	// optional fatal-handoff. A fatal (non-retryable) error stops the processor loudly instead of
	// an infinite silent reconnect loop. _onFatalError receives the in-flight event for a
	// per-event fatal, or null for a connection/stream-level fatal.
	private readonly CdcFatalErrorHandler<MongoDbDataChangeEvent>? _onFatalError;
	private readonly IMessageFailureClassifier? _failureClassifier;
	private MongoDbDataChangeEvent? _inFlightEvent;
	private readonly CdcFatalErrorOptions<MongoDbDataChangeEvent> _fatalErrorOptions;
	private readonly TimeProvider _timeProvider;
	private readonly CdcHealthState? _healthState;

	// The reconnect bound for the current StartAsync call; touched only by that call's consume loop.
	private CdcTransientFailureBackoff? _backoff;

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
	/// <param name="timeProvider">
	/// The clock the reconnect backoff waits on and measures stable connections with; defaults to the
	/// system clock.
	/// </param>
	/// <param name="healthState">
	/// Where consecutive reconnect failures are reported for the CDC health check, when one is registered.
	/// </param>
	public MongoDbCdcProcessor(
		IMongoClient client,
		IOptions<MongoDbCdcOptions> options,
		IMongoDbCdcStateStore stateStore,
		ILogger<MongoDbCdcProcessor> logger,
		IOptions<CdcFatalErrorOptions<MongoDbDataChangeEvent>>? fatalErrorOptions = null,
		IMessageFailureClassifier? failureClassifier = null,
		TimeProvider? timeProvider = null,
		CdcHealthState? healthState = null)
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
		_fatalErrorOptions = fatalErrorOptions?.Value ?? new CdcFatalErrorOptions<MongoDbDataChangeEvent>();
		_onFatalError = _fatalErrorOptions.OnFatalError;
		_failureClassifier = failureClassifier;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_healthState = healthState;
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

		_backoff = new CdcTransientFailureBackoff(
			_options.ReconnectInterval,
			_fatalErrorOptions.MaxReconnectDelay,
			_fatalErrorOptions.MaxConsecutiveTransientFailures,
			_timeProvider,
			_healthState);

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				_backoff.BeginAttempt();
				var invalidated = await ProcessChangesAsync(eventHandler, cancellationToken)
					.ConfigureAwait(false);

				if (invalidated)
				{
					// The next open starts AFTER the invalidation, so the loop makes progress rather than
					// re-reading the same cursor. The pause is only there so a namespace being repeatedly
					// dropped and recreated cannot spin this loop.
					await Task.Delay(_options.ReconnectInterval, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				LogStopping();
				throw;
			}
			catch (Exception ex)
			{
				// Delegate the fatal-vs-transient decision to the single shared guard (CdcFatalGuard.Decide)
				// — the same unit the regression lock binds — instead of an inline `catch when IsFatal`
				// filter. The durable checkpoint is advanced only on the success path, per
				// successfully-handled change (SavePositionAsync inside ProcessChangesAsync). This catch
				// never advances it, and the stream unwinds before that confirm on a fault, so a fault
				// (fatal or transient) never advances the checkpoint past the failing change — a transient
				// reconnect resumes just past the last successfully-handled change (bounded redelivery).
				var decision = CdcFatalGuard.Decide(ex, _failureClassifier);

				if (decision.Stop)
				{
					// Fatal (non-retryable) — stop loud, never an infinite silent reconnect.
					await StopTerminallyAsync(ex).ConfigureAwait(false);
					return; // the fatal handler took over → terminal; do not reconnect.
				}

				// Transient. Counted BEFORE the in-flight event is cleared, so a limit reached on a poisoned
				// change still hands that change to the fatal handler.
				var outcome = _backoff.RecordTransientFailure();
				if (outcome.Exhausted)
				{
					await StopTerminallyAsync(new CdcRetryExhaustedException(outcome.ConsecutiveFailures, ex))
						.ConfigureAwait(false);
					return;
				}

				// Reconnect and retry from the un-advanced checkpoint.
				LogError(ex);
				_inFlightEvent = null;

				await Task.Delay(outcome.Delay, _timeProvider, cancellationToken).ConfigureAwait(false);
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
		var invalidated = false;
		var changeStreamOptions = BuildChangeStreamOptions();

		using var cursor = await WatchAsync(changeStreamOptions, cancellationToken).ConfigureAwait(false);

		// Use a timeout for batch processing
		using var batchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		batchCts.CancelAfter(_options.ChangeStream.MaxAwaitTime * 2);

		try
		{
			while (!invalidated &&
				   count < _options.BatchSize &&
				   await cursor.MoveNextAsync(batchCts.Token).ConfigureAwait(false))
			{
				foreach (var change in cursor.Current)
				{
					var changeEvent = ConvertToChangeEvent(change);

					// Invalidation ends the batch on exactly the terms the continuous path uses: the
					// invalidate token is checkpointed in startAfter mode and the event is not handed to
					// the handler. Checkpointing it as an ordinary resumeAfter position — which is what
					// falling through to the position update below would do — leaves the next call unable
					// to open past the invalidation at all.
					if (changeEvent is not null &&
						changeEvent.ChangeType == MongoDbDataChangeType.Invalidate)
					{
						LogInvalidate();

						_currentPosition = new MongoDbCdcPosition(
							change.ResumeToken,
							MongoDbChangeStreamResumeMode.StartAfter);

						await _stateStore
							.SavePositionAsync(_options.ProcessorId, _currentPosition, cancellationToken)
							.ConfigureAwait(false);

						_confirmedPosition = _currentPosition;
						invalidated = true;
						break;
					}

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

		// Save position if we processed anything. An invalidation has already written its own checkpoint
		// above, and that one must not be overwritten with a resumeAfter-mode position.
		if (count > 0 && !invalidated)
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
			// resumeAfter and startAfter are mutually exclusive — the server rejects a request carrying
			// both — and only startAfter can carry the stream past an invalidate. The token itself records
			// which one it is usable as, so this never has to guess.
			if (_confirmedPosition.ResumeMode == MongoDbChangeStreamResumeMode.StartAfter)
			{
				options.StartAfter = _confirmedPosition.ResumeToken;
			}
			else
			{
				options.ResumeAfter = _confirmedPosition.ResumeToken;
			}
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
			//. An empty pipeline watches all change events unfiltered, which is the intended default.
			return new EmptyPipelineDefinition<ChangeStreamDocument<BsonDocument>>();
		}

		return PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>
			.Create(stages);
	}

	/// <summary>
	/// Reads the change stream until it ends or is invalidated.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> when the stream ended because the namespace was invalidated, in which case
	/// the durable checkpoint now names the invalidation boundary and must be reopened with
	/// <c>startAfter</c>; otherwise <see langword="false"/>.
	/// </returns>
	private async Task<bool> ProcessChangesAsync(
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

						// Checkpoint the INVALIDATE event's own token, in startAfter mode. Saving the
						// pre-invalidation position instead — and reopening it with resumeAfter — cannot
						// advance past the invalidation: every reopen replays to the same invalidate and
						// stops there, so every change made after the drop/rename is never delivered. The
						// mode is persisted with the token so a cold restart reopens correctly too.
						_currentPosition = new MongoDbCdcPosition(
							change.ResumeToken,
							MongoDbChangeStreamResumeMode.StartAfter);

						await _stateStore
							.SavePositionAsync(_options.ProcessorId, _currentPosition, cancellationToken)
							.ConfigureAwait(false);

						_confirmedPosition = _currentPosition;
						_backoff?.RecordProgress();

						return true;
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

						// Durably checkpoint AFTER each successfully-handled change so a transient reconnect
						// resumes just past it — bounding the redelivery window to (at most) the in-flight
						// change, instead of replaying everything since the last invalidate. Only reached on
						// handler success; a fault unwinds before this, leaving the checkpoint un-advanced.
						_currentPosition = new MongoDbCdcPosition(change.ResumeToken);
						await _stateStore
							.SavePositionAsync(_options.ProcessorId, _currentPosition, cancellationToken)
							.ConfigureAwait(false);
						_confirmedPosition = _currentPosition;
						_backoff?.RecordProgress();
					}
				}

				// Advance the in-memory observed position for every change (including filtered/unhandled
				// ones); the durable checkpoint is advanced on the success path above.
				_currentPosition = new MongoDbCdcPosition(change.ResumeToken);
			}
		}

		if (count > 0)
		{
			using var batchActivity = CdcActivitySource.StartProcessBatchActivity("MongoDB", count);
		}

		return false;
	}

	private bool ShouldProcessChange(MongoDbDataChangeEvent changeEvent)
	{
		// If no collections configured, process all
		if (_options.CollectionNames.Length == 0)
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

	/// <summary>
	/// Stops the consume loop for good: hands the failure to the fatal-error handler when one is configured,
	/// or throws it.
	/// </summary>
	/// <remarks>
	/// Shared by a fatal error and an exhausted retry so the two cannot drift apart. Nothing here writes a
	/// position, so a restarted processor resumes from the last one confirmed.
	/// </remarks>
	/// <param name="reason">The fatal error, or the exception describing an exhausted retry.</param>
	/// <returns>A task that completes only when the fatal-error handler took over.</returns>
	private async Task StopTerminallyAsync(Exception reason)
	{
		LogFatalError(reason);

		if (_onFatalError is not null)
		{
			// In-flight event for a per-event failure; null for a connection/poll-level one.
			await _onFatalError(reason, _inFlightEvent).ConfigureAwait(false);
			return;
		}

		// Rethrow preserving the original stack when the reason was thrown; an exhaustion wrapper was not.
		ExceptionDispatchInfo.Throw(reason);
	}
}
