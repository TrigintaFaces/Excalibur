// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading.Channels;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.EventSourcing.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;

using DynamoRecord = Amazon.DynamoDBStreams.Model.Record;

namespace Excalibur.EventSourcing.Tests.DynamoDb;

[Trait("Category", "Unit")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Subscription tests intentionally validate multiple DynamoDB Streams SDK flow paths.")]
public sealed class DynamoDbEventStoreStreamsSubscriptionShould : UnitTestBase
{
	[Fact]
	public void ValidateConstructorGuards()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		var streamsClient = A.Fake<IAmazonDynamoDBStreams>();
		var options = new DynamoDbEventStoreOptions();
		var logger = NullLogger.Instance;

		Should.Throw<ArgumentNullException>(() =>
			new DynamoDbEventStoreStreamsSubscription(null!, streamsClient, options, logger));
		Should.Throw<ArgumentNullException>(() =>
			new DynamoDbEventStoreStreamsSubscription(client, null!, options, logger));
		Should.Throw<ArgumentNullException>(() =>
			new DynamoDbEventStoreStreamsSubscription(client, streamsClient, null!, logger));
		Should.Throw<ArgumentNullException>(() =>
			new DynamoDbEventStoreStreamsSubscription(client, streamsClient, options, null!));
	}

	[Fact]
	public void ExposeExpectedDefaultState()
	{
		var sut = CreateSubscription();

		sut.SubscriptionId.ShouldStartWith("dynamodb-eventstore-");
		sut.IsActive.ShouldBeFalse();
		sut.CurrentContinuationToken.ShouldBeNull();
	}

	[Fact]
	public async Task StartAsync_ThrowObjectDisposedException_WhenDisposed()
	{
		var sut = CreateSubscription();
		await sut.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(() =>
			sut.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task StartAsync_ThrowInvalidOperationException_WhenStreamArnMissing()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.DescribeTableAsync("Events", A<CancellationToken>._))
			.Returns(Task.FromResult(new DescribeTableResponse
			{
				Table = new TableDescription { LatestStreamArn = null }
			}));

		var sut = CreateSubscription(client: client);

		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.StartAsync(CancellationToken.None));

		ex.Message.ShouldContain("DynamoDB Streams is not enabled");
	}

	[Fact]
	public async Task StartAsync_SetActive_WhenStreamArnExists()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		var streamsClient = A.Fake<IAmazonDynamoDBStreams>();
		_ = A.CallTo(() => client.DescribeTableAsync("Events", A<CancellationToken>._))
			.Returns(Task.FromResult(new DescribeTableResponse
			{
				Table = new TableDescription { LatestStreamArn = "arn:aws:dynamodb:us-east-1:123456789012:table/Events/stream/2026-01-01T00:00:00.000" }
			}));
		_ = A.CallTo(() => streamsClient.DescribeStreamAsync(A<DescribeStreamRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DescribeStreamResponse
			{
				StreamDescription = new StreamDescription { Shards = [] }
			}));

		var sut = CreateSubscription(client, streamsClient);

		await sut.StartAsync(CancellationToken.None);
		sut.IsActive.ShouldBeTrue();

		await sut.StopAsync(CancellationToken.None);
		sut.IsActive.ShouldBeFalse();
	}

	[Fact]
	public async Task StopAsync_ReturnWithoutThrow_WhenDisposed()
	{
		var sut = CreateSubscription();
		await sut.DisposeAsync();

		await sut.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ReadChangesAsync_YieldBufferedEvents_WhenActive()
	{
		var sut = CreateSubscription();
		SetPrivateField(sut, "_isActive", true);
		var channel = GetPrivateChannel(sut);

		var expected = new TestChangeFeedEvent(
			ChangeFeedEventType.Created,
			new CloudStoredEvent
			{
				EventId = "evt-1",
				AggregateId = "agg-1",
				AggregateType = "Order",
				EventType = "OrderCreated",
				EventData = [1],
				Version = 1,
				Timestamp = DateTimeOffset.UtcNow,
				PartitionKeyValue = "Order:agg-1",
				DocumentId = "doc-1"
			},
			"doc-1",
			new PartitionKey("Order:agg-1"),
			DateTimeOffset.UtcNow,
			"ct-1",
			1L);

		await channel.Writer.WriteAsync(expected);

		var enumerator = sut.ReadChangesAsync(CancellationToken.None).GetAsyncEnumerator();
		try
		{
			(await enumerator.MoveNextAsync()).ShouldBeTrue();
			enumerator.Current.ShouldBeSameAs(expected);

			SetPrivateField(sut, "_isActive", false);
			(await enumerator.MoveNextAsync()).ShouldBeFalse();
		}
		finally
		{
			await enumerator.DisposeAsync();
		}
	}

	[Fact]
	public async Task ReadChangesAsync_ThrowObjectDisposedException_WhenDisposed()
	{
		var sut = CreateSubscription();
		await sut.DisposeAsync();

		var enumerator = sut.ReadChangesAsync(CancellationToken.None).GetAsyncEnumerator();
		try
		{
			await Should.ThrowAsync<ObjectDisposedException>(() => enumerator.MoveNextAsync().AsTask());
		}
		finally
		{
			await enumerator.DisposeAsync();
		}
	}

	[Fact]
	public async Task PollStreamsAsync_MapEventTypesAndPublishChanges()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		var streamsClient = A.Fake<IAmazonDynamoDBStreams>();
		_ = A.CallTo(() => streamsClient.DescribeStreamAsync(A<DescribeStreamRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DescribeStreamResponse
			{
				StreamDescription = new StreamDescription
				{
					Shards = [new Shard { ShardId = "shardId-00000001720000000000-aaaaaaaa" }]
				}
			}));
		_ = A.CallTo(() => streamsClient.GetShardIteratorAsync(A<GetShardIteratorRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new GetShardIteratorResponse
			{
				ShardIterator = "iterator-1"
			}));

		A.CallTo(() => streamsClient.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(
				Task.FromResult(new GetRecordsResponse
				{
					Records =
					[
						CreateRecord("evt-created", OperationType.INSERT),
						CreateRecord("evt-updated", OperationType.MODIFY),
						CreateRecord("evt-deleted", OperationType.REMOVE),
						CreateRecord("evt-default", OperationType.FindValue("UPSERT"))
					],
					NextShardIterator = "iterator-2"
				}),
				Task.FromException<GetRecordsResponse>(new OperationCanceledException()));

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		var sut = CreateSubscription(client, streamsClient);
		SetPrivateField(sut, "_isActive", true);
		SetPrivateField(sut, "_streamArn", "arn:aws:dynamodb:us-east-1:123456789012:table/Events/stream/2026-01-01T00:00:00.000");

		var pollMethod = typeof(DynamoDbEventStoreStreamsSubscription).GetMethod(
			"PollStreamsAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		pollMethod.ShouldNotBeNull();

		await ((Task)pollMethod!.Invoke(sut, [cts.Token])!).ConfigureAwait(false);

		var changes = await ReadAllAvailableChangesAsync(sut).ConfigureAwait(false);
		changes.Count.ShouldBe(4);
		changes[0].EventType.ShouldBe(ChangeFeedEventType.Created);
		changes[1].EventType.ShouldBe(ChangeFeedEventType.Updated);
		changes[2].EventType.ShouldBe(ChangeFeedEventType.Deleted);
		changes[3].EventType.ShouldBe(ChangeFeedEventType.Created);

		sut.CurrentContinuationToken.ShouldBe("iterator-2");
		changes[0].Document.ShouldNotBeNull();
		changes[0].Document!.EventId.ShouldBe("evt-created");
		changes[0].Document.PartitionKeyValue.ShouldBe("Order:agg-1");
	}

	[Fact]
	public void ToCloudStoredEvent_MapAttributes()
	{
		var sut = CreateSubscription();
		var method = typeof(DynamoDbEventStoreStreamsSubscription).GetMethod(
			"ToCloudStoredEvent",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var cloudEvent = (CloudStoredEvent)method!.Invoke(sut, [CreateImage("evt-1")])!;

		cloudEvent.EventId.ShouldBe("evt-1");
		cloudEvent.AggregateId.ShouldBe("agg-1");
		cloudEvent.AggregateType.ShouldBe("Order");
		cloudEvent.EventType.ShouldBe("OrderCreated");
		cloudEvent.Version.ShouldBe(1);
		cloudEvent.DocumentId.ShouldBe("Order:agg-1:1");
		cloudEvent.Metadata.ShouldNotBeNull();
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		var sut = CreateSubscription();

		await sut.DisposeAsync();
		await sut.DisposeAsync();

		SetPrivateField(sut, "_isActive", true);
		sut.IsActive.ShouldBeFalse();
	}

	[Fact]
	public void DynamoDbStreamsFeedEvent_StoreConstructorValues()
	{
		var assembly = typeof(DynamoDbEventStoreStreamsSubscription).Assembly;
		var eventType = assembly.GetType("Excalibur.EventSourcing.DynamoDb.DynamoDbStreamsFeedEvent");
		eventType.ShouldNotBeNull();

		var timestamp = DateTimeOffset.UtcNow;
		var document = new CloudStoredEvent
		{
			EventId = "evt-42",
			AggregateId = "agg-42",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = [4, 2],
			Version = 42,
			Timestamp = timestamp,
			PartitionKeyValue = "Order:agg-42"
		};
		var partitionKey = new PartitionKey("Order:agg-42");

		var instance = Activator.CreateInstance(
			eventType!,
			ChangeFeedEventType.Created,
			document,
			"doc-42",
			partitionKey,
			timestamp,
			"continuation-42",
			42L);

		instance.ShouldNotBeNull();
		eventType!.GetProperty("EventType")!.GetValue(instance).ShouldBe(ChangeFeedEventType.Created);
		eventType.GetProperty("Document")!.GetValue(instance).ShouldBe(document);
		eventType.GetProperty("DocumentId")!.GetValue(instance).ShouldBe("doc-42");
		eventType.GetProperty("PartitionKey")!.GetValue(instance).ShouldBe(partitionKey);
		eventType.GetProperty("Timestamp")!.GetValue(instance).ShouldBe(timestamp);
		eventType.GetProperty("ContinuationToken")!.GetValue(instance).ShouldBe("continuation-42");
		eventType.GetProperty("SequenceNumber")!.GetValue(instance).ShouldBe(42L);
	}

	private static DynamoDbEventStoreStreamsSubscription CreateSubscription(
		IAmazonDynamoDB? client = null,
		IAmazonDynamoDBStreams? streamsClient = null)
	{
		var options = new DynamoDbEventStoreOptions
		{
			EventsTableName = "Events",
			PartitionKeyAttribute = "pk",
			SortKeyAttribute = "sk",
			MaxBatchSize = 4,
			StreamsPollIntervalMs = 1
		};

		return new DynamoDbEventStoreStreamsSubscription(
			client ?? A.Fake<IAmazonDynamoDB>(),
			streamsClient ?? A.Fake<IAmazonDynamoDBStreams>(),
			options,
			NullLogger.Instance);
	}

	private static DynamoRecord CreateRecord(string eventId, OperationType operationType)
	{
		return new DynamoRecord
		{
			EventID = $"rec-{eventId}",
			EventName = operationType,
			Dynamodb = new StreamRecord
			{
				SequenceNumber = "123456789012345678901",
				NewImage = CreateImage(eventId)
			}
		};
	}

	private static Dictionary<string, Amazon.DynamoDBStreams.Model.AttributeValue> CreateImage(string eventId)
	{
		return new Dictionary<string, Amazon.DynamoDBStreams.Model.AttributeValue>
		{
			["pk"] = new() { S = "Order:agg-1" },
			["sk"] = new() { N = "1" },
			["eventId"] = new() { S = eventId },
			["aggregateId"] = new() { S = "agg-1" },
			["aggregateType"] = new() { S = "Order" },
			["eventType"] = new() { S = "OrderCreated" },
			["version"] = new() { N = "1" },
			["timestamp"] = new() { S = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
			["eventData"] = new() { S = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"id\":1}")) },
			["metadata"] = new() { S = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"m\":\"v\"}")) },
			["isDispatched"] = new() { BOOL = false }
		};
	}

	private static async Task<List<IChangeFeedEvent<CloudStoredEvent>>> ReadAllAvailableChangesAsync(
		DynamoDbEventStoreStreamsSubscription subscription)
	{
		var channel = GetPrivateChannel(subscription);
		var events = new List<IChangeFeedEvent<CloudStoredEvent>>();

		while (channel.Reader.TryRead(out var change))
		{
			events.Add(change);
		}

		await Task.CompletedTask;
		return events;
	}

	private static Channel<IChangeFeedEvent<CloudStoredEvent>> GetPrivateChannel(
		DynamoDbEventStoreStreamsSubscription subscription)
	{
		var field = typeof(DynamoDbEventStoreStreamsSubscription).GetField(
			"_channel",
			BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		return (Channel<IChangeFeedEvent<CloudStoredEvent>>)field!.GetValue(subscription)!;
	}

	private static void SetPrivateField(object instance, string fieldName, object value)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(instance, value);
	}

	private sealed class TestChangeFeedEvent : IChangeFeedEvent<CloudStoredEvent>
	{
		public TestChangeFeedEvent(
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

		public ChangeFeedEventType EventType { get; }
		public CloudStoredEvent? Document { get; }
		public string DocumentId { get; }
		public IPartitionKey PartitionKey { get; }
		public DateTimeOffset Timestamp { get; }
		public string ContinuationToken { get; }
		public long SequenceNumber { get; }
	}
}
