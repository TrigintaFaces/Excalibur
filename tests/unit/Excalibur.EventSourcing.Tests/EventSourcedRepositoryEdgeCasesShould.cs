// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Justification: Test methods constructing EventSourcedRepository with full DI parameters inherently exceed coupling threshold
#pragma warning disable CA1506

using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Versioning;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Implementation;

using FakeItEasy;

using Shouldly;

using Xunit;

using IEventStore = Excalibur.EventSourcing.Abstractions.IEventStore;
using ISnapshotManager = Excalibur.EventSourcing.Abstractions.ISnapshotManager;
using ISnapshotStrategy = Excalibur.EventSourcing.Abstractions.ISnapshotStrategy;
using StoredEvent = Excalibur.EventSourcing.Abstractions.StoredEvent;
using AppendResult = Excalibur.EventSourcing.Abstractions.AppendResult;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Edge case tests for <see cref="EventSourcedRepository{TAggregate}"/> to improve coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventSourcedRepositoryEdgeCasesShould
{
	#region Test Types

	private sealed record TestDomainEvent : DomainEvent, IVersionedMessage
	{
		public string Data { get; init; } = string.Empty;
		int IVersionedMessage.Version => 1;

		public TestDomainEvent(string aggregateId, long version, string data, TimeProvider? timeProvider = null)
			: base(aggregateId, version, timeProvider ?? TimeProvider.System)
		{
			Data = data;
		}

		public TestDomainEvent() : base("", 0, TimeProvider.System) { }
	}

	private sealed record NonVersionedEvent : DomainEvent
	{
		public string Value { get; init; } = string.Empty;

		public NonVersionedEvent(string aggregateId, long version, string value, TimeProvider? timeProvider = null)
			: base(aggregateId, version, timeProvider ?? TimeProvider.System)
		{
			Value = value;
		}

		public NonVersionedEvent() : base("", 0, TimeProvider.System) { }
	}

	private sealed class EdgeCaseAggregate : AggregateRoot
	{
		public EdgeCaseAggregate() { }
		public EdgeCaseAggregate(string id) : base(id) { }

		public string Data { get; private set; } = string.Empty;
		public string Value { get; private set; } = string.Empty;

		public void SetData(string data)
		{
			RaiseEvent(new TestDomainEvent(Id, Version, data));
		}

		public void SetValue(string value)
		{
			RaiseEvent(new NonVersionedEvent(Id, Version, value));
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			switch (@event)
			{
				case TestDomainEvent testEvent:
					Data = testEvent.Data;
					break;
				case NonVersionedEvent nonVersioned:
					Value = nonVersioned.Value;
					break;
			}
		}
	}

	#endregion

	#region Snapshot Integration Tests

	[Fact]
	public async Task GetByIdAsync_ShouldLoadFromSnapshot_WhenSnapshotExists()
	{
		// Arrange
		var aggregateId = "agg-snapshot-1";
		var eventStore = A.Fake<IEventStore>();
		var snapshotManager = A.Fake<ISnapshotManager>();

		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			AggregateType = "EdgeCaseAggregate",
			Version = 5,
			Data = JsonSerializer.SerializeToUtf8Bytes(new { Data = "snapshot-data" }),
			CreatedAt = DateTime.UtcNow,
		};

		_ = A.CallTo(() => snapshotManager.GetLatestSnapshotAsync(aggregateId, A<CancellationToken>._))
			.Returns(snapshot);
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var serializer = CreateMockSerializer();

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			snapshotManager: snapshotManager);

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		// Verify snapshot was loaded
		A.CallTo(() => snapshotManager.GetLatestSnapshotAsync(aggregateId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		// Events are loaded starting from snapshot version
		A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", 4, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SaveAsync_ShouldCreateSnapshot_WhenStrategyIndicates()
	{
		// Arrange
		var aggregateId = "agg-snapshot-save-1";
		var aggregate = new EdgeCaseAggregate(aggregateId);
		aggregate.SetData("test");

		var eventStore = A.Fake<IEventStore>();
		var snapshotManager = A.Fake<ISnapshotManager>();
		var snapshotStrategy = A.Fake<ISnapshotStrategy>();
		var serializer = CreateMockSerializer();

		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));
		_ = A.CallTo(() => snapshotStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._))
			.Returns(true);
		// Use the specific type for the generic method
		_ = A.CallTo(() => snapshotManager.CreateSnapshotAsync(A<EdgeCaseAggregate>._, A<CancellationToken>._))
			.Returns(new Snapshot
			{
				SnapshotId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				AggregateType = "EdgeCaseAggregate",
				Version = 1,
				Data = [],
				CreatedAt = DateTime.UtcNow,
			});

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			snapshotManager: snapshotManager,
			snapshotStrategy: snapshotStrategy);

		// Act
		await repository.SaveAsync(aggregate, CancellationToken.None);

		// Assert
		A.CallTo(() => snapshotStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => snapshotManager.CreateSnapshotAsync(A<EdgeCaseAggregate>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => snapshotManager.SaveSnapshotAsync(aggregateId, A<ISnapshot>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SaveAsync_ShouldNotCreateSnapshot_WhenStrategyIndicatesNo()
	{
		// Arrange
		var aggregate = new EdgeCaseAggregate("agg-no-snapshot");
		aggregate.SetData("test");

		var eventStore = A.Fake<IEventStore>();
		var snapshotManager = A.Fake<ISnapshotManager>();
		var snapshotStrategy = A.Fake<ISnapshotStrategy>();
		var serializer = CreateMockSerializer();

		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));
		_ = A.CallTo(() => snapshotStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._))
			.Returns(false);

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			snapshotManager: snapshotManager,
			snapshotStrategy: snapshotStrategy);

		// Act
		await repository.SaveAsync(aggregate, CancellationToken.None);

		// Assert
		A.CallTo(() => snapshotManager.CreateSnapshotAsync(A<EdgeCaseAggregate>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region ETag Update Tests

	[Fact]
	public async Task SaveAsync_ShouldUpdateETagAfterSave()
	{
		// Arrange
		var aggregateId = "agg-etag";
		var aggregate = new EdgeCaseAggregate(aggregateId);
		aggregate.SetData("test");

		var eventStore = A.Fake<IEventStore>();
		var serializer = CreateMockSerializer();

		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id));

		// Act
		await repository.SaveAsync(aggregate, CancellationToken.None);

		// Assert
		aggregate.ETag.ShouldNotBeNullOrEmpty();
		aggregate.ETag.ShouldContain("EdgeCaseAggregate");
		aggregate.ETag.ShouldContain(aggregateId);
		aggregate.ETag.ShouldContain(":v1"); // Version 1 after first event
	}

	#endregion

	#region Event Deserialization Failure Tests

	[Fact]
	public async Task GetByIdAsync_ShouldSkipEventsWithJsonException_AndReturnAggregate()
	{
		// Arrange
		var aggregateId = "agg-json-fail";

		var storedEvent = new StoredEvent(
			EventId: Guid.NewGuid().ToString(),
			AggregateId: aggregateId,
			AggregateType: "EdgeCaseAggregate",
			EventType: "TestDomainEvent",
			EventData: new byte[] { 0x01, 0x02, 0x03 },
			Metadata: null,
			Version: 0,
			Timestamp: DateTimeOffset.UtcNow,
			IsDispatched: false);

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType("TestDomainEvent"))
			.Returns(typeof(TestDomainEvent));
		_ = A.CallTo(() => serializer.DeserializeEvent(storedEvent.EventData, typeof(TestDomainEvent)))
			.Throws(new JsonException("Invalid JSON"));

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id));

		// Act - Should not throw, just skip the invalid event
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		// When events exist but can't be deserialized, an empty aggregate is returned
		// because the event store indicates the aggregate exists
		_ = result.ShouldNotBeNull();
		result.Version.ShouldBe(0); // No events were successfully applied
		result.Data.ShouldBe(string.Empty); // Default state
	}

	[Fact]
	public async Task GetByIdAsync_ShouldSkipEventsWithTypeLoadException_AndReturnAggregate()
	{
		// Arrange
		var aggregateId = "agg-type-fail";

		var storedEvent = new StoredEvent(
			EventId: Guid.NewGuid().ToString(),
			AggregateId: aggregateId,
			AggregateType: "EdgeCaseAggregate",
			EventType: "UnknownEventType",
			EventData: new byte[] { 0x01, 0x02 },
			Metadata: null,
			Version: 0,
			Timestamp: DateTimeOffset.UtcNow,
			IsDispatched: false);

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType("UnknownEventType"))
			.Throws(new TypeLoadException("Type not found"));

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id));

		// Act - Should not throw, just skip the unknown event
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		// When events exist but can't be deserialized, an empty aggregate is returned
		_ = result.ShouldNotBeNull();
		result.Version.ShouldBe(0);
	}

	[Fact]
	public async Task GetByIdAsync_ShouldSkipEventsWithInvalidOperationException_AndReturnAggregate()
	{
		// Arrange
		var aggregateId = "agg-invalid-op-fail";

		var storedEvent = new StoredEvent(
			EventId: Guid.NewGuid().ToString(),
			AggregateId: aggregateId,
			AggregateType: "EdgeCaseAggregate",
			EventType: "TestDomainEvent",
			EventData: new byte[] { 0x01, 0x02, 0x03 },
			Metadata: null,
			Version: 0,
			Timestamp: DateTimeOffset.UtcNow,
			IsDispatched: false);

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType("TestDomainEvent"))
			.Returns(typeof(TestDomainEvent));
		_ = A.CallTo(() => serializer.DeserializeEvent(storedEvent.EventData, typeof(TestDomainEvent)))
			.Throws(new InvalidOperationException("Deserialization failed"));

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id));

		// Act - Should not throw, just skip the invalid event
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Version.ShouldBe(0);
	}

	#endregion

	#region Upcast Fallback Tests

	[Fact]
	public async Task GetByIdAsync_ShouldFallbackToOriginalEvent_WhenUpcastReturnsNonDomainEvent()
	{
		// Arrange
		var aggregateId = "agg-upcast-fallback";
		var originalEvent = new TestDomainEvent(aggregateId, 0, "original");

		var storedEvent = new StoredEvent(
			EventId: originalEvent.EventId,
			AggregateId: aggregateId,
			AggregateType: "EdgeCaseAggregate",
			EventType: "TestDomainEvent",
			EventData: JsonSerializer.SerializeToUtf8Bytes(originalEvent),
			Metadata: null,
			Version: 0,
			Timestamp: DateTimeOffset.UtcNow,
			IsDispatched: false);

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType("TestDomainEvent"))
			.Returns(typeof(TestDomainEvent));
		_ = A.CallTo(() => serializer.DeserializeEvent(storedEvent.EventData, typeof(TestDomainEvent)))
			.Returns(originalEvent);

		var upcastingPipeline = A.Fake<IUpcastingPipeline>();
		// Return something that's not an IDomainEvent
		_ = A.CallTo(() => upcastingPipeline.Upcast(A<IDispatchMessage>._))
			.Returns(A.Fake<IDispatchMessage>()); // Not an IDomainEvent

		var options = Microsoft.Extensions.Options.Options.Create(new UpcastingOptions { EnableAutoUpcastOnReplay = true });

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			upcastingPipeline,
			upcastingOptions: options);

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.ShouldBe("original"); // Original event should be applied
	}

	#endregion

	#region Generic Key Repository Tests

	[Fact]
	public void GenericKeyRepository_ShouldBeCreatable()
	{
		// Arrange
		var eventStore = A.Fake<IEventStore>();
		var serializer = CreateMockSerializer();

		// Act - Create a repository with Guid keys
		var repository = new EventSourcedRepository<GuidAggregate, Guid>(
			eventStore,
			serializer,
			id => new GuidAggregate(id));

		// Assert
		_ = repository.ShouldNotBeNull();
	}

	[Fact]
	public async Task GenericKeyRepository_ShouldGetByGuidId()
	{
		// Arrange
		var aggregateId = Guid.NewGuid();
		var stringId = aggregateId.ToString();

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(stringId, "GuidAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var serializer = CreateMockSerializer();

		var repository = new EventSourcedRepository<GuidAggregate, Guid>(
			eventStore,
			serializer,
			id => new GuidAggregate(id));

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
		A.CallTo(() => eventStore.LoadAsync(stringId, "GuidAggregate", A<long>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private sealed class GuidAggregate : AggregateRoot<Guid>
	{
		public GuidAggregate() : base(Guid.Empty) { }
		public GuidAggregate(Guid id) : base(id) { }

		protected override void ApplyEventInternal(IDomainEvent @event) { }
	}

	#endregion

	#region SaveAsync with ETag Tests

	[Fact]
	public async Task SaveAsyncWithETag_ShouldDelegateToSaveAsync()
	{
		// Arrange
		var aggregate = new EdgeCaseAggregate("agg-etag-delegate");
		aggregate.ETag = "expected-etag";
		aggregate.SetData("test");

		var eventStore = A.Fake<IEventStore>();
		var serializer = CreateMockSerializer();

		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id));

		// Act
		await repository.SaveAsync(aggregate, "expected-etag", CancellationToken.None);

		// Assert
		A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Helper Methods

	private static IEventSerializer CreateMockSerializer()
	{
		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType("TestDomainEvent"))
			.Returns(typeof(TestDomainEvent));
		_ = A.CallTo(() => serializer.ResolveType("NonVersionedEvent"))
			.Returns(typeof(NonVersionedEvent));
		return serializer;
	}

	#endregion
}
