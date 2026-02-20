// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text.Json;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Observability;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.DynamoDb;

/// <summary>
/// AWS DynamoDB implementation of the cloud-native event store.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification =
		"Event store implementations orchestrate AWS SDK types, serialization, and domain abstractions - high coupling is inherent to the coordinator pattern.")]
public sealed partial class DynamoDbEventStore : ICloudNativeEventStore, IEventStore, IAsyncDisposable
{
	private readonly IAmazonDynamoDB _client;
	private readonly IAmazonDynamoDBStreams _streamsClient;
	private readonly DynamoDbEventStoreOptions _options;
	private readonly ILogger<DynamoDbEventStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);

	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbEventStore" /> class.
	/// </summary>
	/// <param name="client"> The DynamoDB client. </param>
	/// <param name="streamsClient"> The DynamoDB Streams client. </param>
	/// <param name="options"> The event store options. </param>
	/// <param name="logger"> The logger. </param>
	public DynamoDbEventStore(
		IAmazonDynamoDB client,
		IAmazonDynamoDBStreams streamsClient,
		IOptions<DynamoDbEventStoreOptions> options,
		ILogger<DynamoDbEventStore> logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_streamsClient = streamsClient ?? throw new ArgumentNullException(nameof(streamsClient));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public CloudProviderType ProviderType => CloudProviderType.DynamoDb;

	/// <inheritdoc />
	public async Task<CloudEventLoadResult> LoadAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType);

		try
		{
			var streamId = BuildStreamId(aggregateType, aggregateId);
			var events = new List<CloudStoredEvent>();
			double totalCapacity = 0;

			var request = new QueryRequest
			{
				TableName = _options.EventsTableName,
				KeyConditionExpression = $"{_options.PartitionKeyAttribute} = :pk",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = new AttributeValue { S = streamId } },
				ConsistentRead = true,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			do
			{
				var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);
				totalCapacity += response.ConsumedCapacity?.CapacityUnits ?? 0;

				foreach (var item in response.Items)
				{
					events.Add(ToCloudStoredEvent(item));
				}

				request.ExclusiveStartKey = response.LastEvaluatedKey;
			} while (request.ExclusiveStartKey?.Count > 0);

			LogLoadingEvents(streamId, events.Count);

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return new CloudEventLoadResult(events, totalCapacity);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task<CloudEventLoadResult> LoadFromVersionAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		long fromVersion,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType, fromVersion);

		try
		{
			var streamId = BuildStreamId(aggregateType, aggregateId);
			var events = new List<CloudStoredEvent>();
			double totalCapacity = 0;

			var request = new QueryRequest
			{
				TableName = _options.EventsTableName,
				KeyConditionExpression = $"{_options.PartitionKeyAttribute} = :pk AND {_options.SortKeyAttribute} > :version",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":pk"] = new AttributeValue { S = streamId },
					[":version"] = new AttributeValue { N = fromVersion.ToString() }
				},
				ConsistentRead = true,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			do
			{
				var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);
				totalCapacity += response.ConsumedCapacity?.CapacityUnits ?? 0;

				foreach (var item in response.Items)
				{
					events.Add(ToCloudStoredEvent(item));
				}

				request.ExclusiveStartKey = response.LastEvaluatedKey;
			} while (request.ExclusiveStartKey?.Count > 0);

			LogLoadingEvents(streamId, events.Count);

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return new CloudEventLoadResult(events, totalCapacity);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"load_from_version",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task<CloudAppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var operationResult = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var eventsList = events.ToList();
		var correlationId = ExtractCorrelationId(eventsList);
		var messageId = ExtractEventId(eventsList);
		if (eventsList.Count == 0)
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"append",
				operationResult,
				stopwatch.Elapsed);
			return CloudAppendResult.CreateSuccess(expectedVersion, 0);
		}

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventsList.Count, expectedVersion);

		var streamId = BuildStreamId(aggregateType, aggregateId);

		LogAppendingEvents(streamId, aggregateType);

		try
		{
			CloudAppendResult appendResult;
			if (_options.UseTransactionalWrite && eventsList.Count <= 100)
			{
				appendResult = await AppendWithTransactionAsync(
						streamId, aggregateId, aggregateType, eventsList, expectedVersion, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				appendResult = await AppendSequentiallyAsync(
						streamId, aggregateId, aggregateType, eventsList, expectedVersion, cancellationToken)
					.ConfigureAwait(false);
			}

			if (appendResult.Success)
			{
				_ = (activity?.SetTag(EventSourcingTags.Version, appendResult.NextExpectedVersion));
				activity.SetOperationResult(EventSourcingTagValues.Success);
				operationResult = WriteStoreTelemetry.Results.Success;
			}
			else if (appendResult.IsConcurrencyConflict)
			{
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				operationResult = WriteStoreTelemetry.Results.Conflict;
			}
			else
			{
				activity.SetOperationResult(EventSourcingTagValues.Failure);
				operationResult = WriteStoreTelemetry.Results.Failure;
			}

			return appendResult;
		}
		catch (Exception ex)
		{
			operationResult = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"append",
				messageId,
				correlationId);
			_logger.LogError(ex, "Failed to append events to {AggregateType}/{AggregateId}", aggregateType, aggregateId);
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"append",
				operationResult,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task<IChangeFeedSubscription<CloudStoredEvent>> SubscribeToChangesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var subscription = new DynamoDbEventStoreStreamsSubscription(
			_client,
			_streamsClient,
			_options,
			_logger);

		return subscription;
	}

	/// <inheritdoc />
	public async Task<long> GetCurrentVersionAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var streamId = BuildStreamId(aggregateType, aggregateId);

		var request = new QueryRequest
		{
			TableName = _options.EventsTableName,
			KeyConditionExpression = $"{_options.PartitionKeyAttribute} = :pk",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = new AttributeValue { S = streamId } },
			ScanIndexForward = false, // Descending order
			Limit = 1,
			ProjectionExpression = "version",
			ConsistentRead = true
		};

		var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Items.Count == 0)
		{
			return -1;
		}

		var versionAttr = response.Items[0].GetValueOrDefault("version");
		return versionAttr != null && long.TryParse(versionAttr.N, out var version) ? version : -1;
	}

	#region IEventStore Implementation

	/// <inheritdoc />
	async ValueTask<IReadOnlyList<StoredEvent>> IEventStore.LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var partitionKey = new PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await LoadAsync(aggregateId, aggregateType, partitionKey, null, cancellationToken)
			.ConfigureAwait(false);
		return result.Events.Select(ToStoredEvent).ToList();
	}

	/// <inheritdoc />
	async ValueTask<IReadOnlyList<StoredEvent>> IEventStore.LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var partitionKey = new PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await LoadFromVersionAsync(aggregateId, aggregateType, partitionKey, fromVersion, null, cancellationToken)
			.ConfigureAwait(false);
		return result.Events.Select(ToStoredEvent).ToList();
	}

	/// <inheritdoc />
	async ValueTask<AppendResult> IEventStore.AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var partitionKey = new PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await AppendAsync(aggregateId, aggregateType, partitionKey, events, expectedVersion, cancellationToken)
			.ConfigureAwait(false);

		if (result.Success)
		{
			return AppendResult.CreateSuccess(result.NextExpectedVersion, 0);
		}

		if (result.IsConcurrencyConflict)
		{
			return AppendResult.CreateConcurrencyConflict(expectedVersion, result.NextExpectedVersion);
		}

		return AppendResult.CreateFailure(result.ErrorMessage ?? "Unknown error");
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
		int batchSize,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var activity = EventSourcingActivitySource.StartGetUndispatchedActivity(batchSize);

		try
		{
			// DynamoDB scan with filter - not efficient but works for moderate volumes
			var request = new ScanRequest
			{
				TableName = _options.EventsTableName,
				FilterExpression = "isDispatched = :dispatched",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":dispatched"] = new AttributeValue { BOOL = false }
				},
				Limit = batchSize
			};

			var response = await _client.ScanAsync(request, cancellationToken).ConfigureAwait(false);

			var events = response.Items
				.Select(item => ToStoredEvent(ToCloudStoredEvent(item)))
				.ToList();

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return events;
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"get_undispatched",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask MarkEventAsDispatchedAsync(
		string eventId,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var activity = EventSourcingActivitySource.StartMarkDispatchedActivity(eventId);

		try
		{
			// Need to find the event first since we need partition key
			var request = new ScanRequest
			{
				TableName = _options.EventsTableName,
				FilterExpression = "eventId = :eventId",
				ExpressionAttributeValues =
					new Dictionary<string, AttributeValue> { [":eventId"] = new AttributeValue { S = eventId } },
				Limit = 1
			};

			var response = await _client.ScanAsync(request, cancellationToken).ConfigureAwait(false);

			if (response.Items.Count > 0)
			{
				var item = response.Items[0];
				var pk = item[_options.PartitionKeyAttribute].S;
				var sk = item[_options.SortKeyAttribute].N;

				var updateRequest = new UpdateItemRequest
				{
					TableName = _options.EventsTableName,
					Key = new Dictionary<string, AttributeValue>
					{
						[_options.PartitionKeyAttribute] = new AttributeValue { S = pk },
						[_options.SortKeyAttribute] = new AttributeValue { N = sk }
					},
					UpdateExpression = "SET isDispatched = :dispatched",
					ExpressionAttributeValues = new Dictionary<string, AttributeValue>
					{
						[":dispatched"] = new AttributeValue { BOOL = true }
					}
				};

				_ = await _client.UpdateItemAsync(updateRequest, cancellationToken).ConfigureAwait(false);
			}

			activity.SetOperationResult(EventSourcingTagValues.Success);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"mark_dispatched",
				result,
				stopwatch.Elapsed);
		}
	}

	#endregion IEventStore Implementation

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_initLock.Dispose();
		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private static string BuildStreamId(string aggregateType, string aggregateId) =>
		$"{aggregateType}:{aggregateId}";

	private static string? ExtractCorrelationId(IEnumerable<IDomainEvent> events)
	{
		foreach (var @event in events)
		{
			if (@event.Metadata == null)
			{
				continue;
			}

			if (@event.Metadata.TryGetValue("CorrelationId", out var correlationId) ||
				@event.Metadata.TryGetValue("correlationId", out correlationId))
			{
				return correlationId?.ToString();
			}
		}

		return null;
	}

	private static string? ExtractEventId(IEnumerable<IDomainEvent> events)
	{
		foreach (var @event in events)
		{
			if (!string.IsNullOrWhiteSpace(@event.EventId))
			{
				return @event.EventId;
			}
		}

		return null;
	}

	private static byte[] SerializeEvent(IDomainEvent evt)
	{
		return JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType());
	}

	private static StoredEvent ToStoredEvent(CloudStoredEvent cloudEvent) =>
		new(
			cloudEvent.EventId,
			cloudEvent.AggregateId,
			cloudEvent.AggregateType,
			cloudEvent.EventType,
			cloudEvent.EventData,
			cloudEvent.Metadata,
			cloudEvent.Version,
			cloudEvent.Timestamp,
			cloudEvent.IsDispatched);

	private async Task<CloudAppendResult> AppendWithTransactionAsync(
		string streamId,
		string aggregateId,
		string aggregateType,
		List<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var transactItems = new List<TransactWriteItem>();
		var version = expectedVersion;

		foreach (var evt in events)
		{
			version++;
			var doc = CreateEventDocument(streamId, aggregateId, aggregateType, evt, version);

			transactItems.Add(new TransactWriteItem
			{
				Put = new Put
				{
					TableName = _options.EventsTableName,
					Item = doc,
					ConditionExpression = "attribute_not_exists(#pk)",
					ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = _options.PartitionKeyAttribute }
				}
			});
		}

		try
		{
			var request = new TransactWriteItemsRequest
			{
				TransactItems = transactItems,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var response = await _client.TransactWriteItemsAsync(request, cancellationToken)
				.ConfigureAwait(false);

			var totalCapacity = response.ConsumedCapacity?.Sum(c => c.CapacityUnits) ?? 0;

			LogEventsAppended(streamId, events.Count, totalCapacity);
			return CloudAppendResult.CreateSuccess(version, totalCapacity);
		}
		catch (TransactionCanceledException)
		{
			LogConcurrencyConflict(streamId, expectedVersion);
			return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, version, 0);
		}
	}

	private async Task<CloudAppendResult> AppendSequentiallyAsync(
		string streamId,
		string aggregateId,
		string aggregateType,
		List<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var version = expectedVersion;
		double totalCapacity = 0;

		foreach (var evt in events)
		{
			version++;
			var doc = CreateEventDocument(streamId, aggregateId, aggregateType, evt, version);

			var request = new PutItemRequest
			{
				TableName = _options.EventsTableName,
				Item = doc,
				ConditionExpression = "attribute_not_exists(#pk)",
				ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = _options.PartitionKeyAttribute },
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			try
			{
				var response = await _client.PutItemAsync(request, cancellationToken)
					.ConfigureAwait(false);
				totalCapacity += response.ConsumedCapacity?.CapacityUnits ?? 0;
			}
			catch (ConditionalCheckFailedException)
			{
				LogConcurrencyConflict(streamId, expectedVersion);
				return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, version, totalCapacity);
			}
		}

		LogEventsAppended(streamId, events.Count, totalCapacity);
		return CloudAppendResult.CreateSuccess(version, totalCapacity);
	}

	private Dictionary<string, AttributeValue> CreateEventDocument(
		string streamId,
		string aggregateId,
		string aggregateType,
		IDomainEvent evt,
		long version)
	{
		var eventTypeName = EventTypeNameHelper.GetEventTypeName(evt.GetType());

		return new Dictionary<string, AttributeValue>
		{
			[_options.PartitionKeyAttribute] = new AttributeValue { S = streamId },
			[_options.SortKeyAttribute] = new AttributeValue { N = version.ToString() },
			["eventId"] = new AttributeValue { S = evt.EventId.ToString() },
			["aggregateId"] = new AttributeValue { S = aggregateId },
			["aggregateType"] = new AttributeValue { S = aggregateType },
			["eventType"] = new AttributeValue { S = eventTypeName },
			["version"] = new AttributeValue { N = version.ToString() },
			["timestamp"] = new AttributeValue { S = evt.OccurredAt.ToString("O") },
			["eventData"] = new AttributeValue { S = Convert.ToBase64String(SerializeEvent(evt)) },
			["metadata"] = evt.Metadata != null
				? new AttributeValue { S = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(evt.Metadata)) }
				: new AttributeValue { NULL = true },
			["isDispatched"] = new AttributeValue { BOOL = false }
		};
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
			Metadata = item.TryGetValue("metadata", out var metaAttr) && !string.IsNullOrEmpty(metaAttr.S)
				? Convert.FromBase64String(metaAttr.S)
				: null,
			PartitionKeyValue = item[_options.PartitionKeyAttribute].S,
			DocumentId = $"{item[_options.PartitionKeyAttribute].S}:{item[_options.SortKeyAttribute].N}",
			IsDispatched = item.TryGetValue("isDispatched", out var dispatchedAttr) && dispatchedAttr.BOOL == true
		};
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
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

	private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
	{
		try
		{
			_ = await _client.DescribeTableAsync(_options.EventsTableName, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (ResourceNotFoundException)
		{
			var createRequest = new CreateTableRequest
			{
				TableName = _options.EventsTableName,
				KeySchema =
				[
					new KeySchemaElement(_options.PartitionKeyAttribute, Amazon.DynamoDBv2.KeyType.HASH),
					new KeySchemaElement(_options.SortKeyAttribute, Amazon.DynamoDBv2.KeyType.RANGE)
				],
				AttributeDefinitions =
				[
					new AttributeDefinition(_options.PartitionKeyAttribute, ScalarAttributeType.S),
					new AttributeDefinition(_options.SortKeyAttribute, ScalarAttributeType.N)
				]
			};

			if (_options.UseOnDemandCapacity)
			{
				createRequest.BillingMode = BillingMode.PAY_PER_REQUEST;
			}
			else
			{
				createRequest.BillingMode = BillingMode.PROVISIONED;
				createRequest.ProvisionedThroughput = new ProvisionedThroughput(
					_options.ReadCapacityUnits,
					_options.WriteCapacityUnits);
			}

			if (_options.EnableStreams)
			{
				createRequest.StreamSpecification = new StreamSpecification
				{
					StreamEnabled = true,
					StreamViewType = Amazon.DynamoDBv2.StreamViewType.NEW_IMAGE
				};
			}

			_ = await _client.CreateTableAsync(createRequest, cancellationToken).ConfigureAwait(false);

			// Wait for table to become active
			var describeRequest = new DescribeTableRequest { TableName = _options.EventsTableName };
			TableStatus? status;
			do
			{
				await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
				var response = await _client.DescribeTableAsync(describeRequest, cancellationToken)
					.ConfigureAwait(false);
				status = response.Table.TableStatus;
			} while (status != TableStatus.ACTIVE);
		}
	}
}
