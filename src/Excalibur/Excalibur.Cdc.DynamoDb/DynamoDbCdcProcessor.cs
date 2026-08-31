// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Excalibur.Cdc.Diagnostics;
using Excalibur.Data.DynamoDb;
using Excalibur.Data.DynamoDb.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DynamoDbAttributeValue = Amazon.DynamoDBv2.Model.AttributeValue;

namespace Excalibur.Cdc.DynamoDb;

/// <summary>
/// DynamoDB Streams CDC processor implementation.
/// </summary>
/// <remarks>
/// <para>
/// This processor uses the DynamoDB Streams API to capture changes.
/// It manages multiple shards concurrently and handles resharding events.
/// </para>
/// <para>
/// Shard iterators expire after 15 minutes, so processing should not
/// pause for extended periods. The processor automatically refreshes
/// expired iterators using saved sequence numbers.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "CDC processors inherently couple with many SDK types.")]
public sealed partial class DynamoDbCdcProcessor : IDynamoDbCdcProcessor
{
	private readonly IAmazonDynamoDB _dynamoClient;
	private readonly IAmazonDynamoDBStreams _streamsClient;
	private readonly IDynamoDbCdcStateStore _stateStore;
	private readonly DynamoDbCdcOptions _options;
	private readonly ILogger<DynamoDbCdcProcessor> _logger;

	private readonly ConcurrentDictionary<string, string> _shardIterators = new();
	private readonly ConcurrentDictionary<string, string> _shardPositions = new();

	// True only between a fresh start-from-now initialization and the first record this processor handles.
	// It is cleared on that first record, so every shard discovered thereafter -- including split children,
	// which is what an unknown shard nearly always is -- takes the safe TRIM_HORIZON path.
	private bool _startFromLatest;
	private readonly SemaphoreSlim _processingLock = new(1, 1);

	private string? _streamArn;
	private DynamoDbCdcPosition? _currentPosition;
	private DateTimeOffset _lastShardDiscovery = DateTimeOffset.MinValue;
	private volatile bool _disposed;

	// optional fatal-handoff. A fatal (non-retryable) error stops the processor loudly instead of
	// an infinite silent reconnect loop. _onFatalError receives the in-flight event for a
	// per-event fatal, or null for a connection/poll-level fatal.
	private readonly CdcFatalErrorHandler<DynamoDbDataChangeEvent>? _onFatalError;
	private readonly IMessageFailureClassifier? _failureClassifier;
	private DynamoDbDataChangeEvent? _inFlightEvent;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbCdcProcessor"/> class.
	/// </summary>
	/// <param name="dynamoClient">The DynamoDB client for table operations.</param>
	/// <param name="streamsClient">The DynamoDB Streams client.</param>
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
	public DynamoDbCdcProcessor(
		IAmazonDynamoDB dynamoClient,
		IAmazonDynamoDBStreams streamsClient,
		IDynamoDbCdcStateStore stateStore,
		IOptions<DynamoDbCdcOptions> options,
		ILogger<DynamoDbCdcProcessor> logger,
		IOptions<CdcFatalErrorOptions<DynamoDbDataChangeEvent>>? fatalErrorOptions = null,
		IMessageFailureClassifier? failureClassifier = null)
	{
		ArgumentNullException.ThrowIfNull(options);

		_dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
		_streamsClient = streamsClient ?? throw new ArgumentNullException(nameof(streamsClient));
		_stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
		_options = options.Value;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_onFatalError = fatalErrorOptions?.Value.OnFatalError;
		_failureClassifier = failureClassifier;

		_options.Validate();
	}

	/// <inheritdoc/>
	public async Task StartAsync(
		Func<DynamoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await InitializeAsync(cancellationToken).ConfigureAwait(false);
		LogStartingCdcProcessor(_options.ProcessorName, _streamArn!);

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				// Discover new shards periodically
				if (_options.AutoDiscoverShards &&
					DateTimeOffset.UtcNow - _lastShardDiscovery > _options.ShardDiscoveryInterval)
				{
					await DiscoverShardsAsync(cancellationToken).ConfigureAwait(false);
				}

				var processed = await ProcessBatchInternalAsync(eventHandler, autoConfirm: true, cancellationToken)
					.ConfigureAwait(false);

				if (processed == 0)
				{
					// No records, wait before polling again
					await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			// fatal decision delegated to the single shared guard — CdcFatalGuard.Decide(ex).Stop
			// is exactly equivalent to the old inline IsFatal (the same unit the regression lock binds).
			catch (Exception ex) when (CdcFatalGuard.Decide(ex, _failureClassifier).Stop)
			{
				// a fatal (non-retryable) error — stop loud, never an infinite silent reconnect.
				// The shard position (_shardPositions[shardId]) advances ONLY after a record is successfully
				// handed off; a handler throw is caught inside ProcessBatchInternalAsync, which re-points the
				// iterator to AFTER the last handled record and durably confirms only that handled prefix
				// (never the failed record) before re-surfacing. So the checkpoint is never advanced past the
				// failing change (at-least-once preserved — it is re-delivered on restart).
				LogFatalError(ex);

				if (_onFatalError is not null)
				{
					// In-flight event for a per-event fatal; null for a connection/poll-level fatal.
					await _onFatalError(ex, _inFlightEvent).ConfigureAwait(false);
					break; // handler took over → terminal; do not reconnect.
				}

				throw; // default: fail-loud — propagate and stop.
			}
			catch (Exception ex)
			{
				LogProcessingError(_options.ProcessorName, ex);
				_inFlightEvent = null;
				// Wait before retrying
				await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
			}
		}

		LogStoppingCdcProcessor(_options.ProcessorName);
	}

	/// <inheritdoc/>
	public async Task<int> ProcessBatchAsync(
		Func<DynamoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await InitializeAsync(cancellationToken).ConfigureAwait(false);

		return await ProcessBatchInternalAsync(eventHandler, autoConfirm: false, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public Task<DynamoDbCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var positions = new Dictionary<string, string>(_shardPositions);
		var position = DynamoDbCdcPosition.FromShardPositions(_streamArn ?? string.Empty, positions);

		return Task.FromResult(position);
	}

	/// <inheritdoc/>
	public async Task ConfirmPositionAsync(
		DynamoDbCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(position);

		await _stateStore.SavePositionAsync(_options.ProcessorName, position, cancellationToken)
			.ConfigureAwait(false);

		_currentPosition = position;
		LogPositionConfirmed(_options.ProcessorName, position.ShardPositions.Count);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_processingLock.Dispose();

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
		_processingLock.Dispose();
		_stateStore?.Dispose();
	}

	private static DynamoDbDataChangeType MapChangeType(OperationType operationType)
	{
		return operationType.Value switch
		{
			"INSERT" => DynamoDbDataChangeType.Insert,
			"MODIFY" => DynamoDbDataChangeType.Modify,
			"REMOVE" => DynamoDbDataChangeType.Remove,
			_ => throw new InvalidOperationException($"Unknown operation type: {operationType.Value}"),
		};
	}

	private async Task InitializeAsync(CancellationToken cancellationToken)
	{
		if (_streamArn is not null)
		{
			return;
		}

		await _processingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_streamArn is not null)
			{
				return;
			}

			// Get or discover stream ARN
			_streamArn = _options.StreamArn;
			if (string.IsNullOrWhiteSpace(_streamArn))
			{
				var tableResponse = await _dynamoClient.DescribeTableAsync(_options.TableName, cancellationToken)
					.ConfigureAwait(false);

				_streamArn = tableResponse.Table.LatestStreamArn;
				if (string.IsNullOrEmpty(_streamArn))
				{
					throw new InvalidOperationException($"Table '{_options.TableName}' does not have streams enabled.");
				}
			}

			// Load saved position or use start position
			var savedPosition = await _stateStore.GetPositionAsync(_options.ProcessorName, cancellationToken)
				.ConfigureAwait(false);

			_currentPosition = _options.StartPosition ?? savedPosition ?? DynamoDbCdcPosition.Beginning(_streamArn);

			// Start-from-now is honoured ONLY here, on a genuinely fresh start, and never again. The two
			// questions "where does a new deployment begin?" and "where does a resumed consumer pick up an
			// unfamiliar shard?" have opposite safe answers, and answering them with one branch is what made
			// this seam lose records: LATEST is right for the first and silently skips history for the
			// second. Requiring an explicit StartPosition AND no saved position keeps them apart -- once
			// anything has been checkpointed, a shard we do not recognise is a split child and reads from
			// TRIM_HORIZON.
			_startFromLatest = _options.StartPosition is { Timestamp: not null }
				&& _options.StartPosition.ShardPositions.Count == 0
				&& savedPosition is null;

			// Initialize shard positions from saved position
			foreach (var kvp in _currentPosition.ShardPositions)
			{
				_shardPositions[kvp.Key] = kvp.Value;
			}

			// Discover initial shards
			await DiscoverShardsAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_ = _processingLock.Release();
		}
	}

	private async Task DiscoverShardsAsync(CancellationToken cancellationToken)
	{
		var describeRequest = new DescribeStreamRequest { StreamArn = _streamArn };
		var response = await _streamsClient.DescribeStreamAsync(describeRequest, cancellationToken)
			.ConfigureAwait(false);

		var shards = response.StreamDescription.Shards;
		var newShardCount = 0;

		foreach (var shard in shards)
		{
			// Skip shards that have ended (fully processed parent shards)
			if (shard.SequenceNumberRange?.EndingSequenceNumber is not null &&
				_shardPositions.ContainsKey(shard.ShardId))
			{
				continue;
			}

			if (!_shardIterators.ContainsKey(shard.ShardId))
			{
				await InitializeShardIteratorAsync(shard, cancellationToken).ConfigureAwait(false);
				newShardCount++;
			}
		}

		if (newShardCount > 0)
		{
			LogShardsDiscovered(newShardCount, shards.Count);
		}

		_lastShardDiscovery = DateTimeOffset.UtcNow;
	}

	private async Task InitializeShardIteratorAsync(Shard shard, CancellationToken cancellationToken)
	{
		var iteratorRequest = new GetShardIteratorRequest { StreamArn = _streamArn, ShardId = shard.ShardId, };

		// The invariant: for every shard, the iterator opened is at or before the first record on that
		// shard this consumer has not handled. A shard we have a position for resumes after it; a shard we
		// have never seen must start at the beginning of what the stream still holds.
		if (_shardPositions.TryGetValue(shard.ShardId, out var sequenceNumber))
		{
			iteratorRequest.ShardIteratorType = ShardIteratorType.AFTER_SEQUENCE_NUMBER;
			iteratorRequest.SequenceNumber = sequenceNumber;
		}
		else
		{
			// TRIM_HORIZON, never LATEST. An unknown shard is overwhelmingly a shard SPLIT: DynamoDB closes
			// a shard and opens children, and the children carry every write since the split. Opening a
			// child at LATEST skips everything written between the split and this moment -- silently, with
			// no exception, no gap counter and no log line, while the processor appears to make healthy
			// progress past records it never saw.
			//
			// Redelivery is the cost, and it is the cost at-least-once was always going to charge; handlers
			// must be idempotent regardless. Losing records is not a cost this seam is allowed to pay.
			//
			// This previously fell back to LATEST for any position that was not IsBeginning -- and
			// IsBeginning goes false after the first confirmed record, so the safe branch was unreachable
			// for any processor that had ever made progress. A start-from-now deployment is a deliberate
			// INITIAL choice, recorded at initialization, not something inferred from a resume path that
			// cannot tell the two apart.
			iteratorRequest.ShardIteratorType = _startFromLatest
				? ShardIteratorType.LATEST
				: ShardIteratorType.TRIM_HORIZON;
		}

		var response = await _streamsClient.GetShardIteratorAsync(iteratorRequest, cancellationToken)
			.ConfigureAwait(false);

		if (!string.IsNullOrEmpty(response.ShardIterator))
		{
			_shardIterators[shard.ShardId] = response.ShardIterator;
		}
	}

	private async Task<int> ProcessBatchInternalAsync(
		Func<DynamoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		bool autoConfirm,
		CancellationToken cancellationToken)
	{
		using var pollActivity = CdcActivitySource.StartPollActivity("DynamoDb");

		var totalProcessed = 0;
		var shardsToRemove = new List<string>();

		foreach (var shardId in _shardIterators.Keys.ToArray())
		{
			if (!_shardIterators.TryGetValue(shardId, out var iterator) ||
				string.IsNullOrEmpty(iterator))
			{
				shardsToRemove.Add(shardId);
				continue;
			}

			try
			{
				var recordsRequest = new GetRecordsRequest { ShardIterator = iterator, Limit = _options.MaxBatchSize, };

				var response = await _streamsClient.GetRecordsAsync(recordsRequest, cancellationToken)
					.ConfigureAwait(false);

				if (response.Records.Count == 0)
				{
					// No records were handed off — safe to advance the iterator (nothing can be skipped).
					AdvanceOrRetireShard(shardId, response.NextShardIterator, shardsToRemove);
					continue;
				}

				LogReceivedBatch(shardId, response.Records.Count);

				using var batchActivity = CdcActivitySource.StartProcessBatchActivity("DynamoDb", response.Records.Count);

				Exception? batchFailure = null;

				try
				{
					foreach (var record in response.Records)
					{
						var changeEvent = CreateChangeEvent(shardId, record);

						// Track the in-flight event so a fatal raised by the handler is attributed to it. On a
						// throw, _shardPositions is NOT updated for this record (below), so the iterator repoint
						// and durable confirm in the catch use only the handled prefix — never the failed record.
						_inFlightEvent = changeEvent;
						await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);

						// Position advances ONLY after the record was successfully handed off.
						_shardPositions[shardId] = record.Dynamodb.SequenceNumber;

						// The fresh start is over: from here on this consumer has history to protect, so an
						// unfamiliar shard must be read from TRIM_HORIZON rather than skipped to LATEST.
						_startFromLatest = false;
						_inFlightEvent = null;
						totalProcessed++;
					}
				}
				catch (Exception ex) when (ex is not ExpiredIteratorException and not OperationCanceledException)
				{
					// A handler threw mid-batch: do NOT advance the iterator past the handled prefix. Re-point
					// the shard iterator to resume AFTER the last successfully-handled record, so the failed
					// record (and the rest of the batch) are re-delivered rather than silently skipped
					// (at-least-once). Durably confirm the handled prefix before surfacing.
					await RepointShardIteratorAsync(shardId, cancellationToken).ConfigureAwait(false);

					if (autoConfirm)
					{
						// the in-catch durable prefix-confirm MUST NOT mask the original handler
						// exception (root cause). A confirm/position-read failure here is diagnosability-only:
						// log it and let the handler exception below propagate. At-least-once is unaffected —
						// the failed record and the rest of the batch are re-delivered on the repointed iterator.
						try
						{
							await ConfirmPositionAsync(
								await GetCurrentPositionAsync(cancellationToken).ConfigureAwait(false),
								cancellationToken).ConfigureAwait(false);
						}
#pragma warning disable CA1031 // A confirm failure must never replace the root-cause handler exception.
						catch (Exception confirmEx)
#pragma warning restore CA1031
						{
							LogPrefixConfirmFailedAfterHandlerError(shardId, confirmEx);
						}
					}

					// Capture, do not rethrow here. The structural durability gate below sees
					// AdvanceCheckpoint=false for any fault and skips the full-batch iterator advance;
					// the captured handler exception is then re-thrown after the gate.
					batchFailure = ex;
				}

				// STRUCTURAL durability gate: the full-batch iterator
				// advance happens ONLY when the single shared guard permits it. On ANY handler fault
				// CdcFatalGuard.Decide(batchFailure,…).AdvanceCheckpoint is false, so the iterator is never
				// advanced past the batch — the in-catch repoint + prefix-confirm above already handled the
				// at-least-once handled-prefix. Mutating this gate to `if (true)` advances the iterator past
				// the failed record (overriding the repoint) ⇒ the durability arm goes RED. Non-vacuous, structural.
				var decision = CdcFatalGuard.Decide(batchFailure, _failureClassifier);
				if (decision.AdvanceCheckpoint)
				{
					// Entire batch handed off successfully — NOW it is safe to advance the iterator.
					AdvanceOrRetireShard(shardId, response.NextShardIterator, shardsToRemove);

					// Batch checkpoint: save position once per shard batch instead of per-record.
					if (autoConfirm)
					{
						await ConfirmPositionAsync(
							await GetCurrentPositionAsync(cancellationToken).ConfigureAwait(false),
							cancellationToken).ConfigureAwait(false);
					}
				}

				if (batchFailure is not null)
				{
					ExceptionDispatchInfo.Capture(batchFailure).Throw();
				}
			}
			catch (ExpiredIteratorException)
			{
				LogIteratorExpired(shardId);
				// Remove and let next discovery refresh it
				shardsToRemove.Add(shardId);
			}
		}

		// Clean up exhausted shards
		foreach (var shardId in shardsToRemove)
		{
			_ = _shardIterators.TryRemove(shardId, out _);
		}

		return totalProcessed;
	}

	/// <summary>
	/// Advances the shard to the next iterator after a batch has been fully handled, or retires the shard
	/// when the stream reports it exhausted (no next iterator).
	/// </summary>
	private void AdvanceOrRetireShard(string shardId, string? nextShardIterator, List<string> shardsToRemove)
	{
		if (!string.IsNullOrEmpty(nextShardIterator))
		{
			_shardIterators[shardId] = nextShardIterator;
		}
		else
		{
			// Shard is exhausted.
			shardsToRemove.Add(shardId);
		}
	}

	/// <summary>
	/// Re-points a shard's iterator to resume immediately AFTER the last successfully-handled sequence
	/// number, so a failed or unhandled record is re-delivered rather than skipped (at-least-once). If no
	/// position has been recorded for the shard yet, the current iterator is left untouched and the batch
	/// is simply re-read on the next poll.
	/// </summary>
	private async Task RepointShardIteratorAsync(string shardId, CancellationToken cancellationToken)
	{
		if (!_shardPositions.TryGetValue(shardId, out var lastHandledSequence) ||
			string.IsNullOrEmpty(lastHandledSequence))
		{
			return;
		}

		var iteratorRequest = new GetShardIteratorRequest
		{
			StreamArn = _streamArn,
			ShardId = shardId,
			ShardIteratorType = ShardIteratorType.AFTER_SEQUENCE_NUMBER,
			SequenceNumber = lastHandledSequence,
		};

		var response = await _streamsClient.GetShardIteratorAsync(iteratorRequest, cancellationToken)
			.ConfigureAwait(false);

		if (!string.IsNullOrEmpty(response.ShardIterator))
		{
			_shardIterators[shardId] = response.ShardIterator;
		}
	}

	private DynamoDbDataChangeEvent CreateChangeEvent(string shardId, Record record)
	{
		var streamRecord = record.Dynamodb;
		var sequenceNumber = streamRecord.SequenceNumber;
		var timestamp = streamRecord.ApproximateCreationDateTime.HasValue
			? new DateTimeOffset(
				DateTime.SpecifyKind(
					streamRecord.ApproximateCreationDateTime.Value,
					DateTimeKind.Utc))
			: DateTimeOffset.UtcNow;
		var keys = DynamoDbAttributeValueConverter.ToAttributeValueMap(streamRecord.Keys) ??
				   new Dictionary<string, DynamoDbAttributeValue>(StringComparer.Ordinal);
		var newImage = DynamoDbAttributeValueConverter.ToAttributeValueMap(streamRecord.NewImage);
		var oldImage = DynamoDbAttributeValueConverter.ToAttributeValueMap(streamRecord.OldImage);

		var position = _currentPosition!.WithShardPosition(shardId, sequenceNumber);
		var changeType = MapChangeType(record.EventName);

		return changeType switch
		{
			DynamoDbDataChangeType.Insert => DynamoDbDataChangeEvent.CreateInsert(
				position,
				shardId,
				sequenceNumber,
				keys,
				newImage,
				timestamp,
				record.EventID),

			DynamoDbDataChangeType.Modify => DynamoDbDataChangeEvent.CreateModify(
				position,
				shardId,
				sequenceNumber,
				keys,
				newImage,
				oldImage,
				timestamp,
				record.EventID),

			DynamoDbDataChangeType.Remove => DynamoDbDataChangeEvent.CreateRemove(
				position,
				shardId,
				sequenceNumber,
				keys,
				oldImage,
				timestamp,
				record.EventID),

			_ => throw new InvalidOperationException($"Unknown event type: {record.EventName}"),
		};
	}

	[LoggerMessage(DataDynamoDbEventId.CdcProcessorStarting, LogLevel.Information,
		"Starting DynamoDB CDC processor '{ProcessorName}' for stream '{StreamArn}'")]
	private partial void LogStartingCdcProcessor(string processorName, string streamArn);

	[LoggerMessage(DataDynamoDbEventId.CdcProcessorStopping, LogLevel.Information, "Stopping DynamoDB CDC processor '{ProcessorName}'")]
	private partial void LogStoppingCdcProcessor(string processorName);

	[LoggerMessage(DataDynamoDbEventId.CdcBatchReceived, LogLevel.Debug, "Received batch of {Count} records from shard '{ShardId}'")]
	private partial void LogReceivedBatch(string shardId, int count);

	[LoggerMessage(DataDynamoDbEventId.CdcPositionConfirmed, LogLevel.Debug,
		"Position confirmed for processor '{ProcessorName}' with {ShardCount} shards")]
	private partial void LogPositionConfirmed(string processorName, int shardCount);

	[LoggerMessage(DataDynamoDbEventId.CdcShardsDiscovered, LogLevel.Information, "Discovered {NewCount} new shards (total: {TotalCount})")]
	private partial void LogShardsDiscovered(int newCount, int totalCount);

	[LoggerMessage(DataDynamoDbEventId.CdcIteratorExpired, LogLevel.Warning,
		"Shard iterator expired for shard '{ShardId}', will refresh on next discovery")]
	private partial void LogIteratorExpired(string shardId);

	[LoggerMessage(DataDynamoDbEventId.CdcProcessingError, LogLevel.Error, "Error processing CDC batch for '{ProcessorName}'")]
	private partial void LogProcessingError(string processorName, Exception exception);

	[LoggerMessage(DataDynamoDbEventId.CdcPrefixConfirmFailedAfterHandlerError, LogLevel.Warning,
		"Durable prefix-confirm failed after a handler error on shard '{ShardId}'; the original handler exception is preserved and propagated.")]
	private partial void LogPrefixConfirmFailedAfterHandlerError(string shardId, Exception exception);

	[LoggerMessage(DataDynamoDbEventId.CdcFatalError, LogLevel.Critical,
		"Fatal (non-retryable) error in DynamoDB CDC processor — stopping; the failure is surfaced to the configured handler or rethrown (no silent reconnect)")]
	private partial void LogFatalError(Exception ex);
}
