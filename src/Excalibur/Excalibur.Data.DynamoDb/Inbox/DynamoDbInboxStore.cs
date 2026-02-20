// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.DynamoDb.Inbox;

/// <summary>
/// DynamoDB implementation of <see cref="IInboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides message deduplication using DynamoDB's conditional writes.
/// Uses a composite key of (handler_type, message_id) for unique message identification.
/// </para>
/// <para>
/// The atomic first-writer-wins pattern is achieved using conditional PutItem with
/// attribute_not_exists condition.
/// </para>
/// </remarks>
public sealed partial class DynamoDbInboxStore : IInboxStore, IAsyncDisposable, IDisposable
{
	private readonly DynamoDbInboxOptions _options;
	private readonly ILogger<DynamoDbInboxStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private IAmazonDynamoDB? _client;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbInboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbInboxStore(
		IOptions<DynamoDbInboxOptions> options,
		ILogger<DynamoDbInboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbInboxStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">The DynamoDB client.</param>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbInboxStore(
		IAmazonDynamoDB client,
		IOptions<DynamoDbInboxOptions> options,
		ILogger<DynamoDbInboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_logger = logger;
		_initialized = true;
	}

	/// <summary>
	/// Initializes the DynamoDB client.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async ValueTask InitializeAsync(CancellationToken cancellationToken)
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

			_client = CreateClient();

			// Verify connectivity by describing the table
			_ = await _client.DescribeTableAsync(_options.TableName, cancellationToken).ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
		ArgumentNullException.ThrowIfNull(payload);
		ArgumentNullException.ThrowIfNull(metadata);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);

		var item = CreateItemFromEntry(entry);

		var request = new PutItemRequest
		{
			TableName = _options.TableName,
			Item = item,
			ConditionExpression = $"attribute_not_exists({_options.PartitionKeyAttribute})"
		};

		try
		{
			_ = await _client.PutItemAsync(request, cancellationToken).ConfigureAwait(false);
			LogCreatedEntry(messageId, handlerType);
			return entry;
		}
		catch (ConditionalCheckFailedException)
		{
			throw new InvalidOperationException(
				$"Inbox entry already exists for message '{messageId}' and handler '{handlerType}'.");
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var key = CreateKey(messageId, handlerType);
		var now = DateTimeOffset.UtcNow;

		// First check if entry exists and its current status
		var getRequest = new GetItemRequest { TableName = _options.TableName, Key = key, ConsistentRead = _options.UseConsistentReads };

		var getResponse = await _client.GetItemAsync(getRequest, cancellationToken).ConfigureAwait(false);

		if (getResponse.Item == null || getResponse.Item.Count == 0)
		{
			throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
		}

		if (getResponse.Item.TryGetValue("status", out var statusAttr) &&
			int.Parse(statusAttr.N) == (int)InboxStatus.Processed)
		{
			throw new InvalidOperationException(
				$"Message '{messageId}' for handler '{handlerType}' is already marked as processed.");
		}

		var updateRequest = new UpdateItemRequest
		{
			TableName = _options.TableName,
			Key = key,
			UpdateExpression = "SET #status = :status, processed_at = :processed_at",
			ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "status" },
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":status"] = new() { N = ((int)InboxStatus.Processed).ToString() },
				[":processed_at"] = new() { S = now.ToString("O") }
			}
		};

		_ = await _client.UpdateItemAsync(updateRequest, cancellationToken).ConfigureAwait(false);
		LogMarkedProcessed(messageId, handlerType);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;

		// Create a minimal item for atomic first-writer-wins using conditional PutItem
		var item = new Dictionary<string, AttributeValue>
		{
			[_options.PartitionKeyAttribute] = new() { S = handlerType },
			[_options.SortKeyAttribute] = new() { S = messageId },
			["message_type"] = new() { S = string.Empty },
			["payload"] = new() { B = new MemoryStream([]) },
			["status"] = new() { N = ((int)InboxStatus.Processed).ToString() },
			["received_at"] = new() { S = now.ToString("O") },
			["processed_at"] = new() { S = now.ToString("O") },
			["retry_count"] = new() { N = "0" }
		};

		var request = new PutItemRequest
		{
			TableName = _options.TableName,
			Item = item,
			ConditionExpression = $"attribute_not_exists({_options.PartitionKeyAttribute})"
		};

		try
		{
			_ = await _client.PutItemAsync(request, cancellationToken).ConfigureAwait(false);
			LogFirstProcessor(messageId, handlerType);
			return true;
		}
		catch (ConditionalCheckFailedException)
		{
			// Item already exists - another processor got there first
			LogDuplicateDetected(messageId, handlerType);
			return false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var key = CreateKey(messageId, handlerType);

		var request = new GetItemRequest
		{
			TableName = _options.TableName,
			Key = key,
			ConsistentRead = _options.UseConsistentReads,
			ProjectionExpression = "#status",
			ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "status" }
		};

		var response = await _client.GetItemAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Item == null || response.Item.Count == 0)
		{
			return false;
		}

		return response.Item.TryGetValue("status", out var statusAttr) &&
			   int.Parse(statusAttr.N) == (int)InboxStatus.Processed;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var key = CreateKey(messageId, handlerType);

		var request = new GetItemRequest { TableName = _options.TableName, Key = key, ConsistentRead = _options.UseConsistentReads };

		var response = await _client.GetItemAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Item == null || response.Item.Count == 0)
		{
			return null;
		}

		return ItemToEntry(response.Item);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var key = CreateKey(messageId, handlerType);
		var now = DateTimeOffset.UtcNow;

		var updateRequest = new UpdateItemRequest
		{
			TableName = _options.TableName,
			Key = key,
			UpdateExpression =
				"SET #status = :status, last_error = :error, last_attempt_at = :attempt, retry_count = retry_count + :inc",
			ConditionExpression = $"attribute_exists({_options.PartitionKeyAttribute})",
			ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "status" },
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":status"] = new() { N = ((int)InboxStatus.Failed).ToString() },
				[":error"] = new() { S = errorMessage },
				[":attempt"] = new() { S = now.ToString("O") },
				[":inc"] = new() { N = "1" }
			}
		};

		try
		{
			_ = await _client.UpdateItemAsync(updateRequest, cancellationToken).ConfigureAwait(false);
			LogMarkedFailed(messageId, handlerType, errorMessage);
		}
		catch (ConditionalCheckFailedException)
		{
			// Entry doesn't exist - nothing to mark as failed
		}
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// DynamoDB requires a Scan for this query pattern (filtering by status across all partitions)
		var filterExpressions = new List<string> { "#status = :failed" };
		var expressionValues = new Dictionary<string, AttributeValue> { [":failed"] = new() { N = ((int)InboxStatus.Failed).ToString() } };

		if (maxRetries > 0)
		{
			filterExpressions.Add("retry_count < :maxRetries");
			expressionValues[":maxRetries"] = new() { N = maxRetries.ToString() };
		}

		if (olderThan.HasValue)
		{
			filterExpressions.Add("last_attempt_at < :olderThan");
			expressionValues[":olderThan"] = new() { S = olderThan.Value.ToString("O") };
		}

		var request = new ScanRequest
		{
			TableName = _options.TableName,
			FilterExpression = string.Join(" AND ", filterExpressions),
			ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "status" },
			ExpressionAttributeValues = expressionValues,
			Limit = batchSize
		};

		var results = new List<InboxEntry>();
		ScanResponse? response = null;

		do
		{
			if (response?.LastEvaluatedKey?.Count > 0)
			{
				request.ExclusiveStartKey = response.LastEvaluatedKey;
			}

			response = await _client.ScanAsync(request, cancellationToken).ConfigureAwait(false);

			foreach (var item in response.Items)
			{
				if (results.Count >= batchSize)
				{
					break;
				}

				results.Add(ItemToEntry(item));
			}
		} while (results.Count < batchSize && response.LastEvaluatedKey?.Count > 0);

		return results.OrderBy(e => e.RetryCount).ThenBy(e => e.LastAttemptAt);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var request = new ScanRequest { TableName = _options.TableName };
		var results = new List<InboxEntry>();
		ScanResponse? response = null;

		do
		{
			if (response?.LastEvaluatedKey?.Count > 0)
			{
				request.ExclusiveStartKey = response.LastEvaluatedKey;
			}

			response = await _client.ScanAsync(request, cancellationToken).ConfigureAwait(false);
			results.AddRange(response.Items.Select(ItemToEntry));
		} while (response.LastEvaluatedKey?.Count > 0);

		return results;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// DynamoDB doesn't support aggregations natively, so we need to scan
		var request = new ScanRequest
		{
			TableName = _options.TableName,
			ProjectionExpression = "#status",
			ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "status" }
		};

		var total = 0;
		var processed = 0;
		var failed = 0;
		var pending = 0;
		ScanResponse? response = null;

		do
		{
			if (response?.LastEvaluatedKey?.Count > 0)
			{
				request.ExclusiveStartKey = response.LastEvaluatedKey;
			}

			response = await _client.ScanAsync(request, cancellationToken).ConfigureAwait(false);

			foreach (var item in response.Items)
			{
				total++;

				if (item.TryGetValue("status", out var statusAttr))
				{
					var status = (InboxStatus)int.Parse(statusAttr.N);
					switch (status)
					{
						case InboxStatus.Processed:
							processed++;
							break;

						case InboxStatus.Failed:
							failed++;
							break;

						case InboxStatus.Received:
						case InboxStatus.Processing:
							pending++;
							break;

						default:
							break;
					}
				}
			}
		} while (response.LastEvaluatedKey?.Count > 0);

		return new InboxStatistics { TotalEntries = total, ProcessedEntries = processed, FailedEntries = failed, PendingEntries = pending };
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var cutoffDate = DateTimeOffset.UtcNow - retentionPeriod;

		// First, find all processed entries older than cutoff
		var scanRequest = new ScanRequest
		{
			TableName = _options.TableName,
			FilterExpression = "#status = :processed AND processed_at < :cutoff",
			ProjectionExpression = $"{_options.PartitionKeyAttribute}, {_options.SortKeyAttribute}",
			ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "status" },
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":processed"] = new() { N = ((int)InboxStatus.Processed).ToString() },
				[":cutoff"] = new() { S = cutoffDate.ToString("O") }
			}
		};

		var itemsToDelete = new List<Dictionary<string, AttributeValue>>();
		ScanResponse? scanResponse = null;

		do
		{
			if (scanResponse?.LastEvaluatedKey?.Count > 0)
			{
				scanRequest.ExclusiveStartKey = scanResponse.LastEvaluatedKey;
			}

			scanResponse = await _client.ScanAsync(scanRequest, cancellationToken).ConfigureAwait(false);
			itemsToDelete.AddRange(scanResponse.Items);
		} while (scanResponse.LastEvaluatedKey?.Count > 0);

		// Delete in batches of 25 (DynamoDB limit)
		var deletedCount = 0;
		const int batchSize = 25;

		for (var i = 0; i < itemsToDelete.Count; i += batchSize)
		{
			var batch = itemsToDelete.Skip(i).Take(batchSize).ToList();
			var writeRequests = batch.Select(item => new WriteRequest
			{
				DeleteRequest = new DeleteRequest
				{
					Key = new Dictionary<string, AttributeValue>
					{
						[_options.PartitionKeyAttribute] = item[_options.PartitionKeyAttribute],
						[_options.SortKeyAttribute] = item[_options.SortKeyAttribute]
					}
				}
			}).ToList();

			var batchRequest = new BatchWriteItemRequest
			{
				RequestItems = new Dictionary<string, List<WriteRequest>> { [_options.TableName] = writeRequests }
			};

			var batchResponse = await _client.BatchWriteItemAsync(batchRequest, cancellationToken)
				.ConfigureAwait(false);

			deletedCount += writeRequests.Count - (batchResponse.UnprocessedItems.TryGetValue(_options.TableName, out var unprocessed)
				? unprocessed.Count
				: 0);
		}

		LogCleanedUp(deletedCount, cutoffDate);
		return deletedCount;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_client?.Dispose();
		_initLock.Dispose();
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_client?.Dispose();
		_initLock.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private Dictionary<string, AttributeValue> CreateKey(string messageId, string handlerType) =>
		new()
		{
			[_options.PartitionKeyAttribute] = new AttributeValue { S = handlerType },
			[_options.SortKeyAttribute] = new AttributeValue { S = messageId }
		};

	private Dictionary<string, AttributeValue> CreateItemFromEntry(InboxEntry entry)
	{
		var item = new Dictionary<string, AttributeValue>
		{
			[_options.PartitionKeyAttribute] = new() { S = entry.HandlerType },
			[_options.SortKeyAttribute] = new() { S = entry.MessageId },
			["message_type"] = new() { S = entry.MessageType },
			["payload"] = new() { B = new MemoryStream(entry.Payload) },
			["status"] = new() { N = ((int)entry.Status).ToString() },
			["received_at"] = new() { S = entry.ReceivedAt.ToString("O") },
			["retry_count"] = new() { N = entry.RetryCount.ToString() }
		};

		// Add metadata as a map
		if (entry.Metadata.Count > 0)
		{
			item["metadata"] = new AttributeValue
			{
				M = entry.Metadata.ToDictionary(
					kvp => kvp.Key,
					kvp => new AttributeValue { S = kvp.Value?.ToString() ?? string.Empty })
			};
		}

		return item;
	}

	private InboxEntry ItemToEntry(Dictionary<string, AttributeValue> item)
	{
		// Extract metadata first for the object initializer
		IDictionary<string, object> metadata = new Dictionary<string, object>(StringComparer.Ordinal);
		if (item.TryGetValue("metadata", out var m) && m.M != null)
		{
			metadata = m.M.ToDictionary(
				kvp => kvp.Key,
				kvp => (object)kvp.Value.S);
		}

		var entry = new InboxEntry
		{
			MessageId = item[_options.SortKeyAttribute].S,
			HandlerType = item[_options.PartitionKeyAttribute].S,
			MessageType = item.TryGetValue("message_type", out var mt) ? mt.S : string.Empty,
			Payload = item.TryGetValue("payload", out var p) ? p.B.ToArray() : [],
			Status = item.TryGetValue("status", out var s) ? (InboxStatus)int.Parse(s.N) : InboxStatus.Received,
			ReceivedAt = item.TryGetValue("received_at", out var ra) ? DateTimeOffset.Parse(ra.S, CultureInfo.InvariantCulture) : DateTimeOffset.UtcNow,
			RetryCount = item.TryGetValue("retry_count", out var rc) ? int.Parse(rc.N) : 0,
			ProcessedAt = item.TryGetValue("processed_at", out var pa) && !string.IsNullOrEmpty(pa.S)
				? DateTimeOffset.Parse(pa.S, CultureInfo.InvariantCulture)
				: null,
			LastAttemptAt = item.TryGetValue("last_attempt_at", out var la) && !string.IsNullOrEmpty(la.S)
				? DateTimeOffset.Parse(la.S, CultureInfo.InvariantCulture)
				: null,
			LastError = item.TryGetValue("last_error", out var le) && !string.IsNullOrEmpty(le.S)
				? le.S
				: null,
			Metadata = metadata
		};

		return entry;
	}

	private IAmazonDynamoDB CreateClient()
	{
		var config = new AmazonDynamoDBConfig
		{
			Timeout = TimeSpan.FromSeconds(_options.TimeoutInSeconds),
			MaxErrorRetry = _options.MaxRetryAttempts
		};

		if (!string.IsNullOrWhiteSpace(_options.ServiceUrl))
		{
			config.ServiceURL = _options.ServiceUrl;
		}
		else if (_options.GetRegionEndpoint() is { } region)
		{
			config.RegionEndpoint = region;
		}

		if (!string.IsNullOrWhiteSpace(_options.AccessKey) && !string.IsNullOrWhiteSpace(_options.SecretKey))
		{
			var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
			return new AmazonDynamoDBClient(credentials, config);
		}

		return new AmazonDynamoDBClient(config);
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}
