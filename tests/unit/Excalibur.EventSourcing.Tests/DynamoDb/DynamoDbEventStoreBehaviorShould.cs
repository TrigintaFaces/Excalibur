// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.DynamoDb;

[Trait("Category", "Unit")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Behavior tests intentionally exercise multiple AWS SDK branches and request shapes.")]
public sealed class DynamoDbEventStoreBehaviorShould : UnitTestBase
{
	[Fact]
	public async Task LoadAsync_ReturnPagedEventsAndCapacity()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		var response1 = new QueryResponse
		{
			ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.5 },
			Items = [CreateItem("Order:agg-1", "evt-1", 1)],
			LastEvaluatedKey = new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "continue" } }
		};
		var response2 = new QueryResponse
		{
			ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 2.0 },
			Items = [CreateItem("Order:agg-1", "evt-2", 2)],
			LastEvaluatedKey = []
		};

		A.CallTo(() => client.QueryAsync(A<QueryRequest>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(Task.FromResult(response1), Task.FromResult(response2));

		var sut = CreateStore(client);

		var result = await sut.LoadAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), null, CancellationToken.None);

		result.Events.Count.ShouldBe(2);
		result.Events[0].EventId.ShouldBe("evt-1");
		result.Events[1].EventId.ShouldBe("evt-2");
		result.RequestCharge.ShouldBe(3.5);
	}

	[Fact]
	public async Task LoadFromVersionAsync_UseVersionPredicateAndReturnEvents()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.QueryAsync(A<QueryRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new QueryResponse
			{
				ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 },
				Items = [CreateItem("Order:agg-1", "evt-9", 9)],
				LastEvaluatedKey = []
			}));

		var sut = CreateStore(client);

		var result = await sut.LoadFromVersionAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), 5, null, CancellationToken.None);

		result.Events.Count.ShouldBe(1);
		result.Events[0].Version.ShouldBe(9);
		A.CallTo(() => client.QueryAsync(
				A<QueryRequest>.That.Matches(r =>
					r.KeyConditionExpression.Contains(" > :version", StringComparison.Ordinal) &&
					r.ExpressionAttributeValues.ContainsKey(":version")),
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task AppendAsync_ReturnSuccess_WhenNoEvents()
	{
		var sut = CreateStore(A.Fake<IAmazonDynamoDB>());

		var result = await sut.AppendAsync(
			"agg-1",
			"Order",
			new PartitionKey("Order:agg-1"),
			[],
			expectedVersion: 7,
			CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(7);
		result.RequestCharge.ShouldBe(0);
	}

	[Fact]
	public async Task AppendAsync_TransactionalWrite_ReturnSuccess()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.TransactWriteItemsAsync(A<TransactWriteItemsRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new TransactWriteItemsResponse
			{
				ConsumedCapacity = [new ConsumedCapacity { CapacityUnits = 2.5 }]
			}));

		var sut = CreateStore(client, configure: options => options.UseTransactionalWrite = true);
		var events = new IDomainEvent[] { new TestDomainEvent("evt-1"), new TestDomainEvent("evt-2") };

		var result = await sut.AppendAsync(
			"agg-1",
			"Order",
			new PartitionKey("Order:agg-1"),
			events,
			expectedVersion: 0,
			CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(2);
		result.RequestCharge.ShouldBe(2.5);
	}

	[Fact]
	public async Task AppendAsync_TransactionalWrite_ReturnConflict_WhenTransactionCanceled()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.TransactWriteItemsAsync(A<TransactWriteItemsRequest>._, A<CancellationToken>._))
			.ThrowsAsync(new TransactionCanceledException("conflict"));

		var sut = CreateStore(client, configure: options => options.UseTransactionalWrite = true);
		var events = new IDomainEvent[] { new TestDomainEvent("evt-1"), new TestDomainEvent("evt-2") };

		var result = await sut.AppendAsync(
			"agg-1",
			"Order",
			new PartitionKey("Order:agg-1"),
			events,
			expectedVersion: 0,
			CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(2);
	}

	[Fact]
	public async Task AppendAsync_SequentialWrite_ReturnConflict_WhenConditionalCheckFails()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.PutItemAsync(A<PutItemRequest>._, A<CancellationToken>._))
			.ThrowsAsync(new ConditionalCheckFailedException("conflict"));

		var sut = CreateStore(client, configure: options => options.UseTransactionalWrite = false);
		var events = new IDomainEvent[] { new TestDomainEvent("evt-1") };

		var result = await sut.AppendAsync(
			"agg-1",
			"Order",
			new PartitionKey("Order:agg-1"),
			events,
			expectedVersion: 3,
			CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(4);
	}

	[Fact]
	public async Task AppendAsync_FallBackToSequentialWrite_WhenEventCountExceedsTransactionLimit()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.PutItemAsync(A<PutItemRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new PutItemResponse
			{
				ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 0.5 }
			}));

		var sut = CreateStore(client, configure: options => options.UseTransactionalWrite = true);
		var events = Enumerable.Range(1, 101)
			.Select(i => (IDomainEvent)new TestDomainEvent($"evt-{i}"))
			.ToArray();

		var result = await sut.AppendAsync(
			"agg-1",
			"Order",
			new PartitionKey("Order:agg-1"),
			events,
			expectedVersion: 0,
			CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(101);
		result.RequestCharge.ShouldBe(50.5);

		A.CallTo(() => client.TransactWriteItemsAsync(A<TransactWriteItemsRequest>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => client.PutItemAsync(A<PutItemRequest>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task AppendAsync_Throw_WhenSequentialWriteFailsUnexpectedly()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.PutItemAsync(A<PutItemRequest>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("boom"));

		var sut = CreateStore(client, configure: options => options.UseTransactionalWrite = false);
		var events = new IDomainEvent[] { new TestDomainEvent("evt-1") };

		await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.AppendAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), events, expectedVersion: 0, CancellationToken.None));
	}

	[Fact]
	public async Task GetCurrentVersionAsync_ReturnMinusOne_WhenStreamEmpty()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.QueryAsync(A<QueryRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new QueryResponse { Items = [] }));

		var sut = CreateStore(client);

		var version = await sut.GetCurrentVersionAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), CancellationToken.None);

		version.ShouldBe(-1);
	}

	[Fact]
	public async Task GetCurrentVersionAsync_ReturnParsedVersion()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.QueryAsync(A<QueryRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new QueryResponse
			{
				Items =
				[
					new Dictionary<string, AttributeValue>
					{
						["version"] = new AttributeValue { N = "42" }
					}
				]
			}));

		var sut = CreateStore(client);

		var version = await sut.GetCurrentVersionAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), CancellationToken.None);

		version.ShouldBe(42);
	}

	[Fact]
	public async Task GetCurrentVersionAsync_ReturnMinusOne_WhenVersionIsNotNumeric()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.QueryAsync(A<QueryRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new QueryResponse
			{
				Items =
				[
					new Dictionary<string, AttributeValue>
					{
						["version"] = new AttributeValue { N = "not-a-number" }
					}
				]
			}));

		var sut = CreateStore(client);

		var version = await sut.GetCurrentVersionAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), CancellationToken.None);

		version.ShouldBe(-1);
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_ReturnMappedEvents()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.ScanAsync(A<ScanRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new ScanResponse
			{
				Items = [CreateItem("Order:agg-1", "evt-1", 1, isDispatched: false)]
			}));

		var sut = CreateStore(client);

		var events = await sut.GetUndispatchedEventsAsync(20, CancellationToken.None);

		events.Count.ShouldBe(1);
		events[0].EventId.ShouldBe("evt-1");
		events[0].AggregateType.ShouldBe("Order");
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_UpdateItem_WhenEventExists()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.ScanAsync(A<ScanRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new ScanResponse
			{
				Items =
				[
					new Dictionary<string, AttributeValue>
					{
						["pk"] = new AttributeValue { S = "Order:agg-1" },
						["sk"] = new AttributeValue { N = "1" }
					}
				]
			}));
		_ = A.CallTo(() => client.UpdateItemAsync(A<UpdateItemRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new UpdateItemResponse()));

		var sut = CreateStore(client);

		await sut.MarkEventAsDispatchedAsync("evt-1", CancellationToken.None);

		A.CallTo(() => client.UpdateItemAsync(
				A<UpdateItemRequest>.That.Matches(r =>
					r.Key["pk"].S == "Order:agg-1" &&
					r.Key["sk"].N == "1" &&
					r.ExpressionAttributeValues[":dispatched"].BOOL == true),
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_SkipUpdate_WhenEventMissing()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.ScanAsync(A<ScanRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new ScanResponse { Items = [] }));

		var sut = CreateStore(client);

		await sut.MarkEventAsDispatchedAsync("evt-1", CancellationToken.None);

		A.CallTo(() => client.UpdateItemAsync(A<UpdateItemRequest>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_Throw_WhenScanFails()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.ScanAsync(A<ScanRequest>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("scan failed"));

		var sut = CreateStore(client);

		await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.MarkEventAsDispatchedAsync("evt-1", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task IEventStoreAppendAsync_MapCloudConflictToAppendResultConflict()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.PutItemAsync(A<PutItemRequest>._, A<CancellationToken>._))
			.ThrowsAsync(new ConditionalCheckFailedException("conflict"));

		var sut = CreateStore(client, configure: options => options.UseTransactionalWrite = false);
		var eventStore = (IEventStore)sut;
		var events = new IDomainEvent[] { new TestDomainEvent("evt-1") };

		var result = await eventStore.AppendAsync("agg-1", "Order", events, expectedVersion: 4, CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(5);
	}

	[Fact]
	public async Task IEventStoreLoadAsync_MapCloudEventsToStoredEvents()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.QueryAsync(A<QueryRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new QueryResponse
			{
				Items = [CreateItem("Order:agg-1", "evt-1", 1)],
				LastEvaluatedKey = []
			}));

		var sut = CreateStore(client);
		var eventStore = (IEventStore)sut;

		var events = await eventStore.LoadAsync("agg-1", "Order", CancellationToken.None);

		events.Count.ShouldBe(1);
		events[0].EventId.ShouldBe("evt-1");
		events[0].AggregateType.ShouldBe("Order");
		events[0].Version.ShouldBe(1);
	}

	[Fact]
	public async Task IEventStoreLoadAsyncFromVersion_MapCloudEventsToStoredEvents()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.QueryAsync(A<QueryRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new QueryResponse
			{
				Items = [CreateItem("Order:agg-1", "evt-7", 7)],
				LastEvaluatedKey = []
			}));

		var sut = CreateStore(client);
		var eventStore = (IEventStore)sut;

		var events = await eventStore.LoadAsync("agg-1", "Order", fromVersion: 5, CancellationToken.None);

		events.Count.ShouldBe(1);
		events[0].EventId.ShouldBe("evt-7");
		events[0].Version.ShouldBe(7);
	}

	[Fact]
	public async Task AppendAsync_InitializeAndCreateTable_WhenMissing()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.DescribeTableAsync("Events", A<CancellationToken>._))
			.Throws(new ResourceNotFoundException("missing"));
		_ = A.CallTo(() => client.CreateTableAsync(A<CreateTableRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new CreateTableResponse()));
		_ = A.CallTo(() => client.DescribeTableAsync(A<DescribeTableRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DescribeTableResponse
			{
				Table = new TableDescription { TableStatus = TableStatus.ACTIVE }
			}));

		var sut = CreateStore(client, configure: options =>
		{
			options.CreateTableIfNotExists = true;
			options.UseOnDemandCapacity = true;
			options.EnableStreams = true;
		});

		var result = await sut.AppendAsync(
			"agg-1",
			"Order",
			new PartitionKey("Order:agg-1"),
			[],
			expectedVersion: 3,
			CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(3);

		A.CallTo(() => client.CreateTableAsync(
				A<CreateTableRequest>.That.Matches(r =>
					r.BillingMode == BillingMode.PAY_PER_REQUEST &&
					r.StreamSpecification != null &&
					r.StreamSpecification.StreamEnabled == true &&
					r.StreamSpecification.StreamViewType == Amazon.DynamoDBv2.StreamViewType.NEW_IMAGE),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AppendAsync_InitializeWithProvisionedCapacity_WhenConfigured()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.DescribeTableAsync("Events", A<CancellationToken>._))
			.Throws(new ResourceNotFoundException("missing"));
		_ = A.CallTo(() => client.CreateTableAsync(A<CreateTableRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new CreateTableResponse()));
		_ = A.CallTo(() => client.DescribeTableAsync(A<DescribeTableRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DescribeTableResponse
			{
				Table = new TableDescription { TableStatus = TableStatus.ACTIVE }
			}));

		var sut = CreateStore(client, configure: options =>
		{
			options.CreateTableIfNotExists = true;
			options.UseOnDemandCapacity = false;
			options.ReadCapacityUnits = 7;
			options.WriteCapacityUnits = 9;
			options.EnableStreams = false;
		});

		_ = await sut.AppendAsync(
			"agg-1",
			"Order",
			new PartitionKey("Order:agg-1"),
			[],
			expectedVersion: 0,
			CancellationToken.None);

		A.CallTo(() => client.CreateTableAsync(
				A<CreateTableRequest>.That.Matches(r =>
					r.BillingMode == BillingMode.PROVISIONED &&
					r.ProvisionedThroughput != null &&
					r.ProvisionedThroughput.ReadCapacityUnits == 7 &&
					r.ProvisionedThroughput.WriteCapacityUnits == 9 &&
					r.StreamSpecification == null),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SubscribeToChangesAsync_ReturnStreamsSubscription()
	{
		var sut = CreateStore(A.Fake<IAmazonDynamoDB>(), configure: options => options.CreateTableIfNotExists = false);

		var subscription = await sut.SubscribeToChangesAsync(options: null, CancellationToken.None);

		subscription.ShouldBeOfType<DynamoDbEventStoreStreamsSubscription>();
	}

	private static DynamoDbEventStore CreateStore(
		IAmazonDynamoDB client,
		Action<DynamoDbEventStoreOptions>? configure = null)
	{
		var options = new DynamoDbEventStoreOptions
		{
			EventsTableName = "Events",
			PartitionKeyAttribute = "pk",
			SortKeyAttribute = "sk",
			CreateTableIfNotExists = false,
			UseTransactionalWrite = true
		};
		configure?.Invoke(options);

		return new DynamoDbEventStore(
			client,
			A.Fake<IAmazonDynamoDBStreams>(),
			Options.Create(options),
			NullLogger<DynamoDbEventStore>.Instance);
	}

	private static Dictionary<string, AttributeValue> CreateItem(
		string streamId,
		string eventId,
		long version,
		bool isDispatched = false)
	{
		return new Dictionary<string, AttributeValue>
		{
			["pk"] = new AttributeValue { S = streamId },
			["sk"] = new AttributeValue { N = version.ToString() },
			["eventId"] = new AttributeValue { S = eventId },
			["aggregateId"] = new AttributeValue { S = "agg-1" },
			["aggregateType"] = new AttributeValue { S = "Order" },
			["eventType"] = new AttributeValue { S = "OrderCreated" },
			["version"] = new AttributeValue { N = version.ToString() },
			["timestamp"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") },
			["eventData"] = new AttributeValue { S = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"id\":1}")) },
			["metadata"] = new AttributeValue { NULL = true },
			["isDispatched"] = new AttributeValue { BOOL = isDispatched }
		};
	}

	private sealed record TestDomainEvent(string EventId) : IDomainEvent
	{
		public string AggregateId => "agg-1";
		public long Version => 1;
		public DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
		public string EventType => "TestDomainEvent";
		public IDictionary<string, object>? Metadata => null;
	}
}
