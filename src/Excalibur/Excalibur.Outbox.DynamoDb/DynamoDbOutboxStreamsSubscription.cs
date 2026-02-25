// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Excalibur.Data.Abstractions.CloudNative;

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.DynamoDb;

/// <summary>
/// DynamoDB Streams subscription specialized for outbox messages.
/// Only emits unpublished messages (new inserts that are not yet published).
/// </summary>
/// <remarks>
/// This subscription filters stream events to only emit unpublished outbox messages,
/// making it suitable for push-based outbox processing. Published messages and updates
/// are filtered out.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Change feed implementations inherently couple with many SDK and abstraction types.")]
public sealed partial class DynamoDbOutboxStreamsSubscription : IChangeFeedSubscription<CloudOutboxMessage>
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private readonly IAmazonDynamoDB _client;
	private readonly IAmazonDynamoDBStreams _streamsClient;
	private readonly string _tableName;
	private readonly IChangeFeedOptions _options;
	private readonly ILogger _logger;
	private readonly CancellationTokenSource _cts = new();

	private string? _streamArn;
	private bool _isActive;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbOutboxStreamsSubscription"/> class.
	/// </summary>
	/// <param name="client">The DynamoDB client.</param>
	/// <param name="streamsClient">The DynamoDB Streams client.</param>
	/// <param name="tableName">The table name.</param>
	/// <param name="options">The change feed options.</param>
	/// <param name="logger">The logger.</param>
	public DynamoDbOutboxStreamsSubscription(
		IAmazonDynamoDB client,
		IAmazonDynamoDBStreams streamsClient,
		string tableName,
		IChangeFeedOptions options,
		ILogger logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_streamsClient = streamsClient ?? throw new ArgumentNullException(nameof(streamsClient));
		_tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		SubscriptionId = $"outbox-streams-{tableName}-{Guid.NewGuid():N}";
	}

	/// <inheritdoc/>
	public string SubscriptionId { get; }

	/// <inheritdoc/>
	public bool IsActive => _isActive && !_disposed;

	/// <inheritdoc/>
	public string? CurrentContinuationToken { get; private set; }

	/// <inheritdoc/>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		LogStarting(SubscriptionId);

		// Get stream ARN from table
		var describeResponse = await _client.DescribeTableAsync(_tableName, cancellationToken).ConfigureAwait(false);
		_streamArn = describeResponse.Table.LatestStreamArn;

		if (string.IsNullOrEmpty(_streamArn))
		{
			throw new InvalidOperationException($"Table '{_tableName}' does not have streams enabled.");
		}

		_isActive = true;
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		LogStopping(SubscriptionId);
		_isActive = false;
		await _cts.CancelAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async IAsyncEnumerable<IChangeFeedEvent<CloudOutboxMessage>> ReadChangesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (string.IsNullOrEmpty(_streamArn))
		{
			throw new InvalidOperationException("Subscription not started. Call StartAsync first.");
		}

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
		var linkedToken = linkedCts.Token;

		// Get shards using the Streams client
		var describeStreamRequest = new DescribeStreamRequest { StreamArn = _streamArn };

		DescribeStreamResponse? streamDescription = null;
		try
		{
			streamDescription = await _streamsClient.DescribeStreamAsync(describeStreamRequest, linkedToken)
				.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			yield break;
		}

		var shards = streamDescription.StreamDescription.Shards;
		var shardIterators = new Dictionary<string, string>();

		// Get shard iterators for each shard
		foreach (var shard in shards)
		{
			var iteratorRequest = new GetShardIteratorRequest
			{
				StreamArn = _streamArn,
				ShardId = shard.ShardId,
				ShardIteratorType = GetShardIteratorType()
			};

			var iteratorResponse = await _streamsClient.GetShardIteratorAsync(iteratorRequest, linkedToken)
				.ConfigureAwait(false);
			shardIterators[shard.ShardId] = iteratorResponse.ShardIterator;
		}

		long sequenceNumber = 0;

		while (_isActive && !linkedToken.IsCancellationRequested && shardIterators.Count > 0)
		{
			foreach (var shardId in shardIterators.Keys.ToList())
			{
				var iterator = shardIterators[shardId];
				if (string.IsNullOrEmpty(iterator))
				{
					_ = shardIterators.Remove(shardId);
					continue;
				}

				GetRecordsResponse? recordsResponse = null;
				try
				{
					var recordsRequest = new GetRecordsRequest { ShardIterator = iterator, Limit = _options.MaxBatchSize };

					recordsResponse = await _streamsClient.GetRecordsAsync(recordsRequest, linkedToken)
						.ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					yield break;
				}
				catch (ExpiredIteratorException)
				{
					// Shard iterator expired, remove and continue
					_ = shardIterators.Remove(shardId);
					continue;
				}

				// Update iterator for next call
				if (!string.IsNullOrEmpty(recordsResponse.NextShardIterator))
				{
					shardIterators[shardId] = recordsResponse.NextShardIterator;
				}
				else
				{
					_ = shardIterators.Remove(shardId);
				}

				if (recordsResponse.Records.Count == 0)
				{
					continue;
				}

				// Filter for unpublished messages only
				var unpublishedRecords = recordsResponse.Records
					.Where(IsUnpublishedInsert)
					.ToList();

				LogReceivedBatch(SubscriptionId, recordsResponse.Records.Count, unpublishedRecords.Count);

				if (unpublishedRecords.Count > 0)
				{
					CurrentContinuationToken = unpublishedRecords.Last().Dynamodb?.SequenceNumber;
				}

				foreach (var record in unpublishedRecords)
				{
					var image = record.Dynamodb?.NewImage;
					if (image == null)
					{
						continue;
					}

					var message = FromAttributeMap(image);
					var partitionKey = GetPartitionKeyFromRecord(record);
					var documentId = message.MessageId;

					yield return new DynamoDbOutboxStreamEvent(
						ChangeFeedEventType.Created,
						message,
						documentId,
						partitionKey,
						record.Dynamodb?.ApproximateCreationDateTime ?? DateTimeOffset.UtcNow,
						record.Dynamodb?.SequenceNumber ?? string.Empty,
						sequenceNumber++);
				}
			}

			// Wait before polling again
			if (_isActive && !linkedToken.IsCancellationRequested && shardIterators.Count > 0)
			{
				try
				{
					await Task.Delay(_options.PollingInterval, linkedToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					yield break;
				}
			}
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
		_isActive = false;
		await _cts.CancelAsync().ConfigureAwait(false);
		_cts.Dispose();
	}

	private static bool IsUnpublishedInsert(Record record)
	{
		// Only process INSERT events (new messages)
		if (record.EventName.Value != "INSERT")
		{
			return false;
		}

		// Check if message is unpublished
		var newImage = record.Dynamodb?.NewImage;
		if (newImage == null)
		{
			return false;
		}

		// Message must have isPublished = false
		if (newImage.TryGetValue("isPublished", out var isPublished))
		{
			return isPublished.BOOL != true;
		}

		// Default to unpublished if field not present
		return true;
	}

	private static IPartitionKey GetPartitionKeyFromRecord(Record record)
	{
		var keys = record.Dynamodb?.Keys;
		if (keys == null || keys.Count == 0)
		{
			return new PartitionKey(string.Empty);
		}

		if (keys.TryGetValue("pk", out var pkValue))
		{
			return new PartitionKey(pkValue.S ?? pkValue.N ?? string.Empty);
		}

		return new PartitionKey(keys.Values.FirstOrDefault()?.S ?? string.Empty);
	}

	private static CloudOutboxMessage FromAttributeMap(Dictionary<string, AttributeValue> item)
	{
		return new CloudOutboxMessage
		{
			MessageId = item.TryGetValue("sk", out var sk) ? sk.S : Guid.NewGuid().ToString(),
			MessageType = item.TryGetValue("messageType", out var msgType) ? msgType.S : string.Empty,
			Payload = item.TryGetValue("payload", out var payload) && !string.IsNullOrEmpty(payload.S)
				? Convert.FromBase64String(payload.S)
				: [],
			Headers = item.TryGetValue("headers", out var headers) && !string.IsNullOrEmpty(headers.S)
				? JsonSerializer.Deserialize<Dictionary<string, string>>(headers.S, JsonOptions)
				: null,
			AggregateId = item.TryGetValue("aggregateId", out var aggId) ? aggId.S : null,
			AggregateType = item.TryGetValue("aggregateType", out var aggType) ? aggType.S : null,
			CorrelationId = item.TryGetValue("correlationId", out var corrId) ? corrId.S : null,
			CausationId = item.TryGetValue("causationId", out var causId) ? causId.S : null,
			CreatedAt = item.TryGetValue("createdAt", out var created) && !string.IsNullOrEmpty(created.S)
				? DateTimeOffset.Parse(created.S, CultureInfo.InvariantCulture)
				: DateTimeOffset.UtcNow,
			PublishedAt = item.TryGetValue("publishedAt", out var pubAt) && !string.IsNullOrEmpty(pubAt.S)
				? DateTimeOffset.Parse(pubAt.S, CultureInfo.InvariantCulture)
				: null,
			RetryCount = item.TryGetValue("retryCount", out var retry) && !string.IsNullOrEmpty(retry.N)
				? int.Parse(retry.N)
				: 0,
			LastError = item.TryGetValue("lastError", out var err) ? err.S : null,
			PartitionKeyValue = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty
		};
	}

	private ShardIteratorType GetShardIteratorType()
	{
		return _options.StartPosition switch
		{
			ChangeFeedStartPosition.Beginning => ShardIteratorType.TRIM_HORIZON,
			ChangeFeedStartPosition.Now => ShardIteratorType.LATEST,
			ChangeFeedStartPosition.FromContinuationToken when !string.IsNullOrEmpty(_options.ContinuationToken) =>
				ShardIteratorType.AFTER_SEQUENCE_NUMBER,
			_ => ShardIteratorType.LATEST
		};
	}
}

/// <summary>
/// DynamoDB Stream event for outbox messages.
/// </summary>
public sealed class DynamoDbOutboxStreamEvent : IChangeFeedEvent<CloudOutboxMessage>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbOutboxStreamEvent"/> class.
	/// </summary>
	public DynamoDbOutboxStreamEvent(
		ChangeFeedEventType eventType,
		CloudOutboxMessage? document,
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

	/// <inheritdoc/>
	public ChangeFeedEventType EventType { get; }

	/// <inheritdoc/>
	public CloudOutboxMessage? Document { get; }

	/// <inheritdoc/>
	public string DocumentId { get; }

	/// <inheritdoc/>
	public IPartitionKey PartitionKey { get; }

	/// <inheritdoc/>
	public DateTimeOffset Timestamp { get; }

	/// <inheritdoc/>
	public string ContinuationToken { get; }

	/// <inheritdoc/>
	public long SequenceNumber { get; }
}
