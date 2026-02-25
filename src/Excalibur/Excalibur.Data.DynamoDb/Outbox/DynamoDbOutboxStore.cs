// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.Data.DynamoDb.Diagnostics;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace Excalibur.Data.DynamoDb.Outbox;

/// <summary>
/// DynamoDB implementation of <see cref="IOutboxStore"/> using single-table design.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses single-table design with status-based partition keys
/// for efficient status-based queries. Atomic operations are achieved using
/// TransactWriteItems with conditional expressions.
/// </para>
/// <para>
/// Key structure:
/// <list type="bullet">
/// <item><description>PK: OUTBOX#{status}</description></item>
/// <item><description>SK: {priority}#{createdAt}#{messageId}</description></item>
/// <item><description>GSI1: MSG#{messageId} for point lookups</description></item>
/// <item><description>GSI2: SCHEDULED for scheduled message retrieval</description></item>
/// </list>
/// </para>
/// </remarks>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Outbox stores inherently couple with many SDK and abstraction types.")]
public sealed partial class DynamoDbOutboxStore : IOutboxStore, IOutboxStoreAdmin, IAsyncDisposable, IDisposable
{
	private readonly DynamoDbOutboxOptions _options;
	private readonly ILogger<DynamoDbOutboxStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private readonly bool _ownsClient;
	private IAmazonDynamoDB? _client;
	private bool _initialized;

	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbOutboxStore(
		IOptions<DynamoDbOutboxOptions> options,
		ILogger<DynamoDbOutboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;

		_ownsClient = true; // We create the client, so we own it
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbOutboxStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">The DynamoDB client.</param>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbOutboxStore(
		IAmazonDynamoDB client,
		IOptions<DynamoDbOutboxOptions> options,
		ILogger<DynamoDbOutboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_logger = logger;
		_initialized = true;

		_ownsClient = false; // Client is externally provided, don't dispose it
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

			if (_options.CreateTableIfNotExists)
			{
				await EnsureTableExistsAsync(cancellationToken).ConfigureAwait(false);
			}

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc/>
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrWhiteSpace(message.Id);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// First, check if message with this ID already exists (using GSI1)
		var existsQuery = new QueryRequest
		{
			TableName = _options.TableName,
			IndexName = _options.GSI1IndexName,
			KeyConditionExpression = $"{DynamoDbOutboxDocument.GSI1PK} = :gsi1pk",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":gsi1pk"] = new() { S = DynamoDbOutboxDocument.CreateGSI1PK(message.Id) }
			},
			Limit = 1,
			Select = Select.COUNT
		};

		var existsResponse = await _client.QueryAsync(existsQuery, cancellationToken).ConfigureAwait(false);
		if (existsResponse.Count > 0)
		{
			throw new InvalidOperationException(
				$"Message with ID '{message.Id}' already exists.");
		}

		var item = DynamoDbOutboxDocument.FromOutboundMessage(message);

		var request = new PutItemRequest { TableName = _options.TableName, Item = item, ConditionExpression = "attribute_not_exists(PK)" };

		try
		{
			_ = await _client.PutItemAsync(request, cancellationToken).ConfigureAwait(false);
			LogMessageStaged(message.Id, message.MessageType, message.Priority);
		}
		catch (ConditionalCheckFailedException)
		{
			throw new InvalidOperationException(
				$"Message with ID '{message.Id}' already exists.");
		}
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = message.GetType().FullName ?? message.GetType().Name;
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());

		var outboundMessage = new OutboundMessage(messageType, payload, messageType)
		{
			CorrelationId = context.CorrelationId, CausationId = context.CausationId
		};

		await StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);

		LogMessageEnqueued(outboundMessage.Id, messageType);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0.");
		}

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var stagedPK = DynamoDbOutboxDocument.CreatePK(OutboxStatus.Staged);
		var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

		// Query for staged messages that are either not scheduled or scheduled for now or earlier
		var request = new QueryRequest
		{
			TableName = _options.TableName,
			KeyConditionExpression = "PK = :pk",
			FilterExpression = "attribute_not_exists(scheduledAt) OR scheduledAt <= :now",
			ExpressionAttributeValues =
				new Dictionary<string, AttributeValue> { [":pk"] = new() { S = stagedPK }, [":now"] = new() { S = now } },
			Limit = batchSize,
			ConsistentRead = _options.UseConsistentReads
		};

		var results = new List<OutboundMessage>();

		var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);
		foreach (var item in response.Items)
		{
			if (results.Count >= batchSize)
			{
				break;
			}

			results.Add(DynamoDbOutboxDocument.ToOutboundMessage(item));
		}

		return results;
	}

	/// <inheritdoc/>
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// First, find the message by ID using GSI1
		var existingItem = await FindMessageByIdAsync(messageId, cancellationToken).ConfigureAwait(false)
		                   ?? throw new InvalidOperationException($"Message with ID '{messageId}' not found.");

		var currentStatus = (OutboxStatus)int.Parse(existingItem[DynamoDbOutboxDocument.Status].N, CultureInfo.InvariantCulture);
		if (currentStatus == OutboxStatus.Sent)
		{
			throw new InvalidOperationException($"Message with ID '{messageId}' is already marked as sent.");
		}

		// Use TransactWriteItems for atomic status transition
		var oldPK = existingItem[DynamoDbOutboxDocument.PK].S;
		var oldSK = existingItem[DynamoDbOutboxDocument.SK].S;
		var ttlSeconds = _options.SentMessageTtlSeconds > 0 ? _options.SentMessageTtlSeconds : 0;
		var newItem = DynamoDbOutboxDocument.WithStatus(existingItem, OutboxStatus.Sent, ttlSeconds);

		var transactRequest = new TransactWriteItemsRequest
		{
			TransactItems =
			[
				new TransactWriteItem
				{
					Delete = new Delete
					{
						TableName = _options.TableName,
						Key = new Dictionary<string, AttributeValue>
						{
							[DynamoDbOutboxDocument.PK] = new() { S = oldPK }, [DynamoDbOutboxDocument.SK] = new() { S = oldSK }
						},
						ConditionExpression = "attribute_exists(PK)"
					}
				},
				new TransactWriteItem
				{
					Put = new Put { TableName = _options.TableName, Item = newItem, ConditionExpression = "attribute_not_exists(PK)" }
				}
			]
		};

		try
		{
			_ = await _client.TransactWriteItemsAsync(transactRequest, cancellationToken).ConfigureAwait(false);
			LogMessageSent(messageId);
		}
		catch (TransactionCanceledException ex)

		{
			// Check cancellation reasons
			var deleteReason = ex.CancellationReasons?.ElementAtOrDefault(0)?.Code;
			if (deleteReason == "ConditionalCheckFailed")
			{
				throw new InvalidOperationException($"Message with ID '{messageId}' is already marked as sent.", ex);
			}

			throw;
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Find the message - could be in Staged or Failed partition
		var existingItem = await FindMessageByIdAsync(messageId, cancellationToken).ConfigureAwait(false);

		if (existingItem == null)

		{
			// Message doesn't exist - silent return per conformance tests
			return;
		}

		var oldPK = existingItem[DynamoDbOutboxDocument.PK].S;
		var oldSK = existingItem[DynamoDbOutboxDocument.SK].S;
		var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
		var currentStatus = (OutboxStatus)int.Parse(existingItem[DynamoDbOutboxDocument.Status].N, CultureInfo.InvariantCulture);

		// If already in Failed status, just update in place (no partition change needed)
		if (currentStatus == OutboxStatus.Failed)
		{
			var updateRequest = new UpdateItemRequest
			{
				TableName = _options.TableName,
				Key =
					new Dictionary<string, AttributeValue>
					{
						[DynamoDbOutboxDocument.PK] = new() { S = oldPK }, [DynamoDbOutboxDocument.SK] = new() { S = oldSK }
					},
				UpdateExpression = "SET #lastError = :lastError, #retryCount = :retryCount, #lastAttemptAt = :lastAttemptAt",
				ExpressionAttributeNames =
					new Dictionary<string, string>
					{
						["#lastError"] = DynamoDbOutboxDocument.LastError,
						["#retryCount"] = DynamoDbOutboxDocument.RetryCount,
						["#lastAttemptAt"] = DynamoDbOutboxDocument.LastAttemptAt
					},
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":lastError"] = new() { S = errorMessage },
					[":retryCount"] = new() { N = retryCount.ToString(CultureInfo.InvariantCulture) },
					[":lastAttemptAt"] = new() { S = now }
				},
				ConditionExpression = "attribute_exists(PK)"
			};

			try
			{
				_ = await _client.UpdateItemAsync(updateRequest, cancellationToken).ConfigureAwait(false);
				LogMessageFailed(messageId, errorMessage, retryCount);
			}
			catch (ConditionalCheckFailedException)
			{
				LogConcurrencyConflict(messageId, "MarkFailed");
			}

			return;
		}

		// Status is changing - use TransactWriteItems for atomic transition
		var newItem = DynamoDbOutboxDocument.WithStatus(existingItem, OutboxStatus.Failed);
		newItem[DynamoDbOutboxDocument.LastError] = new() { S = errorMessage };
		newItem[DynamoDbOutboxDocument.RetryCount] = new() { N = retryCount.ToString(CultureInfo.InvariantCulture) };
		newItem[DynamoDbOutboxDocument.LastAttemptAt] = new() { S = now };

		var transactRequest = new TransactWriteItemsRequest
		{
			TransactItems =
			[
				new TransactWriteItem
				{
					Delete = new Delete
					{
						TableName = _options.TableName,
						Key = new Dictionary<string, AttributeValue>
						{
							[DynamoDbOutboxDocument.PK] = new() { S = oldPK }, [DynamoDbOutboxDocument.SK] = new() { S = oldSK }
						},
						ConditionExpression = "attribute_exists(PK)"
					}
				},
				new TransactWriteItem
				{
					Put = new Put { TableName = _options.TableName, Item = newItem, ConditionExpression = "attribute_not_exists(PK)" }
				}
			]
		};

		try
		{
			_ = await _client.TransactWriteItemsAsync(transactRequest, cancellationToken).ConfigureAwait(false);
			LogMessageFailed(messageId, errorMessage, retryCount);
		}
		catch (TransactionCanceledException)

		{
			// Race condition - another process modified the document
			LogConcurrencyConflict(messageId, "MarkFailed");
		}
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0.");
		}

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var failedPK = DynamoDbOutboxDocument.CreatePK(OutboxStatus.Failed);

		var filterExpressions = new List<string>();
		var expressionValues = new Dictionary<string, AttributeValue> { [":pk"] = new() { S = failedPK } };

		if (maxRetries > 0)
		{
			filterExpressions.Add("retryCount < :maxRetries");
			expressionValues[":maxRetries"] = new() { N = maxRetries.ToString(CultureInfo.InvariantCulture) };
		}

		if (olderThan.HasValue)
		{
			filterExpressions.Add("lastAttemptAt < :olderThan");
			expressionValues[":olderThan"] = new() { S = olderThan.Value.ToString("O", CultureInfo.InvariantCulture) };
		}

		var request = new QueryRequest
		{
			TableName = _options.TableName,
			KeyConditionExpression = "PK = :pk",
			ExpressionAttributeValues = expressionValues,
			Limit = batchSize,
			ConsistentRead = _options.UseConsistentReads
		};

		if (filterExpressions.Count > 0)
		{
			request.FilterExpression = string.Join(" AND ", filterExpressions);
		}

		var results = new List<OutboundMessage>();

		var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);
		foreach (var item in response.Items)
		{
			if (results.Count >= batchSize)
			{
				break;
			}

			results.Add(DynamoDbOutboxDocument.ToOutboundMessage(item));
		}

		// Sort by retryCount ASC, lastAttemptAt ASC
		return results
			.OrderBy(m => m.RetryCount)
			.ThenBy(m => m.LastAttemptAt);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0.");
		}

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var scheduledBeforeStr = scheduledBefore.ToString("O", CultureInfo.InvariantCulture);

		// Use GSI2 to query scheduled messages
		var request = new QueryRequest
		{
			TableName = _options.TableName,
			IndexName = _options.GSI2IndexName,
			KeyConditionExpression = "GSI2PK = :scheduled AND GSI2SK <= :before",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":scheduled"] = new() { S = DynamoDbOutboxDocument.ScheduledPrefix },
				[":before"] = new() { S = scheduledBeforeStr }
			},
			Limit = batchSize
		};

		var results = new List<OutboundMessage>();

		var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);
		foreach (var item in response.Items)
		{
			if (results.Count >= batchSize)
			{
				break;
			}

			results.Add(DynamoDbOutboxDocument.ToOutboundMessage(item));
		}

		return results;
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0.");
		}

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sentPK = DynamoDbOutboxDocument.CreatePK(OutboxStatus.Sent);
		var olderThanStr = olderThan.ToString("O", CultureInfo.InvariantCulture);

		// Query for sent messages older than cutoff
		var queryRequest = new QueryRequest
		{
			TableName = _options.TableName,
			KeyConditionExpression = "PK = :pk",
			FilterExpression = "sentAt < :olderThan",
			ProjectionExpression = "PK, SK",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":pk"] = new() { S = sentPK }, [":olderThan"] = new() { S = olderThanStr }
			},
			Limit = batchSize,
			ConsistentRead = _options.UseConsistentReads
		};

		var itemsToDelete = new List<Dictionary<string, AttributeValue>>();
		var response = await _client.QueryAsync(queryRequest, cancellationToken).ConfigureAwait(false);
		itemsToDelete.AddRange(response.Items.Take(batchSize));

		if (itemsToDelete.Count == 0)
		{
			return 0;
		}

		// Delete in batches of 25 (DynamoDB limit)
		var deletedCount = 0;
		const int deleteBatchSize = 25;

		for (var i = 0; i < itemsToDelete.Count; i += deleteBatchSize)
		{
			var batch = itemsToDelete.Skip(i).Take(deleteBatchSize).ToList();
			var writeRequests = batch.Select(item => new WriteRequest
			{
				DeleteRequest = new DeleteRequest
				{
					Key = new Dictionary<string, AttributeValue>
					{
						[DynamoDbOutboxDocument.PK] = item[DynamoDbOutboxDocument.PK],
						[DynamoDbOutboxDocument.SK] = item[DynamoDbOutboxDocument.SK]
					}
				}
			}).ToList();

			var batchRequest = new BatchWriteItemRequest
			{
				RequestItems = new Dictionary<string, List<WriteRequest>> { [_options.TableName] = writeRequests }
			};

			var batchResponse = await _client.BatchWriteItemAsync(batchRequest, cancellationToken)
				.ConfigureAwait(false);

			deletedCount += writeRequests.Count -
			                (batchResponse.UnprocessedItems.TryGetValue(_options.TableName, out var unprocessed)
				                ? unprocessed.Count
				                : 0);
		}

		LogCleanedUp(deletedCount, olderThan);
		return deletedCount;
	}

	/// <inheritdoc/>
	public async ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var counts = new Dictionary<OutboxStatus, int>();
		DateTimeOffset? oldestStagedCreatedAt = null;
		DateTimeOffset? oldestFailedCreatedAt = null;
		int scheduledCount;

		// Count items in each status partition
		foreach (var status in Enum.GetValues<OutboxStatus>())
		{
			var pk = DynamoDbOutboxDocument.CreatePK(status);

			var request = new QueryRequest
			{
				TableName = _options.TableName,
				KeyConditionExpression = "PK = :pk",
				Select = "COUNT",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = new() { S = pk } }
			};

			var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);
			counts[status] = response.Count ?? 0;

			// Get oldest item for Staged and Failed
			if (status == OutboxStatus.Staged && (response.Count ?? 0) > 0)
			{
				var oldestRequest = new QueryRequest
				{
					TableName = _options.TableName,
					KeyConditionExpression = "PK = :pk",
					ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = new() { S = pk } },
					Limit = 1,
					ScanIndexForward = true
				};

				var oldestResponse = await _client.QueryAsync(oldestRequest, cancellationToken).ConfigureAwait(false);
				if (oldestResponse.Items.Count > 0)
				{
					oldestStagedCreatedAt = DateTimeOffset.Parse(
						oldestResponse.Items[0][DynamoDbOutboxDocument.CreatedAt].S,
						CultureInfo.InvariantCulture);
				}
			}
			else if (status == OutboxStatus.Failed && (response.Count ?? 0) > 0)
			{
				var oldestRequest = new QueryRequest
				{
					TableName = _options.TableName,
					KeyConditionExpression = "PK = :pk",
					ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = new() { S = pk } },
					Limit = 1,
					ScanIndexForward = true
				};

				var oldestResponse = await _client.QueryAsync(oldestRequest, cancellationToken).ConfigureAwait(false);
				if (oldestResponse.Items.Count > 0)
				{
					oldestFailedCreatedAt = DateTimeOffset.Parse(
						oldestResponse.Items[0][DynamoDbOutboxDocument.CreatedAt].S,
						CultureInfo.InvariantCulture);
				}
			}
		}

		// Count scheduled messages using GSI2
		var scheduledRequest = new QueryRequest
		{
			TableName = _options.TableName,
			IndexName = _options.GSI2IndexName,
			KeyConditionExpression = "GSI2PK = :scheduled",
			Select = "COUNT",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":scheduled"] = new() { S = DynamoDbOutboxDocument.ScheduledPrefix }
			}
		};

		var scheduledResponse = await _client.QueryAsync(scheduledRequest, cancellationToken).ConfigureAwait(false);
		scheduledCount = scheduledResponse.Count ?? 0;

		return new OutboxStatistics
		{
			StagedMessageCount = counts.GetValueOrDefault(OutboxStatus.Staged, 0) - scheduledCount,
			SendingMessageCount = counts.GetValueOrDefault(OutboxStatus.Sending, 0),
			SentMessageCount = counts.GetValueOrDefault(OutboxStatus.Sent, 0),
			FailedMessageCount = counts.GetValueOrDefault(OutboxStatus.Failed, 0),
			ScheduledMessageCount = scheduledCount,
			OldestUnsentMessageAge = oldestStagedCreatedAt.HasValue ? now - oldestStagedCreatedAt.Value : null,
			OldestFailedMessageAge = oldestFailedCreatedAt.HasValue ? now - oldestFailedCreatedAt.Value : null
		};
	}

	private async Task<Dictionary<string, AttributeValue>?> FindMessageByIdAsync(
		string messageId,
		CancellationToken cancellationToken)

	{
		// Use GSI1 to find message by ID
		var gsi1pk = DynamoDbOutboxDocument.CreateGSI1PK(messageId);

		var request = new QueryRequest
		{
			TableName = _options.TableName,
			IndexName = _options.GSI1IndexName,
			KeyConditionExpression = "GSI1PK = :pk",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = new() { S = gsi1pk } },
			Limit = 1
		};

		var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Items.Count == 0)
		{
			return null;
		}

		// GSI returns only projected attributes, need to get full item
		var item = response.Items[0];
		var pk = item[DynamoDbOutboxDocument.PK].S;
		var sk = item[DynamoDbOutboxDocument.SK].S;

		var getRequest = new GetItemRequest
		{
			TableName = _options.TableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[DynamoDbOutboxDocument.PK] = new() { S = pk }, [DynamoDbOutboxDocument.SK] = new() { S = sk }
			},
			ConsistentRead = _options.UseConsistentReads
		};

		var getResponse = await _client.GetItemAsync(getRequest, cancellationToken).ConfigureAwait(false);
		return getResponse.Item?.Count > 0 ? getResponse.Item : null;
	}

	private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
	{
		try
		{
			_ = await _client.DescribeTableAsync(_options.TableName, cancellationToken).ConfigureAwait(false);
		}
		catch (ResourceNotFoundException)

		{
			// Create table with GSIs
			var createRequest = new CreateTableRequest
			{
				TableName = _options.TableName,
				KeySchema =
				[
					new KeySchemaElement { AttributeName = DynamoDbOutboxDocument.PK, KeyType = KeyType.HASH },
					new KeySchemaElement { AttributeName = DynamoDbOutboxDocument.SK, KeyType = KeyType.RANGE }
				],
				AttributeDefinitions =
				[
					new AttributeDefinition { AttributeName = DynamoDbOutboxDocument.PK, AttributeType = ScalarAttributeType.S },
					new AttributeDefinition { AttributeName = DynamoDbOutboxDocument.SK, AttributeType = ScalarAttributeType.S },
					new AttributeDefinition { AttributeName = DynamoDbOutboxDocument.GSI1PK, AttributeType = ScalarAttributeType.S },
					new AttributeDefinition { AttributeName = DynamoDbOutboxDocument.GSI1SK, AttributeType = ScalarAttributeType.S },
					new AttributeDefinition { AttributeName = DynamoDbOutboxDocument.GSI2PK, AttributeType = ScalarAttributeType.S },
					new AttributeDefinition { AttributeName = DynamoDbOutboxDocument.GSI2SK, AttributeType = ScalarAttributeType.S }
				],
				GlobalSecondaryIndexes =
				[
					new GlobalSecondaryIndex
					{
						IndexName = _options.GSI1IndexName,
						KeySchema =
						[
							new KeySchemaElement { AttributeName = DynamoDbOutboxDocument.GSI1PK, KeyType = KeyType.HASH },
							new KeySchemaElement { AttributeName = DynamoDbOutboxDocument.GSI1SK, KeyType = KeyType.RANGE }
						],
						Projection = new Projection { ProjectionType = ProjectionType.ALL }
					},
					new GlobalSecondaryIndex
					{
						IndexName = _options.GSI2IndexName,
						KeySchema =
						[
							new KeySchemaElement { AttributeName = DynamoDbOutboxDocument.GSI2PK, KeyType = KeyType.HASH },
							new KeySchemaElement { AttributeName = DynamoDbOutboxDocument.GSI2SK, KeyType = KeyType.RANGE }
						],
						Projection = new Projection { ProjectionType = ProjectionType.ALL }
					}
				],
				BillingMode = BillingMode.PAY_PER_REQUEST
			};

			_ = await _client.CreateTableAsync(createRequest, cancellationToken).ConfigureAwait(false);

			// Wait for table to be active
			var describeRequest = new DescribeTableRequest { TableName = _options.TableName };
			TableStatus status;
			do
			{
				await Task.Delay(500, cancellationToken).ConfigureAwait(false);
				var describeResponse = await _client.DescribeTableAsync(describeRequest, cancellationToken)
					.ConfigureAwait(false);
				status = describeResponse.Table.TableStatus;
			} while (status != TableStatus.ACTIVE);

			// Enable TTL
			if (_options.SentMessageTtlSeconds > 0)
			{
				var ttlRequest = new UpdateTimeToLiveRequest
				{
					TableName = _options.TableName,
					TimeToLiveSpecification = new TimeToLiveSpecification { Enabled = true, AttributeName = _options.TtlAttributeName }
				};

				_ = await _client.UpdateTimeToLiveAsync(ttlRequest, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private IAmazonDynamoDB CreateClient()
	{
		var config = new AmazonDynamoDBConfig
		{
			Timeout = TimeSpan.FromSeconds(_options.TimeoutInSeconds), MaxErrorRetry = _options.MaxRetryAttempts
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

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Only dispose the client if we own it (created it ourselves)
		if (_ownsClient)
		{
			_client?.Dispose();
		}

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

		// Only dispose the client if we own it (created it ourselves)
		if (_ownsClient)
		{
			_client?.Dispose();
		}

		_initLock.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	// Logging methods using LoggerMessage source generator
	[LoggerMessage(DataDynamoDbEventId.OutboxMessageStaged, LogLevel.Debug,
		"Staged outbox message {MessageId} of type {MessageType} with priority {Priority}")]
	private partial void LogMessageStaged(string messageId, string messageType, int priority);

	[LoggerMessage(DataDynamoDbEventId.OutboxMessageEnqueued, LogLevel.Debug, "Enqueued outbox message {MessageId} of type {MessageType}")]
	private partial void LogMessageEnqueued(string messageId, string messageType);

	[LoggerMessage(DataDynamoDbEventId.OutboxMessageSent, LogLevel.Debug, "Marked outbox message {MessageId} as sent")]
	private partial void LogMessageSent(string messageId);

	[LoggerMessage(DataDynamoDbEventId.OutboxMessageFailed, LogLevel.Warning,
		"Marked outbox message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMessageFailed(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(DataDynamoDbEventId.OutboxCleanedUp, LogLevel.Information,
		"Cleaned up {Count} sent outbox messages older than {CutoffDate}")]
	private partial void LogCleanedUp(int count, DateTimeOffset cutoffDate);

	[LoggerMessage(DataDynamoDbEventId.OutboxConcurrencyConflict, LogLevel.Warning,
		"Concurrency conflict for message {MessageId} during {Operation}")]
	private partial void LogConcurrencyConflict(string messageId, string operation);
}
