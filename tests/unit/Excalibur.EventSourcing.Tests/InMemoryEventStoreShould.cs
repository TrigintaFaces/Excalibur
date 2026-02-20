// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.InMemory;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Unit tests for <see cref="InMemoryEventStore"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryEventStoreShould
{
	#region Test Events

	internal sealed record TestCreatedEvent : DomainEvent
	{
		public string Name { get; init; } = string.Empty;

		public TestCreatedEvent(string aggregateId, long version, string name)
			: base(aggregateId, version, TimeProvider.System)
		{
			Name = name;
		}

		public TestCreatedEvent() : base("", 0, TimeProvider.System) { }
	}

	internal sealed record TestUpdatedEvent : DomainEvent
	{
		public string Value { get; init; } = string.Empty;

		public TestUpdatedEvent(string aggregateId, long version, string value)
			: base(aggregateId, version, TimeProvider.System)
		{
			Value = value;
		}

		public TestUpdatedEvent() : base("", 0, TimeProvider.System) { }
	}

	#endregion Test Events

	#region AppendAsync Tests

	[Fact]
	public async Task AppendAsync_ShouldStoreEventsSuccessfully()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var aggregateId = "agg-1";
		var aggregateType = "TestAggregate";
		var events = new List<IDomainEvent>
		{
			new TestCreatedEvent(aggregateId, 0, "Test")
		};

		// Act
		var result = await store.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(0); // After appending 1 event starting at -1, version becomes 0
		store.GetEventCount().ShouldBe(1);
	}

	[Fact]
	public async Task AppendAsync_ShouldReturnConcurrencyConflict_WhenVersionMismatch()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var aggregateId = "agg-1";
		var aggregateType = "TestAggregate";

		// First append succeeds
		var firstEvents = new List<IDomainEvent> { new TestCreatedEvent(aggregateId, 0, "Test") };
		_ = await store.AppendAsync(aggregateId, aggregateType, firstEvents, -1, CancellationToken.None);

		// Second append with wrong expected version
		var secondEvents = new List<IDomainEvent> { new TestUpdatedEvent(aggregateId, 1, "Updated") };

		// Act - Expecting version -1 but current is 0
		var result = await store.AppendAsync(aggregateId, aggregateType, secondEvents, -1, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.NextExpectedVersion.ShouldBe(0); // Current version is 0 (after first append)
	}

	[Fact]
	public async Task AppendAsync_ShouldSucceed_WithCorrectExpectedVersion()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var aggregateId = "agg-1";
		var aggregateType = "TestAggregate";

		// First append
		var firstEvents = new List<IDomainEvent> { new TestCreatedEvent(aggregateId, 0, "Test") };
		_ = await store.AppendAsync(aggregateId, aggregateType, firstEvents, -1, CancellationToken.None);

		// Second append with correct expected version
		var secondEvents = new List<IDomainEvent> { new TestUpdatedEvent(aggregateId, 1, "Updated") };

		// Act
		var result = await store.AppendAsync(aggregateId, aggregateType, secondEvents, 0, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(1); // After appending 2nd event, version becomes 1
		store.GetEventCount().ShouldBe(2);
	}

	[Fact]
	public async Task AppendAsync_WithEmptyEventList_ShouldReturnSuccessWithoutStoring()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var events = new List<IDomainEvent>();

		// Act
		var result = await store.AppendAsync("agg-1", "TestAggregate", events, -1, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		store.GetEventCount().ShouldBe(0);
	}

	[Fact]
	public async Task AppendAsync_ShouldIncrementVersionForMultipleEvents()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var aggregateId = "agg-1";
		var aggregateType = "TestAggregate";
		var events = new List<IDomainEvent>
		{
			new TestCreatedEvent(aggregateId, 0, "Event1"),
			new TestUpdatedEvent(aggregateId, 1, "Event2"),
			new TestUpdatedEvent(aggregateId, 2, "Event3")
		};

		// Act
		var result = await store.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(2); // After appending 3 events (version 0, 1, 2), version becomes 2
		store.GetEventCount().ShouldBe(3);
	}

	#endregion AppendAsync Tests

	#region LoadAsync Tests

	[Fact]
	public async Task LoadAsync_ShouldReturnEmptyList_WhenAggregateDoesNotExist()
	{
		// Arrange
		var store = new InMemoryEventStore();

		// Act
		var events = await store.LoadAsync("non-existent", "TestAggregate", CancellationToken.None);

		// Assert
		events.ShouldBeEmpty();
	}

	[Fact]
	public async Task LoadAsync_ShouldReturnAllEvents_ForExistingAggregate()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var aggregateId = "agg-1";
		var aggregateType = "TestAggregate";
		var events = new List<IDomainEvent>
		{
			new TestCreatedEvent(aggregateId, 0, "Created"),
			new TestUpdatedEvent(aggregateId, 1, "Updated")
		};
		_ = await store.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None);

		// Act
		var loadedEvents = await store.LoadAsync(aggregateId, aggregateType, CancellationToken.None);

		// Assert
		loadedEvents.Count.ShouldBe(2);
		loadedEvents[0].Version.ShouldBe(0);
		loadedEvents[1].Version.ShouldBe(1);
	}

	[Fact]
	public async Task LoadAsync_WithFromVersion_ShouldReturnEventsAfterVersion()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var aggregateId = "agg-1";
		var aggregateType = "TestAggregate";
		var events = new List<IDomainEvent>
		{
			new TestCreatedEvent(aggregateId, 0, "Event0"),
			new TestUpdatedEvent(aggregateId, 1, "Event1"),
			new TestUpdatedEvent(aggregateId, 2, "Event2"),
			new TestUpdatedEvent(aggregateId, 3, "Event3")
		};
		_ = await store.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None);

		// Act - Get events after version 1
		var loadedEvents = await store.LoadAsync(aggregateId, aggregateType, 1, CancellationToken.None);

		// Assert
		loadedEvents.Count.ShouldBe(2); // Versions 2 and 3
		loadedEvents[0].Version.ShouldBe(2);
		loadedEvents[1].Version.ShouldBe(3);
	}

	[Fact]
	public async Task LoadAsync_ShouldReturnEventsOrderedByVersion()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var aggregateId = "agg-1";
		var aggregateType = "TestAggregate";
		var events = new List<IDomainEvent>
		{
			new TestCreatedEvent(aggregateId, 0, "First"),
			new TestUpdatedEvent(aggregateId, 1, "Second"),
			new TestUpdatedEvent(aggregateId, 2, "Third")
		};
		_ = await store.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None);

		// Act
		var loadedEvents = await store.LoadAsync(aggregateId, aggregateType, CancellationToken.None);

		// Assert
		for (var i = 0; i < loadedEvents.Count; i++)
		{
			loadedEvents[i].Version.ShouldBe(i);
		}
	}

	#endregion LoadAsync Tests

	#region Undispatched Events Tests

	[Fact]
	public async Task GetUndispatchedEventsAsync_ShouldReturnAllEventsInitially()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var events = new List<IDomainEvent>
		{
			new TestCreatedEvent("agg-1", 0, "Event1"),
			new TestUpdatedEvent("agg-1", 1, "Event2")
		};
		_ = await store.AppendAsync("agg-1", "TestAggregate", events, -1, CancellationToken.None);

		// Act
		var undispatched = await store.GetUndispatchedEventsAsync(10, CancellationToken.None);

		// Assert
		undispatched.Count.ShouldBe(2);
		store.GetUndispatchedEventCount().ShouldBe(2);
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_ShouldRemoveFromUndispatched()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var event1 = new TestCreatedEvent("agg-1", 0, "Event1");
		_ = await store.AppendAsync("agg-1", "TestAggregate", new List<IDomainEvent> { event1 }, -1, CancellationToken.None);

		var undispatched = await store.GetUndispatchedEventsAsync(10, CancellationToken.None);
		var eventId = undispatched[0].EventId;

		// Act
		await store.MarkEventAsDispatchedAsync(eventId, CancellationToken.None);

		// Assert
		store.GetUndispatchedEventCount().ShouldBe(0);
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_ShouldRespectBatchSize()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var events = new List<IDomainEvent>
		{
			new TestCreatedEvent("agg-1", 0, "Event1"),
			new TestUpdatedEvent("agg-1", 1, "Event2"),
			new TestUpdatedEvent("agg-1", 2, "Event3"),
			new TestUpdatedEvent("agg-1", 3, "Event4"),
			new TestUpdatedEvent("agg-1", 4, "Event5")
		};
		_ = await store.AppendAsync("agg-1", "TestAggregate", events, -1, CancellationToken.None);

		// Act
		var undispatched = await store.GetUndispatchedEventsAsync(3, CancellationToken.None);

		// Assert
		undispatched.Count.ShouldBe(3);
	}

	#endregion Undispatched Events Tests

	#region Clear Tests

	[Fact]
	public async Task Clear_ShouldRemoveAllEvents()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var events = new List<IDomainEvent>
		{
			new TestCreatedEvent("agg-1", 0, "Event1"),
			new TestUpdatedEvent("agg-1", 1, "Event2")
		};
		_ = await store.AppendAsync("agg-1", "TestAggregate", events, -1, CancellationToken.None);
		store.GetEventCount().ShouldBe(2);

		// Act
		store.Clear();

		// Assert
		store.GetEventCount().ShouldBe(0);
		var loadedEvents = await store.LoadAsync("agg-1", "TestAggregate", CancellationToken.None);
		loadedEvents.ShouldBeEmpty();
	}

	#endregion Clear Tests

	#region Thread Safety Tests

	[Fact]
	public async Task ShouldHandleConcurrentAppends()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var aggregateType = "TestAggregate";
		var tasks = new List<Task<AppendResult>>();

		// Act - Append to different aggregates concurrently
		for (var i = 0; i < 100; i++)
		{
			var aggregateId = $"agg-{i}";
			var events = new List<IDomainEvent>
			{
				new TestCreatedEvent(aggregateId, 0, $"Event{i}")
			};
			// Convert ValueTask to Task for concurrent execution
			tasks.Add(store.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None).AsTask());
		}

		_ = await Task.WhenAll(tasks);

		// Assert
		store.GetEventCount().ShouldBe(100);
		tasks.All(t => t.Result.Success).ShouldBeTrue();
	}

	[Fact]
	public async Task ShouldHandleConcurrentLoads()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var aggregateId = "agg-1";
		var aggregateType = "TestAggregate";
		var events = new List<IDomainEvent>
		{
			new TestCreatedEvent(aggregateId, 0, "Event1"),
			new TestUpdatedEvent(aggregateId, 1, "Event2")
		};
		_ = await store.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None);

		// Act - Load concurrently (convert ValueTask to Task for concurrent execution)
		var tasks = Enumerable.Range(0, 100)
			.Select(_ => store.LoadAsync(aggregateId, aggregateType, CancellationToken.None).AsTask())
			.ToList();

		var results = await Task.WhenAll(tasks);

		// Assert
		results.All(r => r.Count == 2).ShouldBeTrue();
	}

	#endregion Thread Safety Tests

	#region IEventStore Interface Tests

	[Fact]
	public void ShouldImplementIEventStore()
	{
		// Arrange & Act
		var store = new InMemoryEventStore();

		// Assert
		_ = store.ShouldBeAssignableTo<IEventStore>();
	}

	#endregion IEventStore Interface Tests
}
