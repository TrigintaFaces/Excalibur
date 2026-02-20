// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.EventSourcing.InMemory;
using Excalibur.EventSourcing.Observability;

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Integration tests for InMemoryEventStore OpenTelemetry instrumentation.
/// Validates that ActivitySource spans are correctly created with proper tags
/// when executing actual in-memory event store operations.
/// </summary>
/// <remarks>
/// Note: InMemory is the simplest provider - no TestContainers needed.
/// Concurrency conflicts are detected via return value (AppendResult.IsConcurrencyConflict),
/// NOT via exceptions - this is unique among all providers.
/// </remarks>
[Collection("EventStore Telemetry Tests")]
public sealed class InMemoryEventStoreTelemetryShould : IDisposable
{
	private readonly InMemoryEventStore _eventStore;
	private readonly ActivityListener _activityListener;
	private readonly List<Activity> _recordedActivities = [];
	private readonly DateTimeOffset _creationTime = DateTimeOffset.UtcNow;
	private readonly Lock _activitiesLock = new();

	public InMemoryEventStoreTelemetryShould()
	{
		_eventStore = new InMemoryEventStore();

		// Set up activity listener for capturing spans
		_activityListener = new ActivityListener
		{
			ShouldListenTo = source =>
				source.Name == EventSourcingActivitySource.Name ||
				source.Name.StartsWith("Excalibur.", StringComparison.Ordinal) ||
				source.Name.StartsWith("Test.", StringComparison.Ordinal),

			Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
				ActivitySamplingResult.AllDataAndRecorded,

			ActivityStopped = activity =>
			{
				if (activity != null && activity.StartTimeUtc >= _creationTime.UtcDateTime)
				{
					lock (_activitiesLock)
					{
						_recordedActivities.Add(activity);
					}
				}
			},
		};

		ActivitySource.AddActivityListener(_activityListener);
	}

	public void Dispose()
	{
		_activityListener.Dispose();
		_eventStore.Clear();
		ClearRecordedActivities();
	}

	private IReadOnlyList<Activity> GetRecordedActivities()
	{
		lock (_activitiesLock)
		{
			return _recordedActivities.ToList().AsReadOnly();
		}
	}

	private void ClearRecordedActivities()
	{
		lock (_activitiesLock)
		{
			_recordedActivities.Clear();
		}
	}

	#region Event Store Span Creation Tests

	[Fact]
	public async Task CreateActivitySpanForAppendOperation()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent> { CreateTestEvent() };

		// Act
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.AggregateId).ShouldBe(aggregateId);
		appendActivity.GetTagItem(EventSourcingTags.AggregateType).ShouldBe("TestAggregate");
		appendActivity.GetTagItem(EventSourcingTags.EventCount).ShouldBe(1);
		appendActivity.GetTagItem(EventSourcingTags.ExpectedVersion).ShouldBe(-1L);
	}

	[Fact]
	public async Task CreateActivitySpanForLoadOperation()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();

		// First append an event so we can load it
		var events = new List<IDomainEvent> { CreateTestEvent() };
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		ClearRecordedActivities();

		// Act
		_ = await _eventStore.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Load);

		_ = loadActivity.ShouldNotBeNull();
		loadActivity.GetTagItem(EventSourcingTags.AggregateId).ShouldBe(aggregateId);
		loadActivity.GetTagItem(EventSourcingTags.AggregateType).ShouldBe("TestAggregate");
	}

	[Fact]
	public async Task CreateActivitySpanForLoadWithFromVersion()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();

		// Append multiple events
		var events = Enumerable.Range(0, 3).Select(_ => CreateTestEvent()).ToList();
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		ClearRecordedActivities();

		// Act - Load from version 1
		_ = await _eventStore.LoadAsync(aggregateId, "TestAggregate", 1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Load);

		_ = loadActivity.ShouldNotBeNull();
		loadActivity.GetTagItem(EventSourcingTags.FromVersion).ShouldBe(1L);
	}

	[Fact]
	public async Task CreateActivitySpanForGetUndispatchedEvents()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();

		// Append an event (will be undispatched by default)
		var events = new List<IDomainEvent> { CreateTestEvent() };
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		ClearRecordedActivities();

		// Act
		_ = await _eventStore.GetUndispatchedEventsAsync(100, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var getUndispatchedActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.GetUndispatched);

		_ = getUndispatchedActivity.ShouldNotBeNull();
		getUndispatchedActivity.GetTagItem(EventSourcingTags.BatchSize).ShouldBe(100);
	}

	[Fact]
	public async Task CreateActivitySpanForMarkEventAsDispatched()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();
		var testEvent = CreateTestEvent();

		// Append an event
		var events = new List<IDomainEvent> { testEvent };
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		ClearRecordedActivities();

		// Act
		await _eventStore.MarkEventAsDispatchedAsync(testEvent.EventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var markDispatchedActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.MarkDispatched);

		_ = markDispatchedActivity.ShouldNotBeNull();
		markDispatchedActivity.GetTagItem(EventSourcingTags.EventId).ShouldBe(testEvent.EventId);
	}

	#endregion Event Store Span Creation Tests

	#region Tag Verification Tests

	[Fact]
	public async Task SetEventCountTagOnSuccessfulAppend()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();
		var events = Enumerable.Range(0, 5).Select(_ => CreateTestEvent()).ToList();

		// Act
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.EventCount).ShouldBe(5);
	}

	[Fact]
	public async Task SetVersionTagOnSuccessfulAppend()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent> { CreateTestEvent() };

		// Act
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.Version).ShouldBe(0L);
	}

	[Fact]
	public async Task SetEventCountTagOnSuccessfulLoad()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();

		// Append 3 events
		var events = Enumerable.Range(0, 3).Select(_ => CreateTestEvent()).ToList();
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		ClearRecordedActivities();

		// Act
		_ = await _eventStore.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Load);

		_ = loadActivity.ShouldNotBeNull();
		loadActivity.GetTagItem(EventSourcingTags.EventCount).ShouldBe(3);
	}

	#endregion Tag Verification Tests

	#region Operation Result Tests

	[Fact]
	public async Task SetSuccessResultOnSuccessfulAppend()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent> { CreateTestEvent() };

		// Act
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task SetSuccessResultOnSuccessfulLoad()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent> { CreateTestEvent() };
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		ClearRecordedActivities();

		// Act
		_ = await _eventStore.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Load);

		_ = loadActivity.ShouldNotBeNull();
		loadActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task SetConcurrencyConflictResultOnVersionMismatch()
	{
		// Arrange
		// InMemory is UNIQUE - it uses return value for concurrency detection, NOT exceptions
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();

		// First, append an event at version 0
		var firstEvents = new List<IDomainEvent> { CreateTestEvent() };
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", firstEvents, -1, CancellationToken.None)
			.ConfigureAwait(false);

		ClearRecordedActivities();

		// Act - Try to append with wrong expected version (should return concurrency conflict)
		var conflictEvents = new List<IDomainEvent> { CreateTestEvent() };
		var result = await _eventStore.AppendAsync(aggregateId, "TestAggregate", conflictEvents, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - InMemory returns conflict via AppendResult, NOT via exception
		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();

		var activities = GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.ConcurrencyConflict);
	}

	[Fact]
	public async Task SetConcurrencyConflictOnInMemoryVersionMismatch()
	{
		// Arrange
		// This explicitly tests the InMemory-specific pattern:
		// - Other providers throw exceptions for concurrency conflicts
		// - InMemory returns AppendResult.CreateConcurrencyConflict() instead
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();

		// First append sets version 0
		var firstEvents = new List<IDomainEvent> { CreateTestEvent() };
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", firstEvents, -1, CancellationToken.None)
			.ConfigureAwait(false);

		ClearRecordedActivities();

		// Now we try to append again expecting version -1 (new aggregate)
		// This will cause version mismatch (current is 0, expected is -1)
		// The store should detect this and return concurrency conflict via return value
		var secondEvents = new List<IDomainEvent> { CreateTestEvent() };
		var result = await _eventStore.AppendAsync(aggregateId, "TestAggregate", secondEvents, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();

		var activities = GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.ConcurrencyConflict);
	}

	[Fact]
	public async Task SetSuccessResultOnGetUndispatched()
	{
		// Arrange
		ClearRecordedActivities();

		// Act
		_ = await _eventStore.GetUndispatchedEventsAsync(100, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var getUndispatchedActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.GetUndispatched);

		_ = getUndispatchedActivity.ShouldNotBeNull();
		getUndispatchedActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task SetSuccessResultOnMarkDispatched()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var testEvent = CreateTestEvent();

		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", new List<IDomainEvent> { testEvent }, -1, CancellationToken.None)
			.ConfigureAwait(false);

		ClearRecordedActivities();

		// Act
		await _eventStore.MarkEventAsDispatchedAsync(testEvent.EventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var markDispatchedActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.MarkDispatched);

		_ = markDispatchedActivity.ShouldNotBeNull();
		markDispatchedActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	#endregion Operation Result Tests

	#region Multiple Operations Sequence Tests

	[Fact]
	public async Task RecordMultipleOperationsInSequence()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();
		var testEvent = CreateTestEvent();

		// Act - Perform sequence: Append -> Load -> GetUndispatched -> MarkDispatched
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", new List<IDomainEvent> { testEvent }, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_ = await _eventStore.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		_ = await _eventStore.GetUndispatchedEventsAsync(100, CancellationToken.None)
			.ConfigureAwait(false);

		await _eventStore.MarkEventAsDispatchedAsync(testEvent.EventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities()
			.Where(a => a.Source.Name == EventSourcingActivitySource.Name)
			.ToList();

		activities.Count.ShouldBe(4);
		activities.ShouldContain(a => a.OperationName == EventSourcingActivities.Append);
		activities.ShouldContain(a => a.OperationName == EventSourcingActivities.Load);
		activities.ShouldContain(a => a.OperationName == EventSourcingActivities.GetUndispatched);
		activities.ShouldContain(a => a.OperationName == EventSourcingActivities.MarkDispatched);

		// All operations should have Success result
		foreach (var activity in activities)
		{
			var operationResult = activity.GetTagItem(EventSourcingTags.OperationResult);
			_ = operationResult.ShouldNotBeNull();
			operationResult.ToString().ShouldBe(EventSourcingTagValues.Success);
		}
	}

	[Fact]
	public async Task MaintainTraceContextAcrossOperations()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();

		// Create a parent activity to establish trace context
		using var parentActivity = new ActivitySource("Test.Parent", "1.0.0")
			.StartActivity("ParentOperation");

		_ = parentActivity.ShouldNotBeNull();

		// Act
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", new List<IDomainEvent> { CreateTestEvent() }, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_ = await _eventStore.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities()
			.Where(a => a.Source.Name == EventSourcingActivitySource.Name)
			.ToList();

		activities.Count.ShouldBe(2);

		// All child activities should share the same trace ID
		foreach (var activity in activities)
		{
			activity.TraceId.ShouldBe(parentActivity.TraceId);
		}
	}

	#endregion Multiple Operations Sequence Tests

	#region Empty Result Tests

	[Fact]
	public async Task HandleEmptyLoadResultGracefully()
	{
		// Arrange
		ClearRecordedActivities();
		var nonExistentAggregateId = Guid.NewGuid().ToString();

		// Act - Load for an aggregate that does not exist
		var result = await _eventStore.LoadAsync(nonExistentAggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();

		var activities = GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Load);

		_ = loadActivity.ShouldNotBeNull();
		loadActivity.GetTagItem(EventSourcingTags.EventCount).ShouldBe(0);
		loadActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task HandleEmptyAppendGracefully()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();

		// Act - Append empty event list
		var result = await _eventStore.AppendAsync(aggregateId, "TestAggregate", Array.Empty<IDomainEvent>(), -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - Should succeed immediately without creating a span
		result.Success.ShouldBeTrue();

		var activities = GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		// Empty append should not create an activity span
		appendActivity.ShouldBeNull();
	}

	[Fact]
	public async Task HandleEmptyUndispatchedResultGracefully()
	{
		// Arrange
		ClearRecordedActivities();
		_eventStore.Clear(); // Ensure no events exist

		// Act - Get undispatched from empty store
		var result = await _eventStore.GetUndispatchedEventsAsync(100, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();

		var activities = GetRecordedActivities();
		var getUndispatchedActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.GetUndispatched);

		_ = getUndispatchedActivity.ShouldNotBeNull();
		getUndispatchedActivity.GetTagItem(EventSourcingTags.EventCount).ShouldBe(0);
		getUndispatchedActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	#endregion Empty Result Tests

	#region Activity Source Tests

	[Fact]
	public async Task IncludeCorrectActivitySourceName()
	{
		// Arrange
		ClearRecordedActivities();
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent> { CreateTestEvent() };

		// Act
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.Source.Name.ShouldBe(EventSourcingActivitySource.Name);
	}

	[Fact]
	public async Task NotCreateActivityWhenListenerNotActive()
	{
		// Arrange - Dispose the listener to simulate no active listeners
		_activityListener.Dispose();
		ClearRecordedActivities();

		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent> { CreateTestEvent() };

		// Act - Try to append without listener
		_ = await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - No activities should be recorded since listener was disposed
		var activities = GetRecordedActivities();
		activities.ShouldBeEmpty();
	}

	#endregion Activity Source Tests

	#region Helper Methods

	private static InMemoryTelemetryTestDomainEvent CreateTestEvent()
	{
		return new InMemoryTelemetryTestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = Guid.NewGuid().ToString(),
			Version = 0,
			EventType = "TestEvent",
			OccurredAt = DateTimeOffset.UtcNow,
			Value = "Test-" + Guid.NewGuid().ToString("N"),
		};
	}

	#endregion Helper Methods
}

/// <summary>
/// Test domain event for InMemory telemetry integration tests.
/// </summary>
internal sealed class InMemoryTelemetryTestDomainEvent : IDomainEvent
{
	public required string EventId { get; init; }
	public required string AggregateId { get; init; }
	public required long Version { get; init; }
	public required string EventType { get; init; }
	public required DateTimeOffset OccurredAt { get; init; }
	public IDictionary<string, object>? Metadata { get; init; }
	public required string Value { get; init; }
}
