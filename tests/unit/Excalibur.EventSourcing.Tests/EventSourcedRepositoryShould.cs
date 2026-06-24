// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1506 // Excessive class coupling -- test methods create repository with many DI parameters by design

using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Versioning;

using Excalibur.Data;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;

using FakeItEasy;

using Shouldly;

using Xunit;

// Use Excalibur.EventSourcing as canonical namespace (AD-251-2)
using IEventStore = Excalibur.EventSourcing.IEventStore;
using IEventNotificationBroker = Excalibur.EventSourcing.IEventNotificationBroker;
using EventNotificationContext = Excalibur.EventSourcing.EventNotificationContext;
using StoredEvent = Excalibur.EventSourcing.StoredEvent;
using AppendResult = Excalibur.EventSourcing.AppendResult;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Unit tests for <see cref="EventSourcedRepository{TAggregate}"/> with automatic upcasting support.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventSourcedRepositoryShould
{
	#region Test Domain Events

	/// <summary>
	/// V1 event implementing both IDomainEvent and IVersionedMessage.
	/// </summary>
	internal sealed record TestCreatedEventV1 : DomainEvent, IVersionedMessage
	{
		public string Name { get; init; } = string.Empty;


		int IVersionedMessage.Version => 1;
		string IVersionedMessage.MessageType => "TestCreatedEvent";
	}

	/// <summary>
	/// V2 event with split name fields.
	/// </summary>
	internal sealed record TestCreatedEventV2 : DomainEvent, IVersionedMessage
	{
		public string FirstName { get; init; } = string.Empty;
		public string LastName { get; init; } = string.Empty;


		int IVersionedMessage.Version => 2;
		string IVersionedMessage.MessageType => "TestCreatedEvent";
	}

	/// <summary>
	/// Non-versioned event for testing pass-through behavior.
	/// </summary>
	internal sealed record TestUpdatedEvent : DomainEvent
	{
		public string NewValue { get; init; } = string.Empty;

	}

	#endregion Test Domain Events

	#region Test Aggregate

	internal sealed class TestAggregate : AggregateRoot
	{
		public TestAggregate()
		{ }

		public TestAggregate(string id) : base(id)
		{
		}

		public string FirstName { get; private set; } = string.Empty;
		public string LastName { get; private set; } = string.Empty;
		public string CurrentValue { get; private set; } = string.Empty;
		public bool WasUpcastedEventApplied { get; private set; }

		public void Create(string name)
		{
			RaiseEvent(new TestCreatedEventV1 { AggregateId = Id, Version = Version, Name = name });
		}

		public void Update(string newValue)
		{
			RaiseEvent(new TestUpdatedEvent { AggregateId = Id, Version = Version, NewValue = newValue });
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			switch (@event)
			{
				case TestCreatedEventV1 v1:
					var parts = v1.Name.Split(' ', 2);
					FirstName = parts[0];
					LastName = parts.Length > 1 ? parts[1] : string.Empty;
					break;

				case TestCreatedEventV2 v2:
					FirstName = v2.FirstName;
					LastName = v2.LastName;
					WasUpcastedEventApplied = true;
					break;

				case TestUpdatedEvent updated:
					CurrentValue = updated.NewValue;
					break;
			}
		}
	}

	#endregion Test Aggregate

	#region Helper Methods

	private static StoredEvent CreateStoredEvent(IDomainEvent domainEvent, string eventType)
	{
		return new StoredEvent(
			EventId: domainEvent.EventId,
			AggregateId: domainEvent.AggregateId,
			AggregateType: "TestAggregate",
			EventType: eventType,
			EventData: JsonSerializer.SerializeToUtf8Bytes(domainEvent),
			Metadata: null,
			Version: domainEvent.Version,
			Timestamp: domainEvent.OccurredAt);
	}

	private static IEventSerializer CreateMockSerializer(params IDomainEvent[] events)
	{
		var serializer = A.Fake<IEventSerializer>();
		var eventLookup = new Dictionary<string, IDomainEvent>();

		foreach (var evt in events)
		{
			eventLookup[evt.EventId] = evt;
		}

		_ = A.CallTo(() => serializer.ResolveType("TestCreatedEventV1"))
			.Returns(typeof(TestCreatedEventV1));
		_ = A.CallTo(() => serializer.ResolveType("TestCreatedEventV2"))
			.Returns(typeof(TestCreatedEventV2));
		_ = A.CallTo(() => serializer.ResolveType("TestUpdatedEvent"))
			.Returns(typeof(TestUpdatedEvent));

		_ = A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, A<Type>._))
			.ReturnsLazily((FakeItEasy.Core.IFakeObjectCall call) =>
			{
				var data = call.GetArgument<byte[]>(0);
				if (data is null || data.Length == 0)
					throw new InvalidOperationException("No event data to deserialize");

				using var doc = JsonDocument.Parse(data);
				if (doc.RootElement.TryGetProperty("EventId", out var eventIdProp))
				{
					var eventId = eventIdProp.GetString();
					if (eventId != null && eventLookup.TryGetValue(eventId, out var evt))
					{
						return evt;
					}
				}
				throw new InvalidOperationException("Event not found in test lookup");
			});

		return serializer;
	}

	#endregion Helper Methods

	#region GetByIdAsync Tests

	[Fact]
	public async Task ReturnNullWhenAggregateDoesNotExist()
	{
		// Arrange
		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync("non-existent", "TestAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act
		var result = await repository.GetByIdAsync("non-existent", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task LoadAndApplyEventsWithoutUpcasting()
	{
		// Arrange
		var aggregateId = "agg-1";
		var v1Event = new TestCreatedEventV1 { AggregateId = aggregateId, Version = 0, Name = "John Doe" };
		var updateEvent = new TestUpdatedEvent { AggregateId = aggregateId, Version = 1, NewValue = "Updated Value" };

		var storedEvents = new List<StoredEvent>
		{
			CreateStoredEvent(v1Event, "TestCreatedEventV1"),
			CreateStoredEvent(updateEvent, "TestUpdatedEvent")
		};

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "TestAggregate", A<long>._, A<CancellationToken>._))
			.Returns(storedEvents);

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(v1Event, updateEvent),
			id => new TestAggregate(id));

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.FirstName.ShouldBe("John");
		result.LastName.ShouldBe("Doe");
		result.CurrentValue.ShouldBe("Updated Value");
		result.Version.ShouldBe(2L);
		result.WasUpcastedEventApplied.ShouldBeFalse("V1 event should have been applied directly without upcasting");
	}

	// --- bd-ze6pty (S840, AC-10b, ADR-336 Amendment 3): FR-8 — rehydration FAILS on a poison event ---
	// Independent regression lock (author≠impl, TestsDeveloper). Rehydration reconstructs the
	// SOURCE-OF-TRUTH aggregate, so a silently-skipped undeserializable/null event would build a corrupt
	// aggregate and (worse) let it be persisted — strictly more severe than the c3jdco projection case.
	// The repository MUST fail loud (throw) — no skip, no quarantine. RED on the pre-fix skip-and-continue
	// (returns a corrupt aggregate); GREEN on the fail-loud fix.

	[Fact]
	public async Task FailRehydrationWhenAStreamEventDeserializesToNull()
	{
		// Arrange — a stream with a good event followed by a poison event that deserializes to null.
		var aggregateId = "agg-poison";
		var goodEvent = new TestCreatedEventV1 { AggregateId = aggregateId, Version = 0, Name = "John Doe" };
		var goodStored = CreateStoredEvent(goodEvent, "TestCreatedEventV1");
		var poisonStored = new StoredEvent(
			EventId: "evt-poison",
			AggregateId: aggregateId,
			AggregateType: "TestAggregate",
			EventType: "TestCreatedEventV1",
			EventData: "POISON"u8.ToArray(),
			Metadata: null,
			Version: 1,
			Timestamp: DateTimeOffset.UtcNow);

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "TestAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { goodStored, poisonStored });

		// Serializer: the good event deserializes; the poison event deserializes to NULL (the bug-trigger).
		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType("TestCreatedEventV1")).Returns(typeof(TestCreatedEventV1));
		_ = A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, A<Type>._))
			.ReturnsLazily((FakeItEasy.Core.IFakeObjectCall call) =>
			{
				var data = call.GetArgument<byte[]>(0);
				return data.AsSpan().SequenceEqual("POISON"u8) ? (IDomainEvent?)null : goodEvent;
			});

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			serializer,
			id => new TestAggregate(id));

		// Act & Assert — rehydration must FAIL LOUD, never return a silently-truncated aggregate.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => repository.GetByIdAsync(aggregateId, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
// CA1506 suppressed at file level
	public async Task UpcastVersionedEventsWhenEnabled()
// CA1506 suppressed at file level
	{
		// Arrange
		var aggregateId = "agg-1";
		var v1Event = new TestCreatedEventV1 { AggregateId = aggregateId, Version = 0, Name = "Jane Smith" };

		var storedEvents = new List<StoredEvent>
		{
			CreateStoredEvent(v1Event, "TestCreatedEventV1")
		};

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "TestAggregate", A<long>._, A<CancellationToken>._))
			.Returns(storedEvents);

		var pipeline = A.Fake<IUpcastingPipeline>();
		_ = A.CallTo(() => pipeline.Upcast(A<IDispatchMessage>._))
			.ReturnsLazily((IDispatchMessage msg) =>
			{
				if (msg is TestCreatedEventV1 v1)
				{
					var parts = v1.Name.Split(' ', 2);
					return new TestCreatedEventV2 { AggregateId = v1.AggregateId, Version = v1.Version, FirstName = parts[0], LastName = parts.Length > 1 ? parts[1] : "" };
				}
				return msg;
			});

		var options = Microsoft.Extensions.Options.Options.Create(new UpcastingOptions { EnableAutoUpcastOnReplay = true });

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(v1Event),
			id => new TestAggregate(id),
			upcastingOptions: options,
			upcastingPipeline: pipeline);

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.FirstName.ShouldBe("Jane");
		result.LastName.ShouldBe("Smith");
		result.WasUpcastedEventApplied.ShouldBeTrue("V2 event from upcasting should have been applied");
	}

	[Fact]
	public async Task NotUpcastWhenDisabled()
	{
		// Arrange
		var aggregateId = "agg-1";
		var v1Event = new TestCreatedEventV1 { AggregateId = aggregateId, Version = 0, Name = "John Doe" };

		var storedEvents = new List<StoredEvent>
		{
			CreateStoredEvent(v1Event, "TestCreatedEventV1")
		};

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "TestAggregate", A<long>._, A<CancellationToken>._))
			.Returns(storedEvents);

		var pipeline = A.Fake<IUpcastingPipeline>();
		var options = Microsoft.Extensions.Options.Options.Create(new UpcastingOptions { EnableAutoUpcastOnReplay = false });

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(v1Event),
			id => new TestAggregate(id),
			upcastingOptions: options,
			upcastingPipeline: pipeline);

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.WasUpcastedEventApplied.ShouldBeFalse("V1 should be applied directly when upcasting is disabled");
		A.CallTo(() => pipeline.Upcast(A<IDispatchMessage>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotUpcastNonVersionedEvents()
	{
		// Arrange
		var aggregateId = "agg-1";
		var updateEvent = new TestUpdatedEvent { AggregateId = aggregateId, Version = 0, NewValue = "Some Value" };

		var storedEvents = new List<StoredEvent>
		{
			CreateStoredEvent(updateEvent, "TestUpdatedEvent")
		};

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "TestAggregate", A<long>._, A<CancellationToken>._))
			.Returns(storedEvents);

		var pipeline = A.Fake<IUpcastingPipeline>();
		var options = Microsoft.Extensions.Options.Options.Create(new UpcastingOptions { EnableAutoUpcastOnReplay = true });

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(updateEvent),
			id => new TestAggregate(id),
			upcastingOptions: options,
			upcastingPipeline: pipeline);

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.CurrentValue.ShouldBe("Some Value");
		A.CallTo(() => pipeline.Upcast(A<IDispatchMessage>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotUpcastWhenNoPipelineConfigured()
	{
		// Arrange
		var aggregateId = "agg-1";
		var v1Event = new TestCreatedEventV1 { AggregateId = aggregateId, Version = 0, Name = "John Doe" };

		var storedEvents = new List<StoredEvent>
		{
			CreateStoredEvent(v1Event, "TestCreatedEventV1")
		};

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "TestAggregate", A<long>._, A<CancellationToken>._))
			.Returns(storedEvents);

		var options = Microsoft.Extensions.Options.Options.Create(new UpcastingOptions { EnableAutoUpcastOnReplay = true });

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(v1Event),
			id => new TestAggregate(id),
			upcastingPipeline: null,
			snapshotManager: null,
			snapshotStrategy: null,
			upcastingOptions: options);

		// Act
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.WasUpcastedEventApplied.ShouldBeFalse("V1 should be applied directly when no pipeline is available");
	}

	#endregion GetByIdAsync Tests

	#region ExistsAsync Tests

	[Fact]
	public async Task ReturnTrueWhenAggregateExists()
	{
		// Arrange
		var aggregateId = "agg-1";
		var eventStore = A.Fake<IEventStore>();
		var dummyEvent = new StoredEvent(
			EventId: Guid.NewGuid().ToString(),
			AggregateId: aggregateId,
			AggregateType: "TestAggregate",
			EventType: "TestEvent",
			EventData: [],
			Metadata: null,
			Version: 0,
			Timestamp: DateTimeOffset.UtcNow);
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "TestAggregate", A<CancellationToken>._))
			.Returns(new List<StoredEvent> { dummyEvent });

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act
		var exists = await repository.ExistsAsync(aggregateId, CancellationToken.None);

		// Assert
		exists.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFalseWhenAggregateDoesNotExist()
	{
		// Arrange
		var aggregateId = "non-existent";
		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "TestAggregate", A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act
		var exists = await repository.ExistsAsync(aggregateId, CancellationToken.None);

		// Assert
		exists.ShouldBeFalse();
	}

	#endregion ExistsAsync Tests

	#region SaveAsync Tests

	[Fact]
	public async Task SaveUncommittedEventsToEventStore()
	{
		// Arrange
		var aggregateId = "agg-1";
		var aggregate = new TestAggregate(aggregateId);
		aggregate.Create("Test User");

		aggregate.GetUncommittedEvents().Count.ShouldBe(1, "Event should be in uncommitted list after Create");

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._,
				A<string>._,
				A<IEnumerable<IDomainEvent>>._,
				A<long>._,
				A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act
		await repository.SaveAsync(aggregate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			A<IEnumerable<IDomainEvent>>._,
			A<long>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowConcurrencyExceptionOnConcurrencyConflict()
	{
		// Arrange
		var aggregateId = "agg-1";
		var aggregate = new TestAggregate(aggregateId);
		aggregate.Create("Test User");

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._,
				A<string>._,
				A<IEnumerable<IDomainEvent>>._,
				A<long>._,
				A<CancellationToken>._))
			.Returns(AppendResult.CreateConcurrencyConflict(0, 1));

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		var exception = await Should.ThrowAsync<ConcurrencyException>(
			async () => await repository.SaveAsync(aggregate, CancellationToken.None));
		exception.Message.ShouldContain("Concurrency conflict");
	}

	[Fact]
	public async Task ThrowResourceExceptionOnSaveFailure()
	{
		// Arrange
		var aggregateId = "agg-1";
		var aggregate = new TestAggregate(aggregateId);
		aggregate.Create("Test User");

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._,
				A<string>._,
				A<IEnumerable<IDomainEvent>>._,
				A<long>._,
				A<CancellationToken>._))
			.Returns(AppendResult.CreateFailure("Append failed"));

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		var exception = await Should.ThrowAsync<Excalibur.Data.ResourceException>(
			async () => await repository.SaveAsync(aggregate, CancellationToken.None));
		exception.Message.ShouldContain("Append failed");
	}

	[Fact]
	public async Task NotSaveWhenNoUncommittedEvents()
	{
		// Arrange
		var aggregate = new TestAggregate("agg-1");

		var eventStore = A.Fake<IEventStore>();

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act
		await repository.SaveAsync(aggregate, CancellationToken.None);

		// Assert
		A.CallTo(() => eventStore.AppendAsync(
			A<string>._,
			A<string>._,
			A<IEnumerable<IDomainEvent>>._,
			A<long>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	#endregion SaveAsync Tests

	#region ETag Validation Tests

	[Fact]
	public async Task SaveAsync_WithMatchingETag_ShouldSucceed()
	{
		// Arrange
		var aggregate = new TestAggregate("agg-1");
		aggregate.ETag = "expected-etag";
		aggregate.Create("Test");

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		await Should.NotThrowAsync(async () =>
			await repository.SaveAsync(aggregate, "expected-etag", CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_WithMismatchedETag_ShouldThrowConcurrencyException()
	{
		// Arrange
		var aggregate = new TestAggregate("agg-1");
		aggregate.ETag = "actual-etag";
		aggregate.Create("Test");

		var eventStore = A.Fake<IEventStore>();

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		var exception = await Should.ThrowAsync<ConcurrencyException>(async () =>
			await repository.SaveAsync(aggregate, "wrong-etag", CancellationToken.None));

		exception.Message.ShouldContain("Concurrency conflict");
		exception.ExpectedVersionString.ShouldBe("wrong-etag");
		exception.ActualVersionString.ShouldBe("actual-etag");
	}

	[Fact]
	public async Task SaveAsync_WithNullExpectedETag_ShouldSkipValidation()
	{
		// Arrange
		var aggregate = new TestAggregate("agg-1");
		aggregate.ETag = "any-etag";
		aggregate.Create("Test");

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		await Should.NotThrowAsync(async () =>
			await repository.SaveAsync(aggregate, null, CancellationToken.None));
	}

	#endregion ETag Validation Tests

	#region DeleteAsync Tests

	[Fact]
	public async Task DeleteAsync_ShouldThrowNotSupported()
	{
		// Arrange
		var aggregate = new TestAggregate("agg-1");
		var eventStore = A.Fake<IEventStore>();

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		var exception = await Should.ThrowAsync<NotSupportedException>(async () =>
			await repository.DeleteAsync(aggregate, CancellationToken.None));

		exception.Message.ShouldContain("tombstone event");
	}

	[Fact]
	public async Task DeleteAsync_WithNullAggregate_ShouldThrowArgumentNull()
	{
		// Arrange
		var eventStore = A.Fake<IEventStore>();

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await repository.DeleteAsync(null!, CancellationToken.None));
	}

	#endregion DeleteAsync Tests

	#region CQRS Write-Side Only Tests

	[Fact]
	public void ShouldNotExposeQueryAsyncOnConcreteClass()
	{
		// QueryAsync was removed in C.1 (CQRS write-side only)
		var method = typeof(EventSourcedRepository<TestAggregate>).GetMethod("QueryAsync");
		method.ShouldBeNull("QueryAsync was removed — CQRS write-side only");
	}

	[Fact]
	public void ShouldNotExposeFindAsyncOnConcreteClass()
	{
		// FindAsync was removed in C.1 (CQRS write-side only)
		var method = typeof(EventSourcedRepository<TestAggregate>).GetMethod("FindAsync");
		method.ShouldBeNull("FindAsync was removed — CQRS write-side only");
	}

	#endregion CQRS Write-Side Only Tests

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullEventStore()
	{
		_ = Should.Throw<ArgumentNullException>(() => new EventSourcedRepository<TestAggregate>(
			null!,
			CreateMockSerializer(),
			id => new TestAggregate(id)));
	}

	[Fact]
	public void ThrowOnNullEventSerializer()
	{
		var eventStore = A.Fake<IEventStore>();

		_ = Should.Throw<ArgumentNullException>(() => new EventSourcedRepository<TestAggregate>(
			eventStore,
			null!,
			id => new TestAggregate(id)));
	}

	[Fact]
	public void ThrowOnNullAggregateFactory()
	{
		var eventStore = A.Fake<IEventStore>();

		_ = Should.Throw<ArgumentNullException>(() => new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			null!));
	}

	#endregion Constructor Validation Tests

	#region Null Argument Validation Tests

	[Fact]
	public async Task GetByIdAsync_WithNullId_ShouldThrowArgumentNull()
	{
		// Arrange
		var eventStore = A.Fake<IEventStore>();

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await repository.GetByIdAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_WithNullAggregate_ShouldThrowArgumentNull()
	{
		// Arrange
		var eventStore = A.Fake<IEventStore>();

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await repository.SaveAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExistsAsync_WithNullId_ShouldThrowArgumentNull()
	{
		// Arrange
		var eventStore = A.Fake<IEventStore>();

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await repository.ExistsAsync(null!, CancellationToken.None));
	}

	#endregion Null Argument Validation Tests

	#region Interface Implementation Tests

	[Fact]
	public void Repository_ShouldImplementInterface()
	{
		// Arrange
		var eventStore = A.Fake<IEventStore>();

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Assert
		_ = repository.ShouldBeAssignableTo<IEventSourcedRepository<TestAggregate>>();
		_ = repository.ShouldBeAssignableTo<IEventSourcedRepository<TestAggregate, string>>();
	}

	#endregion Interface Implementation Tests

	#region Event Notification Snapshot Tests

	[Fact]
	public async Task SaveAsync_ShouldNotifyBrokerWithEventsAfterMarkEventsAsCommitted()
	{
		// Arrange -- This test verifies the fix where uncommitted events are snapshotted
		// via .ToList() BEFORE MarkEventsAsCommitted() clears the aggregate's internal list.
		// Without the snapshot, the broker would receive an empty list.
		var aggregateId = "agg-notify-1";
		var aggregate = new TestAggregate(aggregateId);
		aggregate.Create("Notify Test");

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._,
				A<string>._,
				A<IEnumerable<IDomainEvent>>._,
				A<long>._,
				A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var broker = A.Fake<IEventNotificationBroker>();
		IReadOnlyList<IDomainEvent>? capturedEvents = null;
		_ = A.CallTo(() => broker.NotifyAsync(
				A<IReadOnlyList<IDomainEvent>>._,
				A<EventNotificationContext>._,
				A<CancellationToken>._))
			.Invokes((IReadOnlyList<IDomainEvent> events, EventNotificationContext _, CancellationToken _) =>
			{
				capturedEvents = events;
			})
			.Returns(Task.CompletedTask);

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id),
			eventNotificationBroker: broker);

		// Act
		await repository.SaveAsync(aggregate, CancellationToken.None);

		// Assert -- broker was called with a non-empty event list
		_ = A.CallTo(() => broker.NotifyAsync(
			A<IReadOnlyList<IDomainEvent>>._,
			A<EventNotificationContext>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();

		capturedEvents.ShouldNotBeNull();
		capturedEvents.Count.ShouldBe(1);
		capturedEvents[0].ShouldBeOfType<TestCreatedEventV1>();

		// Also verify that MarkEventsAsCommitted has already run
		aggregate.GetUncommittedEvents().Count.ShouldBe(0);
	}

	[Fact]
	public async Task SaveAsync_ShouldNotifyBrokerWithMultipleEvents()
	{
		// Arrange -- verify snapshot captures all events, not just the first
		var aggregateId = "agg-notify-multi";
		var aggregate = new TestAggregate(aggregateId);
		aggregate.Create("Multi Test");
		aggregate.Update("Updated Value");

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._,
				A<string>._,
				A<IEnumerable<IDomainEvent>>._,
				A<long>._,
				A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(2, 0));

		var broker = A.Fake<IEventNotificationBroker>();
		IReadOnlyList<IDomainEvent>? capturedEvents = null;
		_ = A.CallTo(() => broker.NotifyAsync(
				A<IReadOnlyList<IDomainEvent>>._,
				A<EventNotificationContext>._,
				A<CancellationToken>._))
			.Invokes((IReadOnlyList<IDomainEvent> events, EventNotificationContext _, CancellationToken _) =>
			{
				capturedEvents = events;
			})
			.Returns(Task.CompletedTask);

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id),
			eventNotificationBroker: broker);

		// Act
		await repository.SaveAsync(aggregate, CancellationToken.None);

		// Assert -- broker received both events
		capturedEvents.ShouldNotBeNull();
		capturedEvents.Count.ShouldBe(2);
		capturedEvents[0].ShouldBeOfType<TestCreatedEventV1>();
		capturedEvents[1].ShouldBeOfType<TestUpdatedEvent>();
	}

	[Fact]
	public async Task SaveAsync_ShouldNotNotifyBrokerWhenAppendFails()
	{
		// Arrange -- verify broker is never called if AppendAsync fails
		var aggregateId = "agg-notify-fail";
		var aggregate = new TestAggregate(aggregateId);
		aggregate.Create("Fail Test");

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._,
				A<string>._,
				A<IEnumerable<IDomainEvent>>._,
				A<long>._,
				A<CancellationToken>._))
			.Returns(AppendResult.CreateConcurrencyConflict(0, 1));

		var broker = A.Fake<IEventNotificationBroker>();

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id),
			eventNotificationBroker: broker);

		// Act & Assert -- save should throw due to concurrency conflict
		await Should.ThrowAsync<ConcurrencyException>(
			() => repository.SaveAsync(aggregate, CancellationToken.None));

		// Broker should never have been called
		A.CallTo(() => broker.NotifyAsync(
			A<IReadOnlyList<IDomainEvent>>._,
			A<EventNotificationContext>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task SaveAsync_ShouldPropagateExceptionWhenBrokerThrows()
	{
		// Arrange -- verify broker exceptions propagate to caller
		var aggregateId = "agg-notify-throw";
		var aggregate = new TestAggregate(aggregateId);
		aggregate.Create("Throw Test");

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._,
				A<string>._,
				A<IEnumerable<IDomainEvent>>._,
				A<long>._,
				A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var broker = A.Fake<IEventNotificationBroker>();
		_ = A.CallTo(() => broker.NotifyAsync(
				A<IReadOnlyList<IDomainEvent>>._,
				A<EventNotificationContext>._,
				A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Broker failed"));

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id),
			eventNotificationBroker: broker);

		// Act & Assert -- broker failure should propagate
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => repository.SaveAsync(aggregate, CancellationToken.None));
		ex.Message.ShouldBe("Broker failed");
	}

	#endregion Event Notification Snapshot Tests
}
