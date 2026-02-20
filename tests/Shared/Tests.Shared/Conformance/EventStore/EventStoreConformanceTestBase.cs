// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.EventSourcing.Abstractions;

namespace Tests.Shared.Conformance.EventStore;

/// <summary>
/// Base class for IEventStore conformance tests.
/// Implementations must provide a concrete IEventStore instance for testing.
/// </summary>
/// <remarks>
/// <para>
/// This conformance test kit verifies that event store implementations
/// correctly implement the IEventStore interface contract, including:
/// </para>
/// <list type="bullet">
///   <item>Stream append operations with optimistic concurrency</item>
///   <item>Event loading (full stream and from specific version)</item>
///   <item>Undispatched event retrieval for outbox pattern</item>
///   <item>Event dispatch marking</item>
///   <item>Concurrent access handling</item>
/// </list>
/// <para>
/// To create conformance tests for your own IEventStore implementation:
/// <list type="number">
///   <item>Inherit from EventStoreConformanceTestBase</item>
///   <item>Override CreateStoreAsync() to create an instance of your IEventStore implementation</item>
///   <item>Override CleanupAsync() to properly clean up the store between tests</item>
/// </list>
/// </para>
/// </remarks>
public abstract class EventStoreConformanceTestBase : IAsyncLifetime
{
	/// <summary>
	/// The event store instance under test.
	/// </summary>
	protected IEventStore Store { get; private set; } = null!;

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		Store = await CreateStoreAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		await CleanupAsync().ConfigureAwait(false);

		if (Store is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if (Store is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Creates a new instance of the IEventStore implementation under test.
	/// </summary>
	/// <returns>A configured IEventStore instance.</returns>
	protected abstract Task<IEventStore> CreateStoreAsync();

	/// <summary>
	/// Cleans up the IEventStore instance after each test.
	/// </summary>
	protected abstract Task CleanupAsync();

	#region Helper Methods

	/// <summary>
	/// Test aggregate type for conformance tests.
	/// </summary>
	protected const string TestAggregateType = "TestAggregate";

	/// <summary>
	/// Creates a test domain event for testing purposes.
	/// </summary>
	protected static TestDomainEvent CreateTestEvent(
		string? aggregateId = null,
		string? eventId = null)
	{
		return new TestDomainEvent
		{
			EventId = eventId ?? Guid.NewGuid().ToString(),
			AggregateId = aggregateId ?? Guid.NewGuid().ToString(),
			OccurredAt = DateTimeOffset.UtcNow,
			Data = $"TestData-{Guid.NewGuid():N}"
		};
	}

	/// <summary>
	/// Creates multiple test events for the same aggregate.
	/// </summary>
	protected static List<TestDomainEvent> CreateTestEvents(string aggregateId, int count)
	{
		return [.. Enumerable.Range(0, count).Select(_ => CreateTestEvent(aggregateId))];
	}

	#endregion Helper Methods

	#region Interface Implementation Tests

	[Fact]
	public void Store_ShouldImplementIEventStore()
	{
		// Assert
		_ = Store.ShouldBeAssignableTo<IEventStore>();
	}

	#endregion Interface Implementation Tests

	#region AppendAsync - New Stream Tests

	[Fact]
	public async Task AppendAsync_NewStream_Succeeds()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 3);

		// Act
		var result = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue("Append to new stream should succeed");
		result.NextExpectedVersion.ShouldBe(2); // 0, 1, 2 = 3 events, next expected is 2 (0-based)
	}

	[Fact]
	public async Task AppendAsync_EmptyEvents_NoOp()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var emptyEvents = Array.Empty<IDomainEvent>();

		// Act
		var result = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			emptyEvents,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue("Append with empty events should succeed (no-op)");
	}

	[Fact]
	public async Task AppendAsync_SingleEvent_Succeeds()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[] { CreateTestEvent(aggregateId) };

		// Act
		var result = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue("Append single event should succeed");
		result.NextExpectedVersion.ShouldBe(0); // First event has version 0
	}

	#endregion AppendAsync - New Stream Tests

	#region AppendAsync - Existing Stream Tests

	[Fact]
	public async Task AppendAsync_ExistingStream_WithCorrectVersion_Succeeds()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var initialEvents = CreateTestEvents(aggregateId, 2);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			initialEvents,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var additionalEvents = CreateTestEvents(aggregateId, 2);

		// Act
		var result = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			additionalEvents,
			expectedVersion: 1, // Last version was 1 (0-indexed)
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue("Append with correct expected version should succeed");
		result.NextExpectedVersion.ShouldBe(3); // 4 total events: 0, 1, 2, 3
	}

	[Fact]
	public async Task AppendAsync_ExistingStream_WithWrongVersion_Fails()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var initialEvents = CreateTestEvents(aggregateId, 3);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			initialEvents,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var additionalEvents = CreateTestEvents(aggregateId, 1);

		// Act
		var result = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			additionalEvents,
			expectedVersion: 0, // Wrong version - actual is 2
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse("Append with wrong expected version should fail");
		result.IsConcurrencyConflict.ShouldBeTrue("Should indicate concurrency conflict");
	}

	[Fact]
	public async Task AppendAsync_NonExistentStream_WithWrongVersion_Fails()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 1);

		// Act - Try to append to non-existent stream expecting version 5
		var result = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: 5, // Wrong - stream doesn't exist
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse("Append to non-existent stream with wrong version should fail");
		result.IsConcurrencyConflict.ShouldBeTrue("Should indicate concurrency conflict");
	}

	#endregion AppendAsync - Existing Stream Tests

	#region LoadAsync - Full Stream Tests

	[Fact]
	public async Task LoadAsync_ExistingStream_ReturnsAllEvents()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 5);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var loadedEvents = await Store.LoadAsync(
			aggregateId,
			TestAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		loadedEvents.Count.ShouldBe(5);
	}

	[Fact]
	public async Task LoadAsync_NonExistentStream_ReturnsEmpty()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();

		// Act
		var loadedEvents = await Store.LoadAsync(
			aggregateId,
			TestAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		loadedEvents.ShouldBeEmpty();
	}

	[Fact]
	public async Task LoadAsync_ReturnsEventsInVersionOrder()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 5);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var loadedEvents = await Store.LoadAsync(
			aggregateId,
			TestAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		for (int i = 0; i < loadedEvents.Count - 1; i++)
		{
			loadedEvents[i].Version.ShouldBeLessThan(loadedEvents[i + 1].Version);
		}
	}

	[Fact]
	public async Task LoadAsync_PreservesEventData()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			new TestDomainEvent
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				OccurredAt = DateTimeOffset.UtcNow,
				Data = "UniqueTestData-12345"
			}
		};

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var loadedEvents = await Store.LoadAsync(
			aggregateId,
			TestAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		loadedEvents.Count.ShouldBe(1);
		loadedEvents[0].EventId.ShouldBe(events[0].EventId);
		loadedEvents[0].AggregateId.ShouldBe(aggregateId);
		loadedEvents[0].AggregateType.ShouldBe(TestAggregateType);
	}

	#endregion LoadAsync - Full Stream Tests

	#region LoadAsync - From Version Tests

	[Fact]
	public async Task LoadAsync_FromVersion_ReturnsEventsAfterVersion()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 10);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Load events after version 4 (exclusive)
		var loadedEvents = await Store.LoadAsync(
			aggregateId,
			TestAggregateType,
			fromVersion: 4,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Should return events 5, 6, 7, 8, 9 (5 events)
		loadedEvents.Count.ShouldBe(5);
		loadedEvents[0].Version.ShouldBeGreaterThan(4);
	}

	[Fact]
	public async Task LoadAsync_FromVersion_ZeroReturnsAllExceptFirst()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 5);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Load events after version 0 (exclusive)
		var loadedEvents = await Store.LoadAsync(
			aggregateId,
			TestAggregateType,
			fromVersion: 0,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Should return events 1, 2, 3, 4 (4 events, excluding version 0)
		loadedEvents.Count.ShouldBe(4);
		loadedEvents.All(e => e.Version > 0).ShouldBeTrue();
	}

	[Fact]
	public async Task LoadAsync_FromVersion_BeyondCurrentVersion_ReturnsEmpty()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 3);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Load events after version 100 (beyond current)
		var loadedEvents = await Store.LoadAsync(
			aggregateId,
			TestAggregateType,
			fromVersion: 100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		loadedEvents.ShouldBeEmpty();
	}

	#endregion LoadAsync - From Version Tests

	#region GetUndispatchedEventsAsync Tests

	[Fact]
	public async Task GetUndispatchedEventsAsync_NewEvents_ReturnsAllUndispatched()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 5);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var undispatched = await Store.GetUndispatchedEventsAsync(
			100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		undispatched.Count.ShouldBeGreaterThanOrEqualTo(5);
		undispatched.All(e => !e.IsDispatched).ShouldBeTrue();
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_RespectsBatchSize()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 10);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var undispatched = await Store.GetUndispatchedEventsAsync(
			3,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		undispatched.Count.ShouldBeLessThanOrEqualTo(3);
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_EmptyStore_ReturnsEmpty()
	{
		// Act
		var undispatched = await Store.GetUndispatchedEventsAsync(
			100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		undispatched.ShouldBeEmpty();
	}

	#endregion GetUndispatchedEventsAsync Tests

	#region MarkEventAsDispatchedAsync Tests

	[Fact]
	public async Task MarkEventAsDispatchedAsync_ValidEvent_UpdatesStatus()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 1);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var undispatched = await Store.GetUndispatchedEventsAsync(
			100,
			CancellationToken.None).ConfigureAwait(false);

		var eventToMark = undispatched.First(e => e.AggregateId == aggregateId);

		// Act
		await Store.MarkEventAsDispatchedAsync(
			eventToMark.EventId,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		var afterMark = await Store.GetUndispatchedEventsAsync(
			100,
			CancellationToken.None).ConfigureAwait(false);

		afterMark.ShouldNotContain(e => e.EventId == eventToMark.EventId);
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_AllEvents_ClearsUndispatched()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 3);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var undispatched = await Store.GetUndispatchedEventsAsync(
			100,
			CancellationToken.None).ConfigureAwait(false);

		var aggregateEvents = undispatched.Where(e => e.AggregateId == aggregateId).ToList();

		// Act - Mark all as dispatched
		foreach (var evt in aggregateEvents)
		{
			await Store.MarkEventAsDispatchedAsync(
				evt.EventId,
				CancellationToken.None).ConfigureAwait(false);
		}

		// Assert
		var afterMark = await Store.GetUndispatchedEventsAsync(
			100,
			CancellationToken.None).ConfigureAwait(false);

		afterMark.Where(e => e.AggregateId == aggregateId).ShouldBeEmpty();
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_DispatchedEventsStillLoadable()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateTestEvents(aggregateId, 3);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var undispatched = await Store.GetUndispatchedEventsAsync(
			100,
			CancellationToken.None).ConfigureAwait(false);

		var aggregateEvents = undispatched.Where(e => e.AggregateId == aggregateId).ToList();

		// Mark all as dispatched
		foreach (var evt in aggregateEvents)
		{
			await Store.MarkEventAsDispatchedAsync(
				evt.EventId,
				CancellationToken.None).ConfigureAwait(false);
		}

		// Act - Load should still return all events
		var loadedEvents = await Store.LoadAsync(
			aggregateId,
			TestAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		loadedEvents.Count.ShouldBe(3);
	}

	#endregion MarkEventAsDispatchedAsync Tests

	#region Concurrency Tests

	[Fact]
	public async Task ConcurrentAppend_SameVersion_OnlyOneSucceeds()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var initialEvents = CreateTestEvents(aggregateId, 1);

		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			initialEvents,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		const int concurrentAttempts = 10;
		var tasks = new List<Task<AppendResult>>();

		// Act - Try to append concurrently with same expected version
		for (int i = 0; i < concurrentAttempts; i++)
		{
			var evt = CreateTestEvent(aggregateId);
			tasks.Add(Task.Run(async () =>
				await Store.AppendAsync(
					aggregateId,
					TestAggregateType,
					new[] { evt },
					expectedVersion: 0, // All expect version 0
					CancellationToken.None).ConfigureAwait(false)));
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Only one should succeed
		var successCount = results.Count(r => r.Success);
		successCount.ShouldBe(1, "Only one concurrent append should succeed");
	}

	[Fact]
	public async Task ConcurrentAppend_DifferentAggregates_AllSucceed()
	{
		// Arrange
		const int concurrentAttempts = 10;
		var tasks = new List<Task<AppendResult>>();

		// Act - Append to different aggregates concurrently
		for (int i = 0; i < concurrentAttempts; i++)
		{
			var aggregateId = Guid.NewGuid().ToString();
			var evt = CreateTestEvent(aggregateId);
			tasks.Add(Task.Run(async () =>
				await Store.AppendAsync(
					aggregateId,
					TestAggregateType,
					new[] { evt },
					expectedVersion: -1,
					CancellationToken.None).ConfigureAwait(false)));
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - All should succeed
		results.All(r => r.Success).ShouldBeTrue("All concurrent appends to different aggregates should succeed");
	}

	#endregion Concurrency Tests

	#region Aggregate Type Tests

	[Fact]
	public async Task AppendAndLoad_DifferentAggregateTypes_AreIsolated()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var eventsTypeA = CreateTestEvents(aggregateId, 3);
		var eventsTypeB = CreateTestEvents(aggregateId, 2);

		_ = await Store.AppendAsync(
			aggregateId,
			"TypeA",
			eventsTypeA,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		_ = await Store.AppendAsync(
			aggregateId,
			"TypeB",
			eventsTypeB,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var loadedTypeA = await Store.LoadAsync(aggregateId, "TypeA", CancellationToken.None)
			.ConfigureAwait(false);
		var loadedTypeB = await Store.LoadAsync(aggregateId, "TypeB", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loadedTypeA.Count.ShouldBe(3);
		loadedTypeB.Count.ShouldBe(2);
	}

	#endregion Aggregate Type Tests
}

/// <summary>
/// Test domain event for conformance testing.
/// </summary>
public class TestDomainEvent : IDomainEvent
{
	/// <inheritdoc/>
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <inheritdoc/>
	public string AggregateId { get; set; } = string.Empty;

	/// <inheritdoc/>
	public long Version { get; set; }

	/// <inheritdoc/>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	/// <inheritdoc/>
	public string EventType => nameof(TestDomainEvent);

	/// <inheritdoc/>
	public IDictionary<string, object>? Metadata { get; set; }

	/// <summary>
	/// Gets or sets test data for the event.
	/// </summary>
	public string Data { get; set; } = string.Empty;
}
