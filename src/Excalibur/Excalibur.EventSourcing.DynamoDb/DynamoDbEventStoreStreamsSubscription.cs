// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Excalibur.Data.Abstractions.CloudNative;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.DynamoDb;

/// <summary>
/// DynamoDB Streams subscription for the event store.
/// </summary>
public sealed class DynamoDbEventStoreStreamsSubscription : IChangeFeedSubscription<CloudStoredEvent>
{
	private readonly IAmazonDynamoDB _client;
	private readonly IAmazonDynamoDBStreams _streamsClient;
	private readonly DynamoDbEventStoreOptions _options;
	private readonly ILogger _logger;
	private readonly CancellationTokenSource _cts = new();
	private readonly Channel<IChangeFeedEvent<CloudStoredEvent>> _channel;
	private readonly Dictionary<string, string> _shardIterators = new();

	private string? _streamArn;
	private bool _isActive;
	private volatile bool _disposed;
	private string? _continuationToken;
	private long _sequenceNumber;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbEventStoreStreamsSubscription" /> class.
	/// </summary>
	/// <param name="client"> The DynamoDB client. </param>
	/// <param name="streamsClient"> The DynamoDB Streams client. </param>
	/// <param name="options"> The event store options. </param>
	/// <param name="logger"> The logger. </param>
	public DynamoDbEventStoreStreamsSubscription(
		IAmazonDynamoDB client,
		IAmazonDynamoDBStreams streamsClient,
		DynamoDbEventStoreOptions options,
		ILogger logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_streamsClient = streamsClient ?? throw new ArgumentNullException(nameof(streamsClient));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		SubscriptionId = $"dynamodb-eventstore-{Guid.NewGuid():N}";

		_channel = Channel.CreateBounded<IChangeFeedEvent<CloudStoredEvent>>(
			new BoundedChannelOptions(_options.MaxBatchSize * 10)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = true,
				SingleWriter = true
			});
	}

	/// <inheritdoc />
	public string SubscriptionId { get; }

	/// <inheritdoc />
	public bool IsActive => _isActive && !_disposed;

	/// <inheritdoc />
	public string? CurrentContinuationToken => _continuationToken;

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(DynamoDbEventStoreStreamsSubscription));
		}

		// Get the stream ARN from the table description
		var tableResponse = await _client.DescribeTableAsync(_options.EventsTableName, cancellationToken)
			.ConfigureAwait(false);

		_streamArn = tableResponse.Table.LatestStreamArn;

		if (string.IsNullOrEmpty(_streamArn))
		{
			throw new InvalidOperationException(
				$"DynamoDB Streams is not enabled on table '{_options.EventsTableName}'");
		}

		_isActive = true;

		_ = PollStreamsAsync(_cts.Token);
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		_isActive = false;
		await _cts.CancelAsync().ConfigureAwait(false);
		_channel.Writer.Complete();
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<IChangeFeedEvent<CloudStoredEvent>> ReadChangesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(DynamoDbEventStoreStreamsSubscription));
		}

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
		var linkedToken = linkedCts.Token;

		while (!linkedToken.IsCancellationRequested && _isActive)
		{
			var items = new List<IChangeFeedEvent<CloudStoredEvent>>();
			var shouldBreak = false;

			try
			{
				var hasItem = await _channel.Reader.WaitToReadAsync(linkedToken).ConfigureAwait(false);
				if (!hasItem)
				{
					shouldBreak = true;
				}
				else
				{
					while (_channel.Reader.TryRead(out var change))
					{
						items.Add(change);
					}
				}
			}
			catch (OperationCanceledException)
			{
				shouldBreak = true;
			}
			catch (ChannelClosedException)
			{
				shouldBreak = true;
			}

			foreach (var item in items)
			{
				yield return item;
			}

			if (shouldBreak)
			{
				yield break;
			}
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isActive = false;

		await _cts.CancelAsync().ConfigureAwait(false);
		_cts.Dispose();
		_channel.Writer.Complete();
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Maintainability",
		"CA1506:Avoid excessive class coupling",
		Justification = "Streams polling coordinates multiple SDK and abstraction types.")]
	private async Task PollStreamsAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested && _isActive && _streamArn != null)
		{
			try
			{
				// Get stream description to find shards
				var describeRequest = new DescribeStreamRequest { StreamArn = _streamArn };

				var describeResponse = await _streamsClient.DescribeStreamAsync(describeRequest, cancellationToken)
					.ConfigureAwait(false);

				foreach (var shard in describeResponse.StreamDescription.Shards)
				{
					if (!_shardIterators.ContainsKey(shard.ShardId))
					{
						// Get iterator for new shard
						var iteratorRequest = new GetShardIteratorRequest
						{
							StreamArn = _streamArn,
							ShardId = shard.ShardId,
							ShardIteratorType = ShardIteratorType.LATEST
						};

						var iteratorResponse = await _streamsClient.GetShardIteratorAsync(iteratorRequest, cancellationToken)
							.ConfigureAwait(false);

						_shardIterators[shard.ShardId] = iteratorResponse.ShardIterator;
					}
				}

				// Process each shard
				foreach (var shardId in _shardIterators.Keys.ToList())
				{
					var iterator = _shardIterators[shardId];
					if (string.IsNullOrEmpty(iterator))
					{
						continue;
					}

					var recordsRequest = new GetRecordsRequest { ShardIterator = iterator, Limit = _options.MaxBatchSize };

					var recordsResponse = await _streamsClient.GetRecordsAsync(recordsRequest, cancellationToken)
						.ConfigureAwait(false);

					foreach (var record in recordsResponse.Records)
					{
						if (record.Dynamodb?.NewImage != null)
						{
							var cloudEvent = ToCloudStoredEvent(record.Dynamodb.NewImage);
							var seqNum = Interlocked.Increment(ref _sequenceNumber);

							var eventType = record.EventName.Value switch
							{
								"INSERT" => ChangeFeedEventType.Created,
								"MODIFY" => ChangeFeedEventType.Updated,
								"REMOVE" => ChangeFeedEventType.Deleted,
								_ => ChangeFeedEventType.Created
							};

							var feedEvent = new DynamoDbStreamsFeedEvent(
								eventType,
								cloudEvent,
								cloudEvent.DocumentId ?? record.EventID,
								new PartitionKey(cloudEvent.PartitionKeyValue ?? string.Empty),
								cloudEvent.Timestamp,
								record.Dynamodb.SequenceNumber,
								seqNum);

							await _channel.Writer.WriteAsync(feedEvent, cancellationToken).ConfigureAwait(false);
						}
					}

					_shardIterators[shardId] = recordsResponse.NextShardIterator ?? string.Empty;
					_continuationToken = recordsResponse.NextShardIterator;
				}

				// Poll interval
				await Task.Delay(_options.StreamsPollIntervalMs, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error polling DynamoDB Streams for subscription {SubscriptionId}", SubscriptionId);
				await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private CloudStoredEvent ToCloudStoredEvent(Dictionary<string, AttributeValue> item)
	{
		return new CloudStoredEvent
		{
			EventId = item["eventId"].S,
			AggregateId = item["aggregateId"].S,
			AggregateType = item["aggregateType"].S,
			EventType = item["eventType"].S,
			Version = long.Parse(item["version"].N),
			Timestamp = DateTimeOffset.Parse(item["timestamp"].S, CultureInfo.InvariantCulture),
			EventData = Convert.FromBase64String(item["eventData"].S),
			Metadata = item.TryGetValue("metadata", out var metaAttr)
					   && !string.IsNullOrEmpty(metaAttr.S)
				? Convert.FromBase64String(metaAttr.S)
				: null,
			PartitionKeyValue = item[_options.PartitionKeyAttribute].S,
			DocumentId = $"{item[_options.PartitionKeyAttribute].S}:{item[_options.SortKeyAttribute].N}",
			IsDispatched = item.TryGetValue("isDispatched", out var dispatchedAttr) && dispatchedAttr.BOOL == true
		};
	}
}

/// <summary>
/// Change feed event for DynamoDB Streams changes.
/// </summary>
internal sealed class DynamoDbStreamsFeedEvent : IChangeFeedEvent<CloudStoredEvent>
{
	public DynamoDbStreamsFeedEvent(
		ChangeFeedEventType eventType,
		CloudStoredEvent? document,
		string documentId,
		IPartitionKey partitionKey,
		DateTimeOffset timestamp,
		string continuationToken,
		long sequenceNumber)
	{
		EventType = eventType;
		Document = document;
		DocumentId = documentId;
		PartitionKey = partitionKey;
		Timestamp = timestamp;
		ContinuationToken = continuationToken;
		SequenceNumber = sequenceNumber;
	}

	/// <inheritdoc />
	public ChangeFeedEventType EventType { get; }

	/// <inheritdoc />
	public CloudStoredEvent? Document { get; }

	/// <inheritdoc />
	public string DocumentId { get; }

	/// <inheritdoc />
	public IPartitionKey PartitionKey { get; }

	/// <inheritdoc />
	public DateTimeOffset Timestamp { get; }

	/// <inheritdoc />
	public string ContinuationToken { get; }

	/// <inheritdoc />
	public long SequenceNumber { get; }
}
