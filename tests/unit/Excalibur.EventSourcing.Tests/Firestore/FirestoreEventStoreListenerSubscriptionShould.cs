// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.EventSourcing.Firestore;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Firestore;

[Trait("Category", "Unit")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Subscription tests cover lifecycle and feed materialization branches.")]
public sealed class FirestoreEventStoreListenerSubscriptionShould : UnitTestBase
{
	[Fact]
	public void ValidateConstructorGuards()
	{
		var db = CreateDb();
		var options = new FirestoreEventStoreOptions();
		var logger = NullLogger.Instance;

		Should.Throw<ArgumentNullException>(() => new FirestoreEventStoreListenerSubscription(null!, options, logger));
		Should.Throw<ArgumentNullException>(() => new FirestoreEventStoreListenerSubscription(db, null!, logger));
		Should.Throw<ArgumentNullException>(() => new FirestoreEventStoreListenerSubscription(db, options, null!));
	}

	[Fact]
	public void ExposeExpectedDefaultProperties()
	{
		var sut = CreateSubscription();

		sut.SubscriptionId.ShouldStartWith("firestore-eventstore-");
		sut.IsActive.ShouldBeFalse();
		sut.CurrentContinuationToken.ShouldBeNull();
	}

	[Fact]
	public async Task StartAsync_ThrowObjectDisposedException_WhenDisposed()
	{
		var sut = CreateSubscription();
		await sut.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(() => sut.StartAsync(CancellationToken.None));
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
	public void ProcessSnapshot_ReturnWithoutThrow_WhenInactive()
	{
		var sut = CreateSubscription();
		var method = typeof(FirestoreEventStoreListenerSubscription).GetMethod(
			"ProcessSnapshot",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();
		var snapshot = (QuerySnapshot)RuntimeHelpers.GetUninitializedObject(typeof(QuerySnapshot));

		_ = method!.Invoke(sut, [snapshot]);
	}

	[Fact]
	public void ProcessSnapshot_ReturnWithoutThrow_WhenDisposed()
	{
		var sut = CreateSubscription();
		SetPrivateField(sut, "_isActive", true);
		SetPrivateField(sut, "_disposed", true);
		var method = typeof(FirestoreEventStoreListenerSubscription).GetMethod(
			"ProcessSnapshot",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();
		var snapshot = (QuerySnapshot)RuntimeHelpers.GetUninitializedObject(typeof(QuerySnapshot));

		_ = method!.Invoke(sut, [snapshot]);
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
	public void FirestoreEventStoreFeedEvent_StoreConstructorValues()
	{
		var assembly = typeof(FirestoreEventStoreListenerSubscription).Assembly;
		var eventType = assembly.GetType("Excalibur.EventSourcing.Firestore.FirestoreEventStoreFeedEvent");
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

	private static FirestoreEventStoreListenerSubscription CreateSubscription()
	{
		return new FirestoreEventStoreListenerSubscription(
			CreateDb(),
			new FirestoreEventStoreOptions
			{
				ProjectId = "test-project",
				EventsCollectionName = "events",
				MaxBatchSize = 4
			},
			NullLogger.Instance);
	}

	private static FirestoreDb CreateDb() =>
		(FirestoreDb)RuntimeHelpers.GetUninitializedObject(typeof(FirestoreDb));

	private static Channel<IChangeFeedEvent<CloudStoredEvent>> GetPrivateChannel(
		FirestoreEventStoreListenerSubscription subscription)
	{
		var field = typeof(FirestoreEventStoreListenerSubscription).GetField(
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
