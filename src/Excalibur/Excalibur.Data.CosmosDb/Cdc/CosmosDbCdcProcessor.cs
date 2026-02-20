// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Net;
using System.Text.Json;

using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.Data.CosmosDb.Resources;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb.Cdc;

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

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbCdcProcessor"/> class.
	/// </summary>
	/// <param name="client">The CosmosDB client from DI.</param>
	/// <param name="stateStore">The state store for position tracking.</param>
	/// <param name="options">The CDC options.</param>
	/// <param name="logger">The logger.</param>
	public CosmosDbCdcProcessor(
		CosmosClient client,
		ICosmosDbCdcStateStore stateStore,
		IOptions<CosmosDbCdcOptions> options,
		ILogger<CosmosDbCdcProcessor> logger)
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
		_currentPosition = _options.StartPosition ?? CosmosDbCdcPosition.Beginning();
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
					await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				LogStoppingProcessing(_options.ProcessorName);
				break;
			}
			catch (Exception ex)
			{
				LogProcessingError(_options.ProcessorName, ex);

				// Wait before retrying on error
				try
				{
					await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
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
		else if (_options.StartPosition is not null)
		{
			_currentPosition = _options.StartPosition;
		}
	}

	private async Task<int> ProcessBatchInternalAsync(
		Func<CosmosDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		var iterator = CreateChangeFeedIterator();
		var processedCount = 0;
		CosmosDbCdcPosition? lastPosition = null;

		try
		{
			while (iterator.HasMoreResults && processedCount < _options.MaxBatchSize)
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

				var continuationToken = response.ContinuationToken;
				lastPosition = CosmosDbCdcPosition.FromContinuationToken(continuationToken);

				foreach (var document in response)
				{
					var changeEvent = CreateChangeEvent(document, lastPosition);

					await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);
					processedCount++;
					Interlocked.Increment(ref _eventCount);
				}
			}

			// Confirm position after successful batch processing
			if (lastPosition is not null)
			{
				await ConfirmPositionAsync(lastPosition, cancellationToken).ConfigureAwait(false);
			}

			if (processedCount > 0)
			{
				LogBatchProcessed(_options.ProcessorName, processedCount, _eventCount);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogBatchError(_options.ProcessorName, processedCount, ex);
			throw;
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
			PageSizeHint = _options.MaxBatchSize,
		};

		// SDK 3.47.0 provides Incremental mode only (LatestVersion is an alias).
		// AllVersionsAndDeletes mode requires SDK 3.50.0+ with ChangeFeedMode.AllVersionsAndDeletes.
		// Only Incremental/LatestVersion mode is currently supported.
		if (_options.Mode == CosmosDbCdcMode.AllVersionsAndDeletes)
		{
			throw new NotSupportedException(
				ErrorMessages.AllVersionsAndDeletesModeNotSupported);
		}

		var mode = ChangeFeedMode.Incremental;

		return _container.GetChangeFeedIterator<JsonDocument>(startFrom, mode, requestOptions);
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
		if (_options.IncludeTimestamp && root.TryGetProperty("_ts", out var tsProp))
		{
			timestamp = DateTimeOffset.FromUnixTimeSeconds(tsProp.GetInt64());
		}

		// Extract LSN
		long lsn = 0;
		if (_options.IncludeLsn && root.TryGetProperty("_lsn", out var lsnProp))
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
		if (_options.Mode == CosmosDbCdcMode.AllVersionsAndDeletes)
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
}
