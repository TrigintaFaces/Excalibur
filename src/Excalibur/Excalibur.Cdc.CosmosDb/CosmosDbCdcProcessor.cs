// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using Excalibur.Cdc.Diagnostics;
using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Processes CosmosDb Change Feed events for CDC scenarios.
/// </summary>
public sealed partial class CosmosDbCdcProcessor : ICosmosDbCdcProcessor
{
	private readonly CosmosClient _client;
	private readonly ICosmosDbCdcStateStore _stateStore;
	private readonly CosmosDbCdcOptions _options;
	private readonly ILogger<CosmosDbCdcProcessor> _logger;

	private Container? _container;
	private CosmosDbCdcPosition _currentPosition;
	private volatile bool _disposed;
	private long _eventCount;

	// 14z4ao: optional fatal-handoff. A fatal (non-retryable) error stops the processor loudly instead of
	// an infinite silent reconnect loop (ADR-338). _onFatalError receives the in-flight event for a
	// per-event fatal, or null for a connection/poll-level fatal.
	private readonly CdcFatalErrorHandler<CosmosDbDataChangeEvent>? _onFatalError;
	private readonly IMessageFailureClassifier? _failureClassifier;
	private CosmosDbDataChangeEvent? _inFlightEvent;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbCdcProcessor"/> class.
	/// </summary>
	/// <param name="client">The CosmosDB client from DI.</param>
	/// <param name="stateStore">The state store for position tracking.</param>
	/// <param name="options">The CDC options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="fatalErrorOptions">
	/// Optional fatal-error handling. When omitted (or its handler is <see langword="null"/>), a fatal
	/// error rethrows and stops the processor (fail-loud — never an infinite silent reconnect loop).
	/// </param>
	/// <param name="failureClassifier">
	/// Optional shared classifier deciding whether a processing error is fatal (non-retryable) or
	/// transient. When omitted, a conservative built-in fallback is used.
	/// </param>
	public CosmosDbCdcProcessor(
		CosmosClient client,
		ICosmosDbCdcStateStore stateStore,
		IOptions<CosmosDbCdcOptions> options,
		ILogger<CosmosDbCdcProcessor> logger,
		IOptions<CdcFatalErrorOptions<CosmosDbDataChangeEvent>>? fatalErrorOptions = null,
		IMessageFailureClassifier? failureClassifier = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(stateStore);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();

		_client = client;
		_stateStore = stateStore;
		_logger = logger;
		_onFatalError = fatalErrorOptions?.Value.OnFatalError;
		_failureClassifier = failureClassifier;
		_currentPosition = _options.ChangeFeed.StartPosition ?? CosmosDbCdcPosition.Beginning();
	}

	/// <inheritdoc/>
	public async Task StartAsync(
		Func<CosmosDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventHandler);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await InitializeAsync(cancellationToken).ConfigureAwait(false);

		LogStartingContinuousProcessing(_options.ProcessorName, _options.DatabaseId, _options.ContainerId);

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				var processedCount = await ProcessBatchInternalAsync(eventHandler, cancellationToken).ConfigureAwait(false);

				if (processedCount == 0)
				{
					// No changes, wait before polling again
					await Task.Delay(_options.ChangeFeed.PollInterval, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				LogStoppingProcessing(_options.ProcessorName);
				break;
			}
			catch (Exception ex)
			{
				// pxhqri/q3w5cv: delegate the fatal-vs-transient decision to the single shared guard
				// (CdcFatalGuard.Decide) — the same unit the regression lock binds — instead of an inline
				// `catch when IsFatal` filter. The durable checkpoint advance inside ProcessBatchInternalAsync
				// is now LITERALLY gated on decision.AdvanceCheckpoint (false on every fault), so a fault never
				// advances the checkpoint past the failing change. 14z4ao behavior is preserved.
				var decision = CdcFatalGuard.Decide(ex, _failureClassifier);

				if (decision.Stop)
				{
					// Fatal (non-retryable) — stop loud, never an infinite silent reconnect.
					LogFatalError(ex);

					if (_onFatalError is not null)
					{
						// In-flight event for a per-event fatal; null for a connection/poll-level fatal.
						await _onFatalError(ex, _inFlightEvent).ConfigureAwait(false);
						return; // handler took over → terminal; do not reconnect.
					}

					throw; // default: fail-loud — propagate and stop.
				}

				// Transient (non-fatal: decision.Stop == false) — reconnect and retry from the un-advanced checkpoint.
				LogProcessingError(_options.ProcessorName, ex);
				_inFlightEvent = null;

				// Wait before retrying on error
				try
				{
					await Task.Delay(_options.ChangeFeed.PollInterval, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					break;
				}
			}
		}
	}

	/// <inheritdoc/>
	public async Task<int> ProcessBatchAsync(
		Func<CosmosDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventHandler);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await InitializeAsync(cancellationToken).ConfigureAwait(false);

		return await ProcessBatchInternalAsync(eventHandler, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<CosmosDbCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var storedPosition = await _stateStore.GetPositionAsync(_options.ProcessorName, cancellationToken).ConfigureAwait(false);
		return storedPosition ?? _currentPosition;
	}

	/// <inheritdoc/>
	public async Task ConfirmPositionAsync(CosmosDbCdcPosition position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await _stateStore.SavePositionAsync(_options.ProcessorName, position, cancellationToken).ConfigureAwait(false);
		_currentPosition = position;

		LogPositionConfirmed(_options.ProcessorName, position.ToString());
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_client.Dispose();
		await _stateStore.DisposeAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_client.Dispose();
		_stateStore.Dispose();
	}

	private async Task InitializeAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			return;
		}

		var database = _client.GetDatabase(_options.DatabaseId);
		_container = database.GetContainer(_options.ContainerId);

		// Restore position from state store
		var storedPosition = await _stateStore.GetPositionAsync(_options.ProcessorName, cancellationToken).ConfigureAwait(false);

		if (storedPosition is not null)
		{
			_currentPosition = storedPosition;
			LogRestoredPosition(_options.ProcessorName, storedPosition.ToString());
		}
		else if (_options.ChangeFeed.StartPosition is not null)
		{
			_currentPosition = _options.ChangeFeed.StartPosition;
		}
	}

	private async Task<int> ProcessBatchInternalAsync(
		Func<CosmosDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		using var pollActivity = CdcActivitySource.StartPollActivity("CosmosDb");

		var iterator = CreateChangeFeedIterator();
		var processedCount = 0;
		CosmosDbCdcPosition? lastPosition = null;
		Exception? batchFailure = null;

		try
		{
			while (iterator.HasMoreResults && processedCount < _options.ChangeFeed.MaxBatchSize)
			{
				FeedResponse<JsonDocument>? response;

				try
				{
					response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				}
				catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotModified)
				{
					// No new changes
					break;
				}

				if (response.Count == 0)
				{
					break;
				}

				using var batchActivity = CdcActivitySource.StartProcessBatchActivity("CosmosDb", response.Count);

				var continuationToken = response.ContinuationToken;
				lastPosition = CosmosDbCdcPosition.FromContinuationToken(continuationToken);

				foreach (var document in response)
				{
					var changeEvent = CreateChangeEvent(document, lastPosition);

					// Track the in-flight event so a fatal raised by the handler is attributed to it; the fatal
					// path unwinds before the post-batch ConfirmPositionAsync (durable checkpoint not advanced).
					_inFlightEvent = changeEvent;
					await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);
					_inFlightEvent = null;
					processedCount++;
					Interlocked.Increment(ref _eventCount);
				}
			}

		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Capture, do not advance here. The structural durability gate below sees
			// AdvanceCheckpoint=false for any fault and skips ConfirmPositionAsync.
			batchFailure = ex;
		}

		// STRUCTURAL durability gate (q3w5cv / FR-B2 / ADR-338): advance the durable checkpoint ONLY when
		// the single shared guard permits it. CdcFatalGuard.Decide(null) => AdvanceCheckpoint=true on clean
		// success; Decide(fault) => AdvanceCheckpoint=false on ANY fault. Mutating this gate to `if (true)`
		// would advance past an unprocessed change and turns AC-N3.1 RED — the violation is inexpressible
		// without editing this one literal gate.
		var decision = CdcFatalGuard.Decide(batchFailure, _failureClassifier);
		if (decision.AdvanceCheckpoint && lastPosition is not null)
		{
			await ConfirmPositionAsync(lastPosition, cancellationToken).ConfigureAwait(false);
		}

		if (batchFailure is not null)
		{
			LogBatchError(_options.ProcessorName, processedCount, batchFailure);
			ExceptionDispatchInfo.Capture(batchFailure).Throw();
		}

		if (processedCount > 0)
		{
			LogBatchProcessed(_options.ProcessorName, processedCount, _eventCount);
		}

		return processedCount;
	}

	private FeedIterator<JsonDocument> CreateChangeFeedIterator()
	{
		ChangeFeedStartFrom startFrom;

		if (_currentPosition.IsValid && _currentPosition.ContinuationToken is not null)
		{
			startFrom = ChangeFeedStartFrom.ContinuationToken(_currentPosition.ContinuationToken);
		}
		else if (_currentPosition.Timestamp.HasValue)
		{
			startFrom = ChangeFeedStartFrom.Time(_currentPosition.Timestamp.Value.UtcDateTime);
		}
		else
		{
			startFrom = ChangeFeedStartFrom.Beginning();
		}

		var requestOptions = new ChangeFeedRequestOptions
		{
			PageSizeHint = _options.ChangeFeed.MaxBatchSize,
		};

		// AllVersionsAndDeletes (full-fidelity) change feed is PREVIEW-ONLY in the .NET Cosmos SDK:
		// ChangeFeedMode.AllVersionsAndDeletes + ChangeFeedItem<T> ship in -preview packages
		// (>= 3.32.0-preview) and are NOT in the public surface of the pinned STABLE 3.58.0
		// (reflection-verified: ChangeFeedItem<T> is internal; ChangeFeedMode exposes only
		// Incremental/LatestVersion). A shipping framework must not take a preview Azure dependency,
		// so full-fidelity is gated to bd-ajt1iy (revisit on stable GA). Stable mode is Incremental.
		if (_options.ChangeFeed.Mode == CosmosDbCdcMode.AllVersionsAndDeletes)
		{
			throw new NotSupportedException(
				"AllVersionsAndDeletes change feed mode is not available in the stable Cosmos .NET SDK " +
				"(preview-only); tracked in bd-ajt1iy. Use Incremental/LatestVersion mode.");
		}

		var mode = ChangeFeedMode.Incremental;

		return _container!.GetChangeFeedIterator<JsonDocument>(startFrom, mode, requestOptions);
	}

	private CosmosDbDataChangeEvent CreateChangeEvent(JsonDocument document, CosmosDbCdcPosition position)
	{
		var root = document.RootElement;

		// Extract document ID
		var documentId = root.TryGetProperty("id", out var idProp)
			? idProp.GetString() ?? string.Empty
			: string.Empty;

		// Extract partition key if specified
		string? partitionKey = null;
		if (!string.IsNullOrEmpty(_options.PartitionKeyPath))
		{
			var pkPath = _options.PartitionKeyPath.TrimStart('/');
			if (root.TryGetProperty(pkPath, out var pkProp))
			{
				partitionKey = pkProp.GetString();
			}
		}

		// Extract timestamp (_ts is Unix epoch seconds)
		var timestamp = DateTimeOffset.UtcNow;
		if (_options.ChangeFeed.IncludeTimestamp && root.TryGetProperty("_ts", out var tsProp))
		{
			timestamp = DateTimeOffset.FromUnixTimeSeconds(tsProp.GetInt64());
		}

		// Extract LSN
		long lsn = 0;
		if (_options.ChangeFeed.IncludeLsn && root.TryGetProperty("_lsn", out var lsnProp))
		{
			lsn = lsnProp.GetInt64();
		}

		// Extract ETag
		var etag = root.TryGetProperty("_etag", out var etagProp)
			? etagProp.GetString()
			: null;

		// Determine change type
		var changeType = DetermineChangeType(root);

		return changeType switch
		{
			CosmosDbDataChangeType.Insert => CosmosDbDataChangeEvent.CreateInsert(
				position, documentId, partitionKey, document, timestamp, lsn, etag),
			CosmosDbDataChangeType.Update => CosmosDbDataChangeEvent.CreateUpdate(
				position, documentId, partitionKey, document, null, timestamp, lsn, etag),
			CosmosDbDataChangeType.Delete => CosmosDbDataChangeEvent.CreateDelete(
				position, documentId, partitionKey, null, timestamp, lsn),
			_ => CosmosDbDataChangeEvent.CreateUpdate(
				position, documentId, partitionKey, document, null, timestamp, lsn, etag),
		};
	}

	private CosmosDbDataChangeType DetermineChangeType(JsonElement root)
	{
		// In AllVersionsAndDeletes mode, check for metadata
		if (_options.ChangeFeed.Mode == CosmosDbCdcMode.AllVersionsAndDeletes)
		{
			// Check for "current" property which indicates the operation type in full fidelity mode
			if (root.TryGetProperty("current", out _))
			{
				// If "previous" exists, it's an update; if document has "current" only, it's insert
				if (root.TryGetProperty("previous", out _))
				{
					return CosmosDbDataChangeType.Update;
				}

				return CosmosDbDataChangeType.Insert;
			}

			// If only "previous" exists (no "current"), it's a delete
			if (root.TryGetProperty("previous", out _) && !root.TryGetProperty("current", out _))
			{
				return CosmosDbDataChangeType.Delete;
			}

			// Check metadata for ttlExpiration (soft delete)
			if (root.TryGetProperty("metadata", out var metadata))
			{
				if (metadata.TryGetProperty("operationType", out var opType))
				{
					var operation = opType.GetString();
					return operation?.ToUpperInvariant() switch
					{
						"CREATE" => CosmosDbDataChangeType.Insert,
						"REPLACE" => CosmosDbDataChangeType.Update,
						"DELETE" => CosmosDbDataChangeType.Delete,
						_ => CosmosDbDataChangeType.Update,
					};
				}
			}
		}

		// In LatestVersion mode, we can only distinguish insert from update
		// by checking if this is the first version (no _etag or version == 1)
		// For simplicity, treat all as updates (which is safe)
		return CosmosDbDataChangeType.Update;
	}

	// Source-generated logging methods
	[LoggerMessage(DataCosmosDbEventId.CdcStarting, LogLevel.Information, "Starting continuous CDC processing for processor '{ProcessorName}' on {DatabaseId}/{ContainerId}")]
	private partial void LogStartingContinuousProcessing(string processorName, string databaseId, string containerId);

	[LoggerMessage(DataCosmosDbEventId.CdcStopping, LogLevel.Information, "Stopping CDC processing for processor '{ProcessorName}'")]
	private partial void LogStoppingProcessing(string processorName);

	[LoggerMessage(DataCosmosDbEventId.CdcPositionRestored, LogLevel.Debug, "Restored position for processor '{ProcessorName}': {Position}")]
	private partial void LogRestoredPosition(string processorName, string position);

	[LoggerMessage(DataCosmosDbEventId.CdcPositionConfirmed, LogLevel.Debug, "Position confirmed for processor '{ProcessorName}': {Position}")]
	private partial void LogPositionConfirmed(string processorName, string position);

	[LoggerMessage(DataCosmosDbEventId.CdcBatchProcessed, LogLevel.Debug, "Processor '{ProcessorName}' processed batch of {Count} events (total: {TotalCount})")]
	private partial void LogBatchProcessed(string processorName, int count, long totalCount);

	[LoggerMessage(DataCosmosDbEventId.CdcProcessingError, LogLevel.Error, "Error processing CDC for processor '{ProcessorName}'")]
	private partial void LogProcessingError(string processorName, Exception exception);

	[LoggerMessage(DataCosmosDbEventId.CdcBatchError, LogLevel.Error, "Error in batch for processor '{ProcessorName}' after processing {Count} events")]
	private partial void LogBatchError(string processorName, int count, Exception exception);

	[LoggerMessage(DataCosmosDbEventId.CdcFatalError, LogLevel.Critical,
		"Fatal (non-retryable) error in CosmosDb CDC processor — stopping; the failure is surfaced to the configured handler or rethrown (no silent reconnect)")]
	private partial void LogFatalError(Exception ex);
}
