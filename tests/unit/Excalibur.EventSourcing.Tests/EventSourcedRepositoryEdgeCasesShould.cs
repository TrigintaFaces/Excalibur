// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Justification: Test methods constructing EventSourcedRepository with full DI parameters inherently exceed coupling threshold
#pragma warning disable CA1506

using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Versioning;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Implementation;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

using IEventStore = Excalibur.EventSourcing.IEventStore;
using ISnapshotManager = Excalibur.EventSourcing.ISnapshotManager;
using ISnapshotStrategy = Excalibur.EventSourcing.ISnapshotStrategy;
using StoredEvent = Excalibur.EventSourcing.StoredEvent;
using AppendResult = Excalibur.EventSourcing.AppendResult;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Edge case tests for <see cref="EventSourcedRepository{TAggregate}"/> to improve coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventSourcedRepositoryEdgeCasesShould
{
	#region Test Types

	[MessageName("Test.EventSourcedRepositoryEdgeCases.TestDomainEvent")]
	private sealed record TestDomainEvent : DomainEvent, IVersionedMessage
	{
		public string Data { get; init; } = string.Empty;

		int IVersionedMessage.Version => 1;
		string IVersionedMessage.MessageType => "TestDomainEvent";
	}

	[MessageName("Test.NonVersionedEvent")]
	private sealed record NonVersionedEvent : DomainEvent
	{
		public string Value { get; init; } = string.Empty;

	}

	private sealed class EdgeCaseAggregate : AggregateRoot
	{
		public EdgeCaseAggregate() { }
		public EdgeCaseAggregate(string id) : base(id) { }

		public string Data { get; private set; } = string.Empty;
		public string Value { get; private set; } = string.Empty;

		public void SetData(string data)
		{
			RaiseEvent(new TestDomainEvent { Data = data });
		}

		public void SetValue(string value)
		{
			RaiseEvent(new NonVersionedEvent { Value = value });
		}

		protected override bool ApplyEventInternal(IDomainEvent @event)
		{
			switch (@event)
			{
				case TestDomainEvent testEvent:
					Data = testEvent.Data;
					return true;
				case NonVersionedEvent nonVersioned:
					Value = nonVersioned.Value;
					return true;
				default:
					return false;
			}
		}

		// e6y51s: the base ApplySnapshot now throws NotSupportedException unless overridden (fail-closed
		// against silent state loss). The snapshot-restore paths exercised below (GetByIdAsync with a
		// snapshot from the snapshot manager) call LoadFromSnapshot -> ApplySnapshot, so this aggregate
		// must rehydrate its own state from the snapshot rather than rely on the silent base no-op.
		protected override void ApplySnapshot(ISnapshot snapshot)
		{
			if (snapshot.Data.IsEmpty)
			{
				return;
			}

			var state = JsonSerializer.Deserialize<SnapshotState>(snapshot.Data.Span);
			if (state is not null)
			{
				Data = state.Data ?? Data;
				Value = state.Value ?? Value;
			}
		}

		private sealed record SnapshotState
		{
			public string? Data { get; init; }
			public string? Value { get; init; }
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
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()),
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
				Data = Array.Empty<byte>(),
				CreatedAt = DateTime.UtcNow,
			});

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()),
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
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()),
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
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()));

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
	public async Task GetByIdAsync_FailsLoud_WhenEventDeserializationThrowsJsonException()
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
			Timestamp: DateTimeOffset.UtcNow);

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
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()));

		// Act & Assert — bd-ze6pty / ADR-336 fail-only: rehydration must FAIL LOUD on a deserialization
		// failure. Silently skipping the event would replay an incomplete history into a corrupt,
		// partially-applied source-of-truth aggregate (the data-loss behavior this lock prevents).
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => repository.GetByIdAsync(aggregateId, CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_FailsLoud_WhenEventTypeCannotBeResolved()
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
			Timestamp: DateTimeOffset.UtcNow);

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType("UnknownEventType"))
			.Throws(new TypeLoadException("Type not found"));

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()));

		// Act & Assert — bd-ze6pty / ADR-336 fail-only: an unresolvable event type is a deserialization
		// failure; rehydration must FAIL LOUD rather than skip the event and return a corrupt aggregate.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => repository.GetByIdAsync(aggregateId, CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_FailsLoud_WhenEventDeserializationThrowsInvalidOperation()
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
			Timestamp: DateTimeOffset.UtcNow);

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
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()));

		// Act & Assert — bd-ze6pty / ADR-336 fail-only: rehydration must FAIL LOUD on a deserialization
		// failure rather than skip the event and return a corrupt, partially-applied aggregate.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => repository.GetByIdAsync(aggregateId, CancellationToken.None));
	}

	#endregion

	#region Upcast Fallback Tests

	[Fact]
	public async Task GetByIdAsync_ShouldFallbackToOriginalEvent_WhenUpcastReturnsNonDomainEvent()
	{
		// Arrange
		var aggregateId = "agg-upcast-fallback";
		var originalEvent = new TestDomainEvent { Data = "original" };

		var storedEvent = new StoredEvent(
			EventId: originalEvent.EventId,
			AggregateId: aggregateId,
			AggregateType: "EdgeCaseAggregate",
			EventType: "TestDomainEvent",
			EventData: JsonSerializer.SerializeToUtf8Bytes(originalEvent),
			Metadata: null,
			Version: 0,
			Timestamp: DateTimeOffset.UtcNow);

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

		var repositoryOptions = Microsoft.Extensions.Options.Options.Create(
			new EventSourcedRepositoryOptions { EnableAutoUpcast = true });

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			repositoryOptions,
			upcastingPipeline: upcastingPipeline);

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
			id => new GuidAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()));

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
			id => new GuidAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()));

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

		protected override bool ApplyEventInternal(IDomainEvent @event) => false; // totality: recognizes no events => unhandled.
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
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()));

		// Act
		await repository.SaveAsync(aggregate, "expected-etag", CancellationToken.None);

		// Assert
		A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Version Gap Detection Tests (T.9)

	[Fact]
	public async Task GetByIdAsync_FailsLoud_WhenVersionGapExistsBetweenSnapshotAndEvents()
	{
		// Arrange - T.9: a version hole between the snapshot and the first available event means the
		// intervening versions never legitimately existed. Replaying onto the snapshot would produce a
		// corrupt aggregate, so the repository MUST fail loud (InvalidOperationException) rather than
		// silently return a version-hole state.
		var aggregateId = "agg-gap-1";
		var eventStore = A.Fake<IEventStore>();
		var snapshotManager = A.Fake<ISnapshotManager>();
		var serializer = CreateMockSerializer();

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

		// Return events starting at version 10, creating a gap (versions 6-9 missing)
		var gapEvents = new List<StoredEvent>
		{
			new(
				EventId: Guid.NewGuid().ToString(),
				AggregateId: aggregateId,
				AggregateType: "EdgeCaseAggregate",
				EventType: "TestDomainEvent",
				EventData: JsonSerializer.SerializeToUtf8Bytes(new TestDomainEvent { Data = "gap-event" }),
				Metadata: null,
				Version: 10,
				Timestamp: DateTimeOffset.UtcNow),
		};
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(gapEvents);
		_ = A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, typeof(TestDomainEvent)))
			.Returns(new TestDomainEvent { Data = "gap-event" });

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()),
			snapshotManager: snapshotManager,
			logger: NullLogger<EventSourcedRepository<EdgeCaseAggregate, string>>.Instance);

		// Act & Assert - a version hole (snapshot v5, first event v10, versions 5-9 missing) must be
		// refused rather than rehydrated into a corrupt aggregate.
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => repository.GetByIdAsync(aggregateId, CancellationToken.None));
		ex.Message.ShouldContain("version hole");
	}

	/// <summary>
	/// The empty-tail hole: snapshot at version 50, but the stream stops at version 40. Zero rows come
	/// back, so the first-event guard has nothing to compare and cannot see this; without the version
	/// probe the repository rehydrates from the snapshot alone and hands back an aggregate at a version
	/// its own stream never reached. The exception must name the aggregate and BOTH versions, because
	/// the operator's next question is which aggregate and how far behind.
	/// </summary>
	[Fact]
	public async Task GetByIdAsync_FailsLoud_WhenSnapshotIsAheadOfATruncatedStream()
	{
		// Arrange
		var aggregateId = "agg-empty-tail-hole";
		var snapshotManager = A.Fake<ISnapshotManager>();
		var serializer = CreateMockSerializer();
		var eventStore = A.Fake<IEventStore>(x => x.Implements<IEventStoreVersionProbe>());
		_ = A.CallTo(() => eventStore.GetService(typeof(IEventStoreVersionProbe))).Returns(eventStore);
		_ = A.CallTo(() => ((IEventStoreVersionProbe)eventStore)
				.GetMaxVersionAsync(aggregateId, "EdgeCaseAggregate", A<CancellationToken>._))
			.Returns(new ValueTask<long>(40L));

		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			AggregateType = "EdgeCaseAggregate",
			Version = 50,
			Data = JsonSerializer.SerializeToUtf8Bytes(new { Data = "snapshot-data" }),
			CreatedAt = DateTime.UtcNow,
		};

		_ = A.CallTo(() => snapshotManager.GetLatestSnapshotAsync(aggregateId, A<CancellationToken>._))
			.Returns(snapshot);

		// The EMPTY TAIL: no events at or above the snapshot's version.
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions { VerifyStreamReachesSnapshot = true }),
			snapshotManager: snapshotManager,
			logger: NullLogger<EventSourcedRepository<EdgeCaseAggregate, string>>.Instance);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => repository.GetByIdAsync(aggregateId, CancellationToken.None));

		ex.Message.ShouldContain("version hole");
		ex.Message.ShouldContain(aggregateId, customMessage: "the operator must be told WHICH aggregate.");
		ex.Message.ShouldContain("50", customMessage: "the snapshot version must be named.");
		ex.Message.ShouldContain("40", customMessage: "the highest stored version must be named.");
	}

	/// <summary>
	/// Liveness, and the arm that keeps the probe from being a blunt instrument: an empty tail is the
	/// NORMAL case when the snapshot sits at the head of the stream. Snapshot at version 50 over events
	/// through version 49 returns zero rows exactly as the truncated case does, and must load cleanly.
	/// Without this arm the safety check above would pass just as well if it refused every snapshot.
	/// </summary>
	[Fact]
	public async Task GetByIdAsync_LoadsNormally_WhenSnapshotSitsAtTheHeadOfTheStream()
	{
		// Arrange
		var aggregateId = "agg-snapshot-at-head";
		var snapshotManager = A.Fake<ISnapshotManager>();
		var serializer = CreateMockSerializer();
		var eventStore = A.Fake<IEventStore>(x => x.Implements<IEventStoreVersionProbe>());
		_ = A.CallTo(() => eventStore.GetService(typeof(IEventStoreVersionProbe))).Returns(eventStore);
		_ = A.CallTo(() => ((IEventStoreVersionProbe)eventStore)
				.GetMaxVersionAsync(aggregateId, "EdgeCaseAggregate", A<CancellationToken>._))
			.Returns(new ValueTask<long>(49L));

		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			AggregateType = "EdgeCaseAggregate",
			Version = 50,
			Data = JsonSerializer.SerializeToUtf8Bytes(new { Data = "snapshot-data" }),
			CreatedAt = DateTime.UtcNow,
		};

		_ = A.CallTo(() => snapshotManager.GetLatestSnapshotAsync(aggregateId, A<CancellationToken>._))
			.Returns(snapshot);

		// The EMPTY TAIL: no events at or above the snapshot's version.
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions { VerifyStreamReachesSnapshot = true }),
			snapshotManager: snapshotManager,
			logger: NullLogger<EventSourcedRepository<EdgeCaseAggregate, string>>.Instance);

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull("a snapshot at the head of its stream is the common case and must load.");
	}

	/// <summary>
	/// Liveness for the opt-in contract: a store that does not provide the probe is never asked, and
	/// behaves exactly as it did before the probe existed -- no third state, no forced provider work.
	/// The same truncated stream that throws above must load here, because nothing can detect it.
	/// </summary>
	[Fact]
	public async Task GetByIdAsync_LoadsUnchanged_WhenTheStoreProvidesNoVersionProbe()
	{
		// Arrange -- a plain store: GetService returns null for the probe.
		var aggregateId = "agg-no-probe";
		var eventStore = A.Fake<IEventStore>();
		var snapshotManager = A.Fake<ISnapshotManager>();
		var serializer = CreateMockSerializer();

		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			AggregateType = "EdgeCaseAggregate",
			Version = 50,
			Data = JsonSerializer.SerializeToUtf8Bytes(new { Data = "snapshot-data" }),
			CreatedAt = DateTime.UtcNow,
		};

		_ = A.CallTo(() => snapshotManager.GetLatestSnapshotAsync(aggregateId, A<CancellationToken>._))
			.Returns(snapshot);

		// The EMPTY TAIL: no events at or above the snapshot's version.
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions { VerifyStreamReachesSnapshot = true }),
			snapshotManager: snapshotManager,
			logger: NullLogger<EventSourcedRepository<EdgeCaseAggregate, string>>.Instance);

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull(
			"a store without the capability must be unaffected by this change.");
	}

	/// <summary>
	/// The DEFAULT is free, and this is the arm that proves it. Same truncated stream as the failing case
	/// above, same store, probe available -- but with verification left at its default of off, the load
	/// behaves exactly as it did before this check existed, and the probe is never consulted. A reader
	/// asking "does this cost me anything if I do not turn it on" is answered here.
	/// </summary>
	[Fact]
	public async Task GetByIdAsync_DoesNotVerifyOrProbe_WhenVerificationIsLeftOff()
	{
		// Arrange -- identical to the failing case, except verification stays at its default (off).
		var aggregateId = "agg-verification-off";
		var snapshotManager = A.Fake<ISnapshotManager>();
		var serializer = CreateMockSerializer();
		var eventStore = A.Fake<IEventStore>(x => x.Implements<IEventStoreVersionProbe>());
		_ = A.CallTo(() => eventStore.GetService(typeof(IEventStoreVersionProbe))).Returns(eventStore);
		_ = A.CallTo(() => ((IEventStoreVersionProbe)eventStore)
				.GetMaxVersionAsync(aggregateId, "EdgeCaseAggregate", A<CancellationToken>._))
			.Returns(new ValueTask<long>(40L));

		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			AggregateType = "EdgeCaseAggregate",
			Version = 50,
			Data = JsonSerializer.SerializeToUtf8Bytes(new { Data = "snapshot-data" }),
			CreatedAt = DateTime.UtcNow,
		};

		_ = A.CallTo(() => snapshotManager.GetLatestSnapshotAsync(aggregateId, A<CancellationToken>._))
			.Returns(snapshot);
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()),
			snapshotManager: snapshotManager,
			logger: NullLogger<EventSourcedRepository<EdgeCaseAggregate, string>>.Instance);

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert -- unchanged behaviour, and the probe was never called.
		_ = result.ShouldNotBeNull("with verification off the load must behave exactly as it did before.");
		A.CallTo(() => ((IEventStoreVersionProbe)eventStore)
				.GetMaxVersionAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task GetByIdAsync_ShouldSucceed_WhenNoVersionGapAfterSnapshot()
	{
		// Arrange - Contiguous events after snapshot (no gap)
		var aggregateId = "agg-no-gap-1";
		var eventStore = A.Fake<IEventStore>();
		var snapshotManager = A.Fake<ISnapshotManager>();
		var serializer = CreateMockSerializer();

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

		// Return events starting at version 5 -- no gap
		var contiguousEvents = new List<StoredEvent>
		{
			new(
				EventId: Guid.NewGuid().ToString(),
				AggregateId: aggregateId,
				AggregateType: "EdgeCaseAggregate",
				EventType: "TestDomainEvent",
				EventData: JsonSerializer.SerializeToUtf8Bytes(new TestDomainEvent { Data = "contiguous" }),
				Metadata: null,
				Version: 5,
				Timestamp: DateTimeOffset.UtcNow),
		};
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "EdgeCaseAggregate", A<long>._, A<CancellationToken>._))
			.Returns(contiguousEvents);
		_ = A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, typeof(TestDomainEvent)))
			.Returns(new TestDomainEvent { Data = "contiguous" });

		var repository = new EventSourcedRepository<EdgeCaseAggregate>(
			eventStore,
			serializer,
			id => new EdgeCaseAggregate(id),
			Microsoft.Extensions.Options.Options.Create(new EventSourcedRepositoryOptions()),
			snapshotManager: snapshotManager,
			logger: NullLogger<EventSourcedRepository<EdgeCaseAggregate, string>>.Instance);

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert - Aggregate loaded normally
		_ = result.ShouldNotBeNull();
		result.Data.ShouldBe("contiguous");
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
