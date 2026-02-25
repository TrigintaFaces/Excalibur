// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;

using Excalibur.Data.Abstractions.CloudNative;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// DynamoDB Streams implementation of change feed subscription.
/// </summary>
/// <typeparam name="TDocument"> The document type. </typeparam>
/// <remarks>
/// This implementation uses the DynamoDB Streams API via <see cref="IAmazonDynamoDBStreams" /> client for real-time change data capture.
/// The table must have streams enabled.
/// </remarks>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Change feed implementations inherently couple with many SDK and abstraction types.")]
public sealed partial class DynamoDbStreamsSubscription<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
TDocument>
	: IChangeFeedSubscription<TDocument>
	where TDocument : class
{
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
	/// Initializes a new instance of the <see cref="DynamoDbStreamsSubscription{TDocument}" /> class.
	/// </summary>
	/// <param name="client"> The DynamoDB client. </param>
	/// <param name="streamsClient"> The DynamoDB Streams client. </param>
	/// <param name="tableName"> The table name. </param>
	/// <param name="options"> The change feed options. </param>
	/// <param name="logger"> The logger. </param>
	public DynamoDbStreamsSubscription(
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

		SubscriptionId = $"streams-{tableName}-{Guid.NewGuid():N}";
	}

	/// <inheritdoc />
	public string SubscriptionId { get; }

	/// <inheritdoc />
	public bool IsActive => _isActive && !_disposed;

	/// <inheritdoc />
	public string? CurrentContinuationToken { get; private set; }

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(DynamoDbStreamsSubscription<>));
		}

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

	/// <inheritdoc />
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

	/// <inheritdoc />
	public async IAsyncEnumerable<IChangeFeedEvent<TDocument>> ReadChangesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(DynamoDbStreamsSubscription<>));
		}

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

				LogReceivedBatch(SubscriptionId, recordsResponse.Records.Count);
				CurrentContinuationToken = recordsResponse.Records.Last().Dynamodb?.SequenceNumber;

				foreach (var record in recordsResponse.Records)
				{
					var eventType = MapEventType(record.EventName);
					TDocument? document = null;

					// Get document from NewImage or OldImage depending on event type
					var image = record.Dynamodb?.NewImage ?? record.Dynamodb?.OldImage;
					if (image != null)
					{
						document = DeserializeDocument(image);
					}

					var documentId = GetDocumentIdFromRecord(record);
					var partitionKey = GetPartitionKeyFromRecord(record);

					yield return new DynamoDbStreamEvent<TDocument>(
						eventType,
						document,
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
	}

	private static ChangeFeedEventType MapEventType(OperationType operationType)
	{
		return operationType.Value switch
		{
			"INSERT" => ChangeFeedEventType.Created,
			"MODIFY" => ChangeFeedEventType.Updated,
			"REMOVE" => ChangeFeedEventType.Deleted,
			_ => ChangeFeedEventType.Updated
		};
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private static TDocument? DeserializeDocument(
		Dictionary<string, AttributeValue> item)
	{
		var converted = DynamoDbAttributeValueConverter.ToAttributeValueMap(item);
		if (converted is null)
		{
			return null;
		}

		var doc = Document.FromAttributeMap(converted);
		var json = doc.ToJson();
		return JsonSerializer.Deserialize<TDocument>(json);
	}

	private static string GetDocumentIdFromRecord(Record record)
	{
		var keys = record.Dynamodb?.Keys;
		if (keys == null || keys.Count == 0)
		{
			return Guid.NewGuid().ToString();
		}

		// Try to get sort key (sk) first, then partition key (pk)
		if (keys.TryGetValue("sk", out var skValue))
		{
			return skValue.S ?? skValue.N ?? Guid.NewGuid().ToString();
		}

		if (keys.TryGetValue("pk", out var pkValue))
		{
			return pkValue.S ?? pkValue.N ?? Guid.NewGuid().ToString();
		}

		return keys.Values.FirstOrDefault()?.S ?? Guid.NewGuid().ToString();
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
/// DynamoDB Stream event implementation.
/// </summary>
/// <typeparam name="TDocument"> The document type. </typeparam>
public sealed class DynamoDbStreamEvent<TDocument> : IChangeFeedEvent<TDocument>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbStreamEvent{TDocument}" /> class.
	/// </summary>
	public DynamoDbStreamEvent(
		ChangeFeedEventType eventType,
		TDocument? document,
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
	public TDocument? Document { get; }

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
