// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Versioning;

using Excalibur.Data.Abstractions;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;

using FakeItEasy;

using Shouldly;

using Xunit;

// Use Excalibur.EventSourcing.Abstractions as canonical source (AD-251-2)
using IEventStore = Excalibur.EventSourcing.Abstractions.IEventStore;
using StoredEvent = Excalibur.EventSourcing.Abstractions.StoredEvent;
using AppendResult = Excalibur.EventSourcing.Abstractions.AppendResult;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Unit tests for <see cref="EventSourcedRepository{TAggregate}"/> with automatic upcasting support.
/// </summary>
[Trait("Category", "Unit")]
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
		public new string MessageType => "TestCreatedEvent";

		public TestCreatedEventV1(string aggregateId, long version, string name, TimeProvider? timeProvider = null)
			: base(aggregateId, version, timeProvider ?? TimeProvider.System)
		{
			Name = name;
		}

		public TestCreatedEventV1() : base("", 0, TimeProvider.System) { }
	}

	/// <summary>
	/// V2 event with split name fields.
	/// </summary>
	internal sealed record TestCreatedEventV2 : DomainEvent, IVersionedMessage
	{
		public string FirstName { get; init; } = string.Empty;
		public string LastName { get; init; } = string.Empty;

		int IVersionedMessage.Version => 2;
		public new string MessageType => "TestCreatedEvent";

		public TestCreatedEventV2(string aggregateId, long version, string firstName, string lastName, TimeProvider? timeProvider = null)
			: base(aggregateId, version, timeProvider ?? TimeProvider.System)
		{
			FirstName = firstName;
			LastName = lastName;
		}

		public TestCreatedEventV2() : base("", 0, TimeProvider.System) { }
	}

	/// <summary>
	/// Non-versioned event for testing pass-through behavior.
	/// </summary>
	internal sealed record TestUpdatedEvent : DomainEvent
	{
		public string NewValue { get; init; } = string.Empty;

		public TestUpdatedEvent(string aggregateId, long version, string newValue, TimeProvider? timeProvider = null)
			: base(aggregateId, version, timeProvider ?? TimeProvider.System)
		{
			NewValue = newValue;
		}

		public TestUpdatedEvent() : base("", 0, TimeProvider.System) { }
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
			RaiseEvent(new TestCreatedEventV1(Id, Version, name));
		}

		public void Update(string newValue)
		{
			RaiseEvent(new TestUpdatedEvent(Id, Version, newValue));
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
			Timestamp: domainEvent.OccurredAt,
			IsDispatched: false);
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
		var v1Event = new TestCreatedEventV1(aggregateId, 0, "John Doe");
		var updateEvent = new TestUpdatedEvent(aggregateId, 1, "Updated Value");

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

	[Fact]
	public async Task UpcastVersionedEventsWhenEnabled()
	{
		// Arrange
		var aggregateId = "agg-1";
		var v1Event = new TestCreatedEventV1(aggregateId, 0, "Jane Smith");

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
					return new TestCreatedEventV2(v1.AggregateId, v1.Version, parts[0], parts.Length > 1 ? parts[1] : "");
				}
				return msg;
			});

		var options = Microsoft.Extensions.Options.Options.Create(new UpcastingOptions { EnableAutoUpcastOnReplay = true });

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(v1Event),
			id => new TestAggregate(id),
			pipeline,
			null,
			null,
			options);

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
		var v1Event = new TestCreatedEventV1(aggregateId, 0, "John Doe");

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
			pipeline,
			null,
			null,
			options);

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
		var updateEvent = new TestUpdatedEvent(aggregateId, 0, "Some Value");

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
			pipeline,
			null,
			null,
			options);

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
		var v1Event = new TestCreatedEventV1(aggregateId, 0, "John Doe");

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
			Timestamp: DateTimeOffset.UtcNow,
			IsDispatched: false);
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
		var exception = await Should.ThrowAsync<Excalibur.Dispatch.Exceptions.ResourceException>(
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

	#region QueryAsync and FindAsync Tests

	internal sealed record TestQuery : IAggregateQuery<TestAggregate>;

	[Fact]
	public async Task QueryAsync_ShouldThrowNotSupported()
	{
		// Arrange
		var eventStore = A.Fake<IEventStore>();

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		var exception = await Should.ThrowAsync<NotSupportedException>(async () =>
			await repository.QueryAsync(new TestQuery(), CancellationToken.None));

		exception.Message.ShouldContain("query executor");
	}

	[Fact]
	public async Task FindAsync_ShouldThrowNotSupported()
	{
		// Arrange
		var eventStore = A.Fake<IEventStore>();

		var repository = new EventSourcedRepository<TestAggregate>(
			eventStore,
			CreateMockSerializer(),
			id => new TestAggregate(id));

		// Act & Assert
		var exception = await Should.ThrowAsync<NotSupportedException>(async () =>
			await repository.FindAsync(new TestQuery(), CancellationToken.None));

		exception.Message.ShouldContain("query executor");
	}

	#endregion QueryAsync and FindAsync Tests

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
}
