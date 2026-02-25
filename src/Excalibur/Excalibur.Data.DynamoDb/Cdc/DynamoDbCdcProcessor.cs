// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Excalibur.Data.DynamoDb.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DynamoDbAttributeValue = Amazon.DynamoDBv2.Model.AttributeValue;

namespace Excalibur.Data.DynamoDb.Cdc;

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
	private readonly SemaphoreSlim _processingLock = new(1, 1);

	private string? _streamArn;
	private DynamoDbCdcPosition? _currentPosition;
	private DateTimeOffset _lastShardDiscovery = DateTimeOffset.MinValue;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbCdcProcessor"/> class.
	/// </summary>
	/// <param name="dynamoClient">The DynamoDB client for table operations.</param>
	/// <param name="streamsClient">The DynamoDB Streams client.</param>
	/// <param name="stateStore">The state store for position tracking.</param>
	/// <param name="options">The CDC options.</param>
	/// <param name="logger">The logger.</param>
	public DynamoDbCdcProcessor(
		IAmazonDynamoDB dynamoClient,
		IAmazonDynamoDBStreams streamsClient,
		IDynamoDbCdcStateStore stateStore,
		IOptions<DynamoDbCdcOptions> options,
		ILogger<DynamoDbCdcProcessor> logger)
	{
		ArgumentNullException.ThrowIfNull(options);

		_dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
		_streamsClient = streamsClient ?? throw new ArgumentNullException(nameof(streamsClient));
		_stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
		_options = options.Value;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_options.Validate();
	}

	/// <inheritdoc/>
	public async Task StartAsync(
		Func<DynamoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await InitializeAsync(cancellationToken).ConfigureAwait(false);
		LogStartingCdcProcessor(_options.ProcessorName, _streamArn);

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
			catch (Exception ex)
			{
				LogProcessingError(_options.ProcessorName, ex);
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
		(_stateStore as IDisposable)?.Dispose();
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

		// Check if we have a saved position for this shard
		if (_shardPositions.TryGetValue(shard.ShardId, out var sequenceNumber))
		{
			iteratorRequest.ShardIteratorType = ShardIteratorType.AFTER_SEQUENCE_NUMBER;
			iteratorRequest.SequenceNumber = sequenceNumber;
		}
		else if (_currentPosition.IsBeginning)
		{
			iteratorRequest.ShardIteratorType = ShardIteratorType.TRIM_HORIZON;
		}
		else
		{
			iteratorRequest.ShardIteratorType = ShardIteratorType.LATEST;
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

				// Update iterator for next call
				if (!string.IsNullOrEmpty(response.NextShardIterator))
				{
					_shardIterators[shardId] = response.NextShardIterator;
				}
				else
				{
					// Shard is exhausted
					shardsToRemove.Add(shardId);
				}

				if (response.Records.Count == 0)
				{
					continue;
				}

				LogReceivedBatch(shardId, response.Records.Count);

				foreach (var record in response.Records)
				{
					var changeEvent = CreateChangeEvent(shardId, record);

					await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);

					// Update position for this shard
					_shardPositions[shardId] = record.Dynamodb.SequenceNumber;
					totalProcessed++;
				}

				// Batch checkpoint: save position once per shard batch instead of per-record
				if (autoConfirm && response.Records.Count > 0)
				{
					await ConfirmPositionAsync(
						await GetCurrentPositionAsync(cancellationToken).ConfigureAwait(false),
						cancellationToken).ConfigureAwait(false);
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

		var position = _currentPosition.WithShardPosition(shardId, sequenceNumber);
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
}
