// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Threading.Channels;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.EventSourcing.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.CosmosDb;

[Trait("Category", "Unit")]
public sealed class CosmosDbEventStoreChangeFeedSubscriptionShould : UnitTestBase
{
	[Fact]
	public void ValidateConstructorGuards()
	{
		var container = A.Fake<Container>();
		var options = new CosmosDbEventStoreOptions();
		var logger = NullLogger.Instance;

		Should.Throw<ArgumentNullException>(() =>
			new CosmosDbEventStoreChangeFeedSubscription(null!, options, logger));
		Should.Throw<ArgumentNullException>(() =>
			new CosmosDbEventStoreChangeFeedSubscription(container, null!, logger));
		Should.Throw<ArgumentNullException>(() =>
			new CosmosDbEventStoreChangeFeedSubscription(container, options, null!));
	}

	[Fact]
	public void ExposeExpectedDefaultProperties()
	{
		var sut = CreateSubscription();

		sut.SubscriptionId.ShouldStartWith("cosmos-eventstore-");
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
			new Excalibur.Data.Abstractions.CloudNative.PartitionKey("Order:agg-1"),
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
			await Should.ThrowAsync<ObjectDisposedException>(() =>
				enumerator.MoveNextAsync().AsTask());
		}
		finally
		{
			await enumerator.DisposeAsync();
		}
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
	public void ToCloudStoredEvent_MapEventDocumentFields()
	{
		var assembly = typeof(CosmosDbEventStoreChangeFeedSubscription).Assembly;
		var eventDocumentType = assembly.GetType("Excalibur.EventSourcing.CosmosDb.EventDocument");
		eventDocumentType.ShouldNotBeNull();

		var doc = Activator.CreateInstance(eventDocumentType!)!;
		eventDocumentType!.GetProperty("Id")!.SetValue(doc, "doc-1");
		eventDocumentType.GetProperty("StreamId")!.SetValue(doc, "Order:agg-1");
		eventDocumentType.GetProperty("EventId")!.SetValue(doc, "evt-1");
		eventDocumentType.GetProperty("AggregateId")!.SetValue(doc, "agg-1");
		eventDocumentType.GetProperty("AggregateType")!.SetValue(doc, "Order");
		eventDocumentType.GetProperty("EventType")!.SetValue(doc, "OrderCreated");
		eventDocumentType.GetProperty("Version")!.SetValue(doc, 3L);
		eventDocumentType.GetProperty("Timestamp")!.SetValue(doc, DateTimeOffset.UtcNow);
		eventDocumentType.GetProperty("EventData")!.SetValue(doc, new byte[] { 1, 2, 3 });
		eventDocumentType.GetProperty("Metadata")!.SetValue(doc, new byte[] { 9, 8, 7 });
		eventDocumentType.GetProperty("IsDispatched")!.SetValue(doc, true);
		eventDocumentType.GetProperty("ETag")!.SetValue(doc, "etag-1");

		var mapMethod = typeof(CosmosDbEventStoreChangeFeedSubscription).GetMethod(
			"ToCloudStoredEvent",
			BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var mapped = (CloudStoredEvent)mapMethod!.Invoke(null, [doc])!;
		mapped.DocumentId.ShouldBe("doc-1");
		mapped.PartitionKeyValue.ShouldBe("Order:agg-1");
		mapped.EventId.ShouldBe("evt-1");
		mapped.EventType.ShouldBe("OrderCreated");
		mapped.Version.ShouldBe(3);
		mapped.IsDispatched.ShouldBeTrue();
		mapped.ETag.ShouldBe("etag-1");
	}

	[Fact]
	public void EventStoreChangeFeedEvent_StoreConstructorValues()
	{
		var assembly = typeof(CosmosDbEventStoreChangeFeedSubscription).Assembly;
		var changeFeedEventType = assembly.GetType("Excalibur.EventSourcing.CosmosDb.EventStoreChangeFeedEvent");
		changeFeedEventType.ShouldNotBeNull();

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
		var partitionKey = new Excalibur.Data.Abstractions.CloudNative.PartitionKey("Order:agg-42");

		var instance = Activator.CreateInstance(
			changeFeedEventType!,
			ChangeFeedEventType.Created,
			document,
			"doc-42",
			partitionKey,
			timestamp,
			"continuation-42",
			42L);

		instance.ShouldNotBeNull();
		changeFeedEventType!.GetProperty("EventType")!.GetValue(instance).ShouldBe(ChangeFeedEventType.Created);
		changeFeedEventType.GetProperty("Document")!.GetValue(instance).ShouldBeSameAs(document);
		changeFeedEventType.GetProperty("DocumentId")!.GetValue(instance).ShouldBe("doc-42");
		changeFeedEventType.GetProperty("PartitionKey")!.GetValue(instance).ShouldBe(partitionKey);
		changeFeedEventType.GetProperty("Timestamp")!.GetValue(instance).ShouldBe(timestamp);
		changeFeedEventType.GetProperty("ContinuationToken")!.GetValue(instance).ShouldBe("continuation-42");
		changeFeedEventType.GetProperty("SequenceNumber")!.GetValue(instance).ShouldBe(42L);
	}

	private static CosmosDbEventStoreChangeFeedSubscription CreateSubscription()
	{
		var container = A.Fake<Container>();
		var options = new CosmosDbEventStoreOptions
		{
			MaxBatchSize = 4,
			ChangeFeedPollIntervalMs = 1
		};

		return new CosmosDbEventStoreChangeFeedSubscription(container, options, NullLogger.Instance);
	}

	private static Channel<IChangeFeedEvent<CloudStoredEvent>> GetPrivateChannel(
		CosmosDbEventStoreChangeFeedSubscription subscription)
	{
		var field = typeof(CosmosDbEventStoreChangeFeedSubscription).GetField(
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
