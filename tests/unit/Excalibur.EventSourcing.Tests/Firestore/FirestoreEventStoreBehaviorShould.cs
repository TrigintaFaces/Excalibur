// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Firestore;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Firestore;

[Trait("Category", "Unit")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Behavior tests intentionally cover multiple Firestore event-store branches.")]
public sealed class FirestoreEventStoreBehaviorShould : UnitTestBase
{
	[Fact]
	public void ProviderType_ReturnFirestore()
	{
		var sut = CreateInitializedStore(withDatabase: false);
		sut.ProviderType.ShouldBe(CloudProviderType.Firestore);
	}

	[Fact]
	public async Task InitializeAsync_Throw_WhenOptionsAreInvalid()
	{
		var sut = new FirestoreEventStore(
			Options.Create(new FirestoreEventStoreOptions
			{
				ProjectId = null,
				EmulatorHost = null
			}),
			NullLogger<FirestoreEventStore>.Instance);

		await Should.ThrowAsync<InvalidOperationException>(() => sut.InitializeAsync(CancellationToken.None));
	}

	[Fact]
	public async Task InitializeAsync_ReturnImmediately_WhenAlreadyInitialized()
	{
		var sut = CreateInitializedStore(withDatabase: false);
		SetPrivateField(sut, "_initialized", true);

		await sut.InitializeAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AppendAsync_ReturnSuccess_WhenNoEvents()
	{
		var sut = CreateInitializedStore(withDatabase: true);

		var result = await sut.AppendAsync(
			"agg-1",
			"Order",
			new PartitionKey("Order:agg-1"),
			[],
			expectedVersion: 9,
			CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(9);
		result.RequestCharge.ShouldBe(0);
	}

	[Fact]
	public async Task IEventStoreAppendAsync_MapSuccess_WhenNoEvents()
	{
		var sut = CreateInitializedStore(withDatabase: true);
		var eventStore = (IEventStore)sut;

		var result = await eventStore.AppendAsync("agg-1", "Order", [], expectedVersion: 4, CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(4);
		result.FirstEventPosition.ShouldBe(0);
	}

	[Fact]
	public async Task LoadAsync_Throw_WhenDatabaseObjectIsUnavailable()
	{
		var sut = CreateInitializedStore(withDatabase: true);
		await Should.ThrowAsync<Exception>(() =>
			sut.LoadAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), null, CancellationToken.None));
	}

	[Fact]
	public async Task LoadFromVersionAsync_Throw_WhenDatabaseObjectIsUnavailable()
	{
		var sut = CreateInitializedStore(withDatabase: true);
		await Should.ThrowAsync<Exception>(() =>
			sut.LoadFromVersionAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), 5, null, CancellationToken.None));
	}

	[Fact]
	public async Task GetCurrentVersionAsync_Throw_WhenDatabaseObjectIsUnavailable()
	{
		var sut = CreateInitializedStore(withDatabase: true);
		await Should.ThrowAsync<Exception>(() =>
			sut.GetCurrentVersionAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), CancellationToken.None));
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_Throw_WhenDatabaseObjectIsUnavailable()
	{
		var sut = CreateInitializedStore(withDatabase: true);
		await Should.ThrowAsync<Exception>(() =>
			sut.GetUndispatchedEventsAsync(10, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_Throw_WhenDatabaseObjectIsUnavailable()
	{
		var sut = CreateInitializedStore(withDatabase: true);
		await Should.ThrowAsync<Exception>(() =>
			sut.MarkEventAsDispatchedAsync("evt-1", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task SubscribeToChangesAsync_ReturnSubscription_WhenInitialized()
	{
		var sut = CreateInitializedStore(withDatabase: true);

		var subscription = await sut.SubscribeToChangesAsync(options: null, CancellationToken.None);

		subscription.ShouldBeOfType<FirestoreEventStoreListenerSubscription>();
	}

	[Fact]
	public async Task EnsureInitialized_Throw_WhenDatabaseIsMissing()
	{
		var sut = CreateInitializedStore(withDatabase: false);
		await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.SubscribeToChangesAsync(options: null, CancellationToken.None));
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		var sut = CreateInitializedStore(withDatabase: false);

		await sut.DisposeAsync();
		await sut.DisposeAsync();
	}

	[Fact]
	public void BuildStreamId_CombineAggregateTypeAndId()
	{
		var method = typeof(FirestoreEventStore).GetMethod("BuildStreamId", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		method!.Invoke(null, ["Order", "agg-42"]).ShouldBe("Order:agg-42");
	}

	[Fact]
	public void ExtractCorrelationId_ResolveBothKeyCasings()
	{
		var method = typeof(FirestoreEventStore).GetMethod("ExtractCorrelationId", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		var upper = new IDomainEvent[] { new TestDomainEvent("evt-1", new Dictionary<string, object> { ["CorrelationId"] = "c1" }) };
		method!.Invoke(null, [upper]).ShouldBe("c1");

		var lower = new IDomainEvent[] { new TestDomainEvent("evt-2", new Dictionary<string, object> { ["correlationId"] = "c2" }) };
		method.Invoke(null, [lower]).ShouldBe("c2");

		var none = new IDomainEvent[] { new TestDomainEvent("evt-3", null) };
		method.Invoke(null, [none]).ShouldBeNull();
	}

	[Fact]
	public void ExtractEventId_ReturnFirstNonEmptyId()
	{
		var method = typeof(FirestoreEventStore).GetMethod("ExtractEventId", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		var events = new IDomainEvent[]
		{
			new TestDomainEvent("", null),
			new TestDomainEvent("evt-9", null),
			new TestDomainEvent("evt-10", null)
		};
		method!.Invoke(null, [events]).ShouldBe("evt-9");
	}

	[Fact]
	public void ToStoredEvent_MapCloudStoredEvent()
	{
		var method = typeof(FirestoreEventStore).GetMethod("ToStoredEvent", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		var cloud = new CloudStoredEvent
		{
			EventId = "evt-1",
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = [1, 2, 3],
			Metadata = [4, 5],
			Version = 8,
			Timestamp = DateTimeOffset.UtcNow,
			IsDispatched = true,
			PartitionKeyValue = "Order:agg-1"
		};

		var stored = (StoredEvent)method!.Invoke(null, [cloud])!;
		stored.EventId.ShouldBe("evt-1");
		stored.AggregateType.ShouldBe("Order");
		stored.Version.ShouldBe(8);
		stored.IsDispatched.ShouldBeTrue();
	}

	private static FirestoreEventStore CreateInitializedStore(bool withDatabase)
	{
		var sut = (FirestoreEventStore)RuntimeHelpers.GetUninitializedObject(typeof(FirestoreEventStore));
		SetPrivateField(
			sut,
			"_options",
			new FirestoreEventStoreOptions
			{
				ProjectId = "test-project",
				EventsCollectionName = "events",
				MaxBatchSize = 32
			});
		SetPrivateField(sut, "_logger", NullLogger<FirestoreEventStore>.Instance);
		SetPrivateField(sut, "_initialized", true);
		SetPrivateField(sut, "_initLock", new SemaphoreSlim(1, 1));

		if (withDatabase)
		{
			var db = (FirestoreDb)RuntimeHelpers.GetUninitializedObject(typeof(FirestoreDb));
			SetPrivateField(sut, "_db", db);
		}

		return sut;
	}

	private static void SetPrivateField(object instance, string fieldName, object? value)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(instance, value);
	}

	private sealed record TestDomainEvent(string EventId, IDictionary<string, object>? Metadata) : IDomainEvent
	{
		public string AggregateId => "agg-1";
		public long Version => 1;
		public DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
		public string EventType => "TestDomainEvent";
	}
}
