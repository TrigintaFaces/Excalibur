// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.Data.CloudNative;
using Excalibur.Data.Observability;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.DynamoDb;

/// <summary>
/// AWS DynamoDB implementation of the cloud-native outbox store.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Cloud outbox implementations inherently couple with many SDK and abstraction types.")]
public sealed partial class DynamoDbOutboxStore : ICloudNativeOutboxStore, ICloudNativeOutboxStoreBatch, ICloudNativeOutboxStoreClaim, IAsyncDisposable, ITenantPartitionedStore
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	/// <summary>The maximum number of items DynamoDB allows in a single <c>TransactWriteItems</c> call.</summary>
	private const int DynamoTransactItemLimit = 100;

	private readonly DynamoDbOutboxOptions _options;
	private readonly ILogger<DynamoDbOutboxStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);

	private IAmazonDynamoDB? _client;
	private IAmazonDynamoDBStreams? _streamsClient;
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The DynamoDB outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbOutboxStore(
		IOptions<DynamoDbOutboxOptions> options,
		ILogger<DynamoDbOutboxStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options.Validate();
	}

	/// <inheritdoc/>
	public CloudPersistenceProviderType ProviderType => CloudPersistenceProviderType.DynamoDb;

	/// <summary>
	/// Initializes the DynamoDB client and creates the table if needed.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task InitializeAsync(CancellationToken cancellationToken)
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

			LogInitializing(_options.TableName);

			_client = CreateClient();
			_streamsClient = CreateStreamsClient();

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
	public async Task<CloudOperationResult<CloudOutboxMessage>> AddAsync(
		CloudOutboxMessage message,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var item = ToAttributeMap(message, partitionKey);

		try
		{
			var request = new PutItemRequest
			{
				TableName = _options.TableName,
				Item = item,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var response = await _client!.PutItemAsync(request, cancellationToken).ConfigureAwait(false);
			var consumedCapacity = response.ConsumedCapacity?.WriteCapacityUnits ?? 0;

			LogOperationCompleted("Add", consumedCapacity);

			return new CloudOperationResult<CloudOutboxMessage>(
				success: true,
				statusCode: (int)response.HttpStatusCode,
				requestCharge: consumedCapacity,
				document: message);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"add",
				message.MessageId,
				message.CorrelationId,
				message.CausationId);
			LogOperationFailed("Add", ex.Message, ex);
			return new CloudOperationResult<CloudOutboxMessage>(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"add",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudBatchResult> AddBatchAsync(
		IEnumerable<CloudOutboxMessage> messages,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var writeRequests = messages
			.Select(m => new WriteRequest { PutRequest = new PutRequest { Item = ToAttributeMap(m, partitionKey) } }).ToList();

		try
		{
			var request = new BatchWriteItemRequest
			{
				RequestItems = new Dictionary<string, List<WriteRequest>> { [_options.TableName] = writeRequests },
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var response = await _client!.BatchWriteItemAsync(request, cancellationToken).ConfigureAwait(false);
			var consumedCapacity = response.ConsumedCapacity?.Sum(c => c.WriteCapacityUnits) ?? 0;

			LogOperationCompleted("AddBatch", consumedCapacity);

			var operationResults = writeRequests.Select(_ => new CloudOperationResult(
				success: true,
				statusCode: 200,
				requestCharge: 0)).ToList();

			if (response.HttpStatusCode != HttpStatusCode.OK)
			{
				result = WriteStoreTelemetry.Results.Failure;
			}

			return new CloudBatchResult(
				success: response.HttpStatusCode == HttpStatusCode.OK,
				requestCharge: consumedCapacity,
				operationResults: operationResults);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"add_batch");
			LogOperationFailed("AddBatch", ex.Message, ex);
			return new CloudBatchResult(
				success: false,
				requestCharge: 0,
				operationResults: [],
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"add_batch",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudQueryResult<CloudOutboxMessage>> GetPendingAsync(
		IPartitionKey partitionKey,
		int batchSize,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		try
		{
			var request = new QueryRequest
			{
				TableName = _options.TableName,
				// Queried against the createdAt GSI, not the base table: a base-table Query is ordered by
				// the sort key (a message id), which is not creation order. ScanIndexForward defaults to
				// true (ascending), so results come back oldest-first -- the FIFO guarantee
				// ICloudNativeOutboxStore documents.
				IndexName = _options.CreatedAtIndexName,
				KeyConditionExpression = $"{_options.PartitionKeyAttribute} = :pk",
				FilterExpression = "isPublished = :false",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":pk"] = new() { S = partitionKey.Value },
					[":false"] = new() { BOOL = false }
				},
				Limit = batchSize,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var response = await _client!.QueryAsync(request, cancellationToken).ConfigureAwait(false);
			var consumedCapacity = response.ConsumedCapacity?.ReadCapacityUnits ?? 0;

			var messages = response.Items.Select(FromAttributeMap).ToList();

			LogOperationCompleted("GetPending", consumedCapacity);

			return new CloudQueryResult<CloudOutboxMessage>(
				messages,
				consumedCapacity,
#pragma warning disable IL2026, IL3050
				response.LastEvaluatedKey?.Count > 0 ? JsonSerializer.Serialize(response.LastEvaluatedKey) : null);
#pragma warning restore IL2026, IL3050
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"get_pending");
			LogOperationFailed("GetPending", ex.Message, ex);
			return new CloudQueryResult<CloudOutboxMessage>([], 0);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"get_pending",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// The atomic step is <c>UpdateItem</c> under a <c>ConditionExpression</c> naming the lease. DynamoDB
	/// evaluates the condition and applies the update as one operation on a single item, so of two claimants
	/// issuing it against the same item exactly one succeeds and the other is refused with
	/// <c>ConditionalCheckFailedException</c>. That refusal is the mechanism working, not a fault.
	/// </para>
	/// <para>
	/// The query that precedes it only nominates candidates — its filter is evaluated on a read and excludes
	/// nobody. An unconditional <c>UpdateItem</c> here would be silently wrong: with no condition to fail,
	/// both claimants would succeed and both would publish.
	/// </para>
	/// </remarks>
	public async Task<CloudQueryResult<CloudOutboxMessage>> ClaimPendingAsync(
		IPartitionKey partitionKey,
		int batchSize,
		string claimantId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(partitionKey);
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
		ArgumentException.ThrowIfNullOrWhiteSpace(claimantId);
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var leaseCutoff = DateTimeOffset.UtcNow.AddSeconds(-_options.LeaseTimeoutSeconds)
			.ToString("o", CultureInfo.InvariantCulture);

		try
		{
			var candidates = await QueryClaimableKeysAsync(partitionKey, batchSize, leaseCutoff, cancellationToken)
				.ConfigureAwait(false);

			var consumedCapacity = candidates.ConsumedCapacity;
			var claimed = new List<CloudOutboxMessage>(candidates.MessageIds.Count);

			foreach (var messageId in candidates.MessageIds)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var attempt = await TryClaimOneAsync(messageId, partitionKey, claimantId, leaseCutoff, cancellationToken)
					.ConfigureAwait(false);

				consumedCapacity += attempt.ConsumedCapacity;

				if (attempt.Claimed is not null)
				{
					claimed.Add(attempt.Claimed);
				}
			}

			LogOperationCompleted("ClaimPending", consumedCapacity);

			return new CloudQueryResult<CloudOutboxMessage>(claimed, consumedCapacity);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"claim_pending");
			LogOperationFailed("ClaimPending", ex.Message, ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"claim_pending",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <summary>
	/// Nominates the items that look claimable right now.
	/// </summary>
	/// <remarks>
	/// This decides nothing. A DynamoDB filter is applied after the read, so two claimants querying at the
	/// same instant see the same items; exclusion happens later, at the conditional update.
	/// </remarks>
	/// <param name="partitionKey">The partition to read.</param>
	/// <param name="batchSize">The maximum number of items to consider.</param>
	/// <param name="leaseCutoff">The instant before which a stamped lease has expired.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The candidate message identifiers, with the cost of finding them.</returns>
	private async Task<(IReadOnlyList<string> MessageIds, double ConsumedCapacity)> QueryClaimableKeysAsync(
		IPartitionKey partitionKey,
		int batchSize,
		string leaseCutoff,
		CancellationToken cancellationToken)
	{
		var request = new QueryRequest
		{
			TableName = _options.TableName,
			// Same reasoning as GetPendingAsync: candidates come from the createdAt GSI, ascending, so a
			// claim call hands out its own batch in creation order too -- ICloudNativeOutboxStoreClaim's
			// ordering statement, closing the gap where the base-table Query (sort key = message id) gave
			// no such guarantee.
			IndexName = _options.CreatedAtIndexName,
			KeyConditionExpression = $"{_options.PartitionKeyAttribute} = :pk",

			// The lease instant is stored round-trip ("o") in UTC, which is fixed width and therefore orders
			// correctly under DynamoDB's string comparison.
			FilterExpression = "#isPublished = :false AND (attribute_not_exists(#leasedAt) OR #leasedAt < :cutoff)",
			ExpressionAttributeNames = new Dictionary<string, string>
			{
				["#isPublished"] = "isPublished",
				["#leasedAt"] = "leasedAt"
			},
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":pk"] = new() { S = partitionKey.Value },
				[":false"] = new() { BOOL = false },
				[":cutoff"] = new() { S = leaseCutoff }
			},
			Limit = batchSize,
			ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
		};

		var response = await _client!.QueryAsync(request, cancellationToken).ConfigureAwait(false);

		var messageIds = response.Items
			.Select(item => item[_options.SortKeyAttribute].S)
			.Take(batchSize)
			.ToList();

		return (messageIds, response.ConsumedCapacity?.ReadCapacityUnits ?? 0);
	}

	/// <summary>
	/// Attempts to win one item, stamping the lease under a condition naming that lease.
	/// </summary>
	/// <param name="messageId">The item to claim.</param>
	/// <param name="partitionKey">The partition the item lives in.</param>
	/// <param name="claimantId">The claimant to record as the lease owner.</param>
	/// <param name="leaseCutoff">The instant before which an existing lease has expired.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The claimed message when this claimant won, otherwise <see langword="null"/>, with the cost.</returns>
	private async Task<(CloudOutboxMessage? Claimed, double ConsumedCapacity)> TryClaimOneAsync(
		string messageId,
		IPartitionKey partitionKey,
		string claimantId,
		string leaseCutoff,
		CancellationToken cancellationToken)
	{
		// The stamp is taken HERE, immediately before the write that establishes the lease, and not at the
		// start of the drain. A batch-start instant would hand the last message of an N-message batch a
		// lease that has already burned the query round-trip plus N-1 conditional writes, so its protective
		// interval would shrink as the batch grows -- and the lease is the only thing standing between a
		// slow drain and a second dispatcher publishing the same message. The eligibility cutoff is
		// deliberately NOT re-anchored: it stays at the batch-start value, because an older cutoff is the
		// conservative direction (it judges fewer leases expired) and so cannot admit a live lease.
		var nowText = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);

		var request = new UpdateItemRequest
		{
			TableName = _options.TableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[_options.PartitionKeyAttribute] = new() { S = partitionKey.Value },
				[_options.SortKeyAttribute] = new() { S = messageId }
			},

			// The whole guarantee lives in this line. It re-tests, at the moment of the write, exactly what
			// the query tested at the moment of the read.
			ConditionExpression = "#isPublished = :false AND (attribute_not_exists(#leasedAt) OR #leasedAt < :cutoff)",
			UpdateExpression = "SET #leasedAt = :now, #leasedBy = :claimant",
			ExpressionAttributeNames = new Dictionary<string, string>
			{
				["#isPublished"] = "isPublished",
				["#leasedAt"] = "leasedAt",
				["#leasedBy"] = "leasedBy"
			},
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":false"] = new() { BOOL = false },
				[":cutoff"] = new() { S = leaseCutoff },
				[":now"] = new() { S = nowText },
				[":claimant"] = new() { S = claimantId }
			},
			ReturnValues = ReturnValue.ALL_NEW,
			ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
		};

		try
		{
			var response = await _client!.UpdateItemAsync(request, cancellationToken).ConfigureAwait(false);
			return (FromAttributeMap(response.Attributes), response.ConsumedCapacity?.WriteCapacityUnits ?? 0);
		}
		catch (ConditionalCheckFailedException)
		{
			// Another claimant took the item, or it was published, between the query and this update.
			// Expected under concurrency — this is the exclusion doing its job, so it is neither logged as
			// a failure nor retried here.
			return (null, 0);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult> MarkAsPublishedAsync(
		string messageId,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var publishedAt = DateTimeOffset.UtcNow;
		var ttl = _options.DefaultTimeToLiveSeconds > 0
			? publishedAt.AddSeconds(_options.DefaultTimeToLiveSeconds).ToUnixTimeSeconds()
			: 0;

		try
		{
			var request = new UpdateItemRequest
			{
				TableName = _options.TableName,
				Key = new Dictionary<string, AttributeValue>
				{
					[_options.PartitionKeyAttribute] = new() { S = partitionKey.Value },
					[_options.SortKeyAttribute] = new() { S = messageId }
				},
				// "ttl" is a DynamoDB reserved keyword: naming it literally in an UpdateExpression is
				// rejected by the service ("Invalid UpdateExpression: Attribute name is a reserved
				// keyword") -- the configured attribute name (default "ttl") must go through an
				// ExpressionAttributeNames placeholder, exactly as every other reserved-word-shaped
				// attribute name must.
				UpdateExpression = "SET isPublished = :true, publishedAt = :publishedAt" +
								   (ttl > 0 ? ", #ttl = :ttl" : ""),
				// UpdateItem with no condition is an upsert: a nonexistent messageId would silently
				// CREATE a phantom "published" item instead of failing, which is indistinguishable from
				// success to the caller. The condition makes marking an unknown message fail loud, the
				// same as every other backend's MarkAsPublishedAsync.
				ConditionExpression = "attribute_exists(#pk)",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":true"] = new() { BOOL = true },
					[":publishedAt"] = new() { S = publishedAt.ToString("o") }
				},
				ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = _options.PartitionKeyAttribute },
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			if (ttl > 0)
			{
				request.ExpressionAttributeValues[":ttl"] = new() { N = ttl.ToString() };
				request.ExpressionAttributeNames["#ttl"] = _options.TtlAttribute;
			}

			var response = await _client!.UpdateItemAsync(request, cancellationToken).ConfigureAwait(false);
			var consumedCapacity = response.ConsumedCapacity?.WriteCapacityUnits ?? 0;

			LogOperationCompleted("MarkAsPublished", consumedCapacity);

			return new CloudOperationResult(
				success: true,
				statusCode: (int)response.HttpStatusCode,
				requestCharge: consumedCapacity);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"mark_published",
				messageId);
			LogOperationFailed("MarkAsPublished", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"mark_published",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudBatchResult> MarkBatchAsPublishedAsync(
		IEnumerable<string> messageIds,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		try
		{
			var messageIdList = messageIds as IReadOnlyList<string> ?? messageIds.ToList();
			var operationResults = new List<CloudOperationResult>(messageIdList.Count);
			double totalCapacity = 0;

			var publishedAt = DateTimeOffset.UtcNow;
			var ttl = _options.DefaultTimeToLiveSeconds > 0
				? publishedAt.AddSeconds(_options.DefaultTimeToLiveSeconds).ToUnixTimeSeconds()
				: 0;

			// DynamoDB caps a TransactWriteItems at 100 items, so mark-published is applied as a sequence of
			// ≤100-item atomic transactions rather than a per-message UpdateItem loop. Each chunk is all-or-
			// nothing, so a mid-batch failure can no longer leave a partially-marked batch within a chunk.
			for (var offset = 0; offset < messageIdList.Count; offset += DynamoTransactItemLimit)
			{
				var count = Math.Min(DynamoTransactItemLimit, messageIdList.Count - offset);
				var transactItems = new List<TransactWriteItem>(count);
				for (var i = 0; i < count; i++)
				{
					transactItems.Add(BuildMarkPublishedTransactItem(messageIdList[offset + i], partitionKey, publishedAt, ttl));
				}

				try
				{
					var request = new TransactWriteItemsRequest
					{
						TransactItems = transactItems,
						ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
					};

					var response = await _client!.TransactWriteItemsAsync(request, cancellationToken).ConfigureAwait(false);
					var chunkCapacity = response.ConsumedCapacity?.Sum(c => c.CapacityUnits) ?? 0;
					totalCapacity += chunkCapacity;

					var perItemCharge = count > 0 ? chunkCapacity / count : 0;
					for (var i = 0; i < count; i++)
					{
						operationResults.Add(new CloudOperationResult(
							success: true,
							statusCode: (int)response.HttpStatusCode,
							requestCharge: perItemCharge));
					}
				}
				catch (AmazonDynamoDBException ex)
				{
					// The whole chunk was rejected atomically (e.g. TransactionCanceledException) — record
					// every message in it as not-published so the caller sees the failure.
					for (var i = 0; i < count; i++)
					{
						operationResults.Add(new CloudOperationResult(
							success: false,
							statusCode: (int)ex.StatusCode,
							requestCharge: 0,
							errorMessage: ex.Message));
					}
				}
			}

			if (operationResults.Any(r => !r.Success))
			{
				result = WriteStoreTelemetry.Results.Failure;
			}

			return new CloudBatchResult(
				success: operationResults.All(r => r.Success),
				requestCharge: totalCapacity,
				operationResults: operationResults);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"mark_batch_published",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <summary>
	/// Builds the <c>Update</c> transact item that marks a single outbox message published, mirroring the
	/// single-item <see cref="MarkAsPublishedAsync"/> update so the batch path is behaviorally identical.
	/// </summary>
	private TransactWriteItem BuildMarkPublishedTransactItem(
		string messageId,
		IPartitionKey partitionKey,
		DateTimeOffset publishedAt,
		long ttl)
	{
		var values = new Dictionary<string, AttributeValue>
		{
			[":true"] = new() { BOOL = true },
			[":publishedAt"] = new() { S = publishedAt.ToString("o") }
		};

		if (ttl > 0)
		{
			values[":ttl"] = new() { N = ttl.ToString(System.Globalization.CultureInfo.InvariantCulture) };
		}

		var item = new Update
		{
			TableName = _options.TableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[_options.PartitionKeyAttribute] = new() { S = partitionKey.Value },
				[_options.SortKeyAttribute] = new() { S = messageId }
			},
			// See MarkAsPublishedAsync: "ttl" is a DynamoDB reserved keyword and must go through an
			// ExpressionAttributeNames placeholder rather than being named literally in the expression.
			UpdateExpression = "SET isPublished = :true, publishedAt = :publishedAt" +
							   (ttl > 0 ? ", #ttl = :ttl" : ""),
			ExpressionAttributeValues = values
		};

		if (ttl > 0)
		{
			item.ExpressionAttributeNames = new Dictionary<string, string> { ["#ttl"] = _options.TtlAttribute };
		}

		return new TransactWriteItem { Update = item };
	}

	/// <inheritdoc/>
	public async Task<CloudCleanupResult> CleanupOldMessagesAsync(
		IPartitionKey partitionKey,
		TimeSpan retentionPeriod,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var cutoffDate = DateTimeOffset.UtcNow.Subtract(retentionPeriod).ToString("o");
		var deletedCount = 0;
		double totalCapacity = 0;

		try
		{
			var queryRequest = new QueryRequest
			{
				TableName = _options.TableName,
				KeyConditionExpression = $"{_options.PartitionKeyAttribute} = :pk",
				FilterExpression = "isPublished = :true AND publishedAt < :cutoff",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":pk"] = new() { S = partitionKey.Value },
					[":true"] = new() { BOOL = true },
					[":cutoff"] = new() { S = cutoffDate }
				},
				ProjectionExpression = $"{_options.PartitionKeyAttribute}, {_options.SortKeyAttribute}",
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var queryResponse = await _client!.QueryAsync(queryRequest, cancellationToken).ConfigureAwait(false);
			totalCapacity += queryResponse.ConsumedCapacity?.ReadCapacityUnits ?? 0;

			foreach (var item in queryResponse.Items)
			{
				var deleteRequest = new DeleteItemRequest
				{
					TableName = _options.TableName,
					Key = new Dictionary<string, AttributeValue>
					{
						[_options.PartitionKeyAttribute] = item[_options.PartitionKeyAttribute],
						[_options.SortKeyAttribute] = item[_options.SortKeyAttribute]
					},
					ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
				};

				var deleteResponse = await _client!.DeleteItemAsync(deleteRequest, cancellationToken)
					.ConfigureAwait(false);
				totalCapacity += deleteResponse.ConsumedCapacity?.WriteCapacityUnits ?? 0;
				deletedCount++;
			}

			LogOperationCompleted("CleanupOldMessages", totalCapacity);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"cleanup_old");
			LogOperationFailed("CleanupOldMessages", ex.Message, ex);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"cleanup_old",
				result,
				stopwatch.Elapsed);
		}

		return new CloudCleanupResult(deletedCount, totalCapacity);
	}

	/// <inheritdoc/>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Reliability",
		"CA2000:Dispose objects before losing scope",
		Justification = "Ownership of subscription transfers to caller on successful return; disposed on failure path.")]
	public async Task<IChangeFeedSubscription<CloudOutboxMessage>> SubscribeToNewMessagesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		DynamoDbOutboxStreamsSubscription? subscription = null;

		try
		{
			subscription = new DynamoDbOutboxStreamsSubscription(
				_client!,
				_streamsClient!,
				_options.TableName,
				options ?? ChangeFeedOptions.Default,
				_logger);

			await subscription.StartAsync(cancellationToken).ConfigureAwait(false);
			return subscription;
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			if (subscription is not null)
			{
				await subscription.DisposeAsync().ConfigureAwait(false);
			}

			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"subscribe_new",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult> IncrementRetryCountAsync(
		string messageId,
		IPartitionKey partitionKey,
		string? errorMessage,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		try
		{
			var updateExpression = "SET retryCount = retryCount + :inc";
			var expressionValues = new Dictionary<string, AttributeValue> { [":inc"] = new() { N = "1" } };

			if (!string.IsNullOrEmpty(errorMessage))
			{
				updateExpression += ", lastError = :error";
				expressionValues[":error"] = new() { S = errorMessage };
			}

			var request = new UpdateItemRequest
			{
				TableName = _options.TableName,
				Key = new Dictionary<string, AttributeValue>
				{
					[_options.PartitionKeyAttribute] = new() { S = partitionKey.Value },
					[_options.SortKeyAttribute] = new() { S = messageId }
				},
				UpdateExpression = updateExpression,
				ExpressionAttributeValues = expressionValues,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var response = await _client!.UpdateItemAsync(request, cancellationToken).ConfigureAwait(false);
			var consumedCapacity = response.ConsumedCapacity?.WriteCapacityUnits ?? 0;

			LogOperationCompleted("IncrementRetryCount", consumedCapacity);

			return new CloudOperationResult(
				success: true,
				statusCode: (int)response.HttpStatusCode,
				requestCharge: consumedCapacity);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"increment_retry",
				messageId);
			LogOperationFailed("IncrementRetryCount", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"increment_retry",
				result,
				stopwatch.Elapsed);
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

		_client?.Dispose();
		_streamsClient?.Dispose();
		_initLock?.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private IAmazonDynamoDB CreateClient()
	{
		var config = new AmazonDynamoDBConfig { MaxErrorRetry = _options.MaxRetryAttempts };

		if (!string.IsNullOrWhiteSpace(_options.Connection.ServiceUrl))
		{
			config.ServiceURL = _options.Connection.ServiceUrl;
		}
		else if (_options.GetRegionEndpoint() is { } region)
		{
			config.RegionEndpoint = region;
		}

		if (!string.IsNullOrWhiteSpace(_options.Connection.AccessKey) && !string.IsNullOrWhiteSpace(_options.Connection.SecretKey))
		{
			var credentials = new BasicAWSCredentials(_options.Connection.AccessKey, _options.Connection.SecretKey);
			return new AmazonDynamoDBClient(credentials, config);
		}

		return new AmazonDynamoDBClient(config);
	}

	private IAmazonDynamoDBStreams CreateStreamsClient()
	{
		var config = new AmazonDynamoDBStreamsConfig { MaxErrorRetry = _options.MaxRetryAttempts };

		if (!string.IsNullOrWhiteSpace(_options.Connection.ServiceUrl))
		{
			config.ServiceURL = _options.Connection.ServiceUrl;
		}
		else if (_options.GetRegionEndpoint() is { } region)
		{
			config.RegionEndpoint = region;
		}

		if (!string.IsNullOrWhiteSpace(_options.Connection.AccessKey) && !string.IsNullOrWhiteSpace(_options.Connection.SecretKey))
		{
			var credentials = new BasicAWSCredentials(_options.Connection.AccessKey, _options.Connection.SecretKey);
			return new AmazonDynamoDBStreamsClient(credentials, config);
		}

		return new AmazonDynamoDBStreamsClient(config);
	}

	private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
	{
		try
		{
			_ = await _client!.DescribeTableAsync(_options.TableName, cancellationToken).ConfigureAwait(false);
		}
		catch (ResourceNotFoundException)
		{
			var request = new CreateTableRequest
			{
				TableName = _options.TableName,
				KeySchema =
				[
					new() { AttributeName = _options.PartitionKeyAttribute, KeyType = Amazon.DynamoDBv2.KeyType.HASH },
					new() { AttributeName = _options.SortKeyAttribute, KeyType = Amazon.DynamoDBv2.KeyType.RANGE }
				],
				AttributeDefinitions =
				[
					new() { AttributeName = _options.PartitionKeyAttribute, AttributeType = ScalarAttributeType.S },
					new() { AttributeName = _options.SortKeyAttribute, AttributeType = ScalarAttributeType.S },
					new() { AttributeName = "createdAt", AttributeType = ScalarAttributeType.S }
				],
				// The base table's Query is physically ordered by the sort key (a message id), which cannot
				// express "in creation order". This GSI is what GetPendingAsync/ClaimPendingAsync query
				// instead, to honour the FIFO guarantee ICloudNativeOutboxStore documents. ProjectionType.ALL
				// so a GSI query returns every attribute FromAttributeMap needs, with no second round trip.
				GlobalSecondaryIndexes =
				[
					new()
					{
						IndexName = _options.CreatedAtIndexName,
						KeySchema =
						[
							new() { AttributeName = _options.PartitionKeyAttribute, KeyType = Amazon.DynamoDBv2.KeyType.HASH },
							new() { AttributeName = "createdAt", KeyType = Amazon.DynamoDBv2.KeyType.RANGE }
						],
						Projection = new Projection { ProjectionType = ProjectionType.ALL }
					}
				],
				BillingMode = BillingMode.PAY_PER_REQUEST
			};

			if (_options.EnableStreams)
			{
				request.StreamSpecification = new StreamSpecification
				{
					StreamEnabled = true,
					StreamViewType = Amazon.DynamoDBv2.StreamViewType.NEW_AND_OLD_IMAGES
				};
			}

			try
			{
				_ = await _client!.CreateTableAsync(request, cancellationToken).ConfigureAwait(false);
			}
			catch (ResourceInUseException)
			{
				// Multi-instance cold-start race: another instance created (or is creating) the table
				// between our DescribeTable and CreateTable. Benign — fall through to wait-for-active.
			}

			// Wait for table to become active
			var timeout = DateTimeOffset.UtcNow.AddMinutes(2);
			while (DateTimeOffset.UtcNow < timeout)
			{
				var describe = await _client!.DescribeTableAsync(_options.TableName, cancellationToken)
					.ConfigureAwait(false);
				if (describe.Table.TableStatus == TableStatus.ACTIVE)
				{
					break;
				}

				await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
			}

			// Enable TTL if configured
			if (_options.DefaultTimeToLiveSeconds > 0)
			{
				_ = await _client!.UpdateTimeToLiveAsync(
					new UpdateTimeToLiveRequest
					{
						TableName = _options.TableName,
						TimeToLiveSpecification = new TimeToLiveSpecification { Enabled = true, AttributeName = _options.TtlAttribute }
					}, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void EnsureInitialized()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			throw new InvalidOperationException(
				"Outbox store has not been initialized. Call InitializeAsync first.");
		}
	}

	private Dictionary<string, AttributeValue> ToAttributeMap(CloudOutboxMessage message, IPartitionKey partitionKey)
	{
		var item = new Dictionary<string, AttributeValue>
		{
			[_options.PartitionKeyAttribute] = new() { S = partitionKey.Value },
			[_options.SortKeyAttribute] = new() { S = message.MessageId },
			["messageType"] = new() { S = message.MessageType },
			["payload"] = new() { S = Convert.ToBase64String(message.Payload) },
			["createdAt"] = new() { S = message.CreatedAt.ToString("o") },
			["isPublished"] = new() { BOOL = message.IsPublished },
			["retryCount"] = new() { N = message.RetryCount.ToString() }
		};

		if (message.Headers != null)
		{
#pragma warning disable IL2026, IL3050
			item["headers"] = new() { S = JsonSerializer.Serialize(message.Headers, JsonOptions) };
#pragma warning restore IL2026, IL3050
		}

		if (!string.IsNullOrEmpty(message.AggregateId))
		{
			item["aggregateId"] = new() { S = message.AggregateId };
		}

		if (!string.IsNullOrEmpty(message.AggregateType))
		{
			item["aggregateType"] = new() { S = message.AggregateType };
		}

		if (!string.IsNullOrEmpty(message.CorrelationId))
		{
			item["correlationId"] = new() { S = message.CorrelationId };
		}

		if (!string.IsNullOrEmpty(message.CausationId))
		{
			item["causationId"] = new() { S = message.CausationId };
		}

		// Always emit the tenant attribute, folded through the single total conversion. An untenanted
		// message binds the reserved sentinel rather than omitting the attribute, converging on the
		// same representation the SQL providers and Redis outbox use for "no tenant".
		item["tenantId"] = new() { S = KeyedTenantPartition.FromStoredValue(message.TenantId).TenantId };

		if (!string.IsNullOrEmpty(message.Destination))
		{
			item["destination"] = new() { S = message.Destination };
		}

		if (message.PublishedAt.HasValue)
		{
			item["publishedAt"] = new() { S = message.PublishedAt.Value.ToString("o") };
		}

		if (!string.IsNullOrEmpty(message.LastError))
		{
			item["lastError"] = new() { S = message.LastError };
		}

		return item;
	}

	private CloudOutboxMessage FromAttributeMap(Dictionary<string, AttributeValue> item)
	{
		return new CloudOutboxMessage
		{
			MessageId = item[_options.SortKeyAttribute].S,
			MessageType = item["messageType"].S,
			Payload = Convert.FromBase64String(item["payload"].S),
#pragma warning disable IL2026, IL3050
			Headers = item.TryGetValue("headers", out var headers) && !string.IsNullOrEmpty(headers.S)
				? JsonSerializer.Deserialize<Dictionary<string, string>>(headers.S, JsonOptions)
				: null,
#pragma warning restore IL2026, IL3050
			AggregateId = item.TryGetValue("aggregateId", out var aggId) ? aggId.S : null,
			AggregateType = item.TryGetValue("aggregateType", out var aggType) ? aggType.S : null,
			CorrelationId = item.TryGetValue("correlationId", out var corrId) ? corrId.S : null,
			CausationId = item.TryGetValue("causationId", out var causId) ? causId.S : null,
			// Read-tolerant: a row written before this fix carries no tenantId attribute at all.
			// FromStoredValue folds a missing attribute the same way it folds a stored null/empty/
			// sentinel — onto Untenanted — so TenantId is never null after this store reloads a row.
			TenantId = KeyedTenantPartition.FromStoredValue(
				item.TryGetValue("tenantId", out var tenId) ? tenId.S : null).TenantId,
			Destination = item.TryGetValue("destination", out var dest) ? dest.S : null,
			CreatedAt = DateTimeOffset.Parse(item["createdAt"].S, CultureInfo.InvariantCulture),
			PublishedAt = item.TryGetValue("publishedAt", out var pubAt) && !string.IsNullOrEmpty(pubAt.S)
				? DateTimeOffset.Parse(pubAt.S, CultureInfo.InvariantCulture)
				: null,
			RetryCount = int.Parse(item["retryCount"].N),
			LastError = item.TryGetValue("lastError", out var err) ? err.S : null,
			PartitionKeyValue = item[_options.PartitionKeyAttribute].S,
			LeasedAt = item.TryGetValue("leasedAt", out var leasedAt) && !string.IsNullOrEmpty(leasedAt.S)
				? DateTimeOffset.Parse(leasedAt.S, CultureInfo.InvariantCulture)
				: null,
			LeasedBy = item.TryGetValue("leasedBy", out var leasedBy) ? leasedBy.S : null
		};
	}
}
