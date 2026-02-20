// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Observability;

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Integration tests for DynamoDbEventStore OpenTelemetry instrumentation.
/// Validates that ActivitySource spans are correctly created with proper tags
/// when executing actual database operations against LocalStack DynamoDB.
/// </summary>
/// <remarks>
/// Note: LocalStack may have limitations in some CI environments.
/// Tests may fail if LocalStack/Docker is not available.
/// DynamoDB uses two concurrency exception types:
/// - TransactionCanceledException for transactional writes (≤100 events)
/// - ConditionalCheckFailedException for sequential writes
/// </remarks>
[Collection("EventStore Telemetry Tests")]
public sealed class DynamoDbEventStoreTelemetryShould : IClassFixture<DynamoDbEventStoreTelemetryTestFixture>, IAsyncLifetime
{
	private readonly DynamoDbEventStoreTelemetryTestFixture _fixture;

	public DynamoDbEventStoreTelemetryShould(DynamoDbEventStoreTelemetryTestFixture fixture)
	{
		_fixture = fixture;
	}

	public Task InitializeAsync() => Task.CompletedTask;

	public async Task DisposeAsync()
	{
		if (_fixture.IsInitialized)
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}

		_fixture.ClearRecordedActivities();
	}

	#region Event Store Span Creation Tests

	[Fact]
	public async Task CreateActivitySpanForAppendOperation()
	{
		// Arrange
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent> { CreateTestEvent() };

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
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
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// First append an event so we can load it
		var events = new List<IDomainEvent> { CreateTestEvent() };
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act
		_ = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Load);

		_ = loadActivity.ShouldNotBeNull();
		loadActivity.GetTagItem(EventSourcingTags.AggregateId).ShouldBe(aggregateId);
		loadActivity.GetTagItem(EventSourcingTags.AggregateType).ShouldBe("TestAggregate");
	}

	[Fact]
	public async Task CreateActivitySpanForLoadWithFromVersion()
	{
		// Arrange
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Append multiple events
		var events = Enumerable.Range(0, 3).Select(_ => CreateTestEvent()).ToList();
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act - Load from version 1
		_ = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", 1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Load);

		_ = loadActivity.ShouldNotBeNull();
		loadActivity.GetTagItem(EventSourcingTags.FromVersion).ShouldBe(1L);
	}

	[Fact]
	public async Task CreateActivitySpanForGetUndispatchedEvents()
	{
		// Arrange
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Append an event (will be undispatched by default)
		var events = new List<IDomainEvent> { CreateTestEvent() };
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act
		_ = await eventStore.GetUndispatchedEventsAsync(100, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var getUndispatchedActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.GetUndispatched);

		_ = getUndispatchedActivity.ShouldNotBeNull();
		getUndispatchedActivity.GetTagItem(EventSourcingTags.BatchSize).ShouldBe(100);
	}

	[Fact]
	public async Task CreateActivitySpanForMarkEventAsDispatched()
	{
		// Arrange
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var testEvent = CreateTestEvent();

		// Append an event
		var events = new List<IDomainEvent> { testEvent };
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act
		await eventStore.MarkEventAsDispatchedAsync(testEvent.EventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
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
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var events = Enumerable.Range(0, 5).Select(_ => CreateTestEvent()).ToList();

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.EventCount).ShouldBe(5);
	}

	[Fact]
	public async Task SetVersionTagOnSuccessfulAppend()
	{
		// Arrange
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent> { CreateTestEvent() };

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.Version).ShouldBe(0L);
	}

	[Fact]
	public async Task SetEventCountTagOnSuccessfulLoad()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Append 3 events
		var events = Enumerable.Range(0, 3).Select(_ => CreateTestEvent()).ToList();
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act
		_ = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
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
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent> { CreateTestEvent() };

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task SetSuccessResultOnSuccessfulLoad()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent> { CreateTestEvent() };
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act
		_ = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Load);

		_ = loadActivity.ShouldNotBeNull();
		loadActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task SetConcurrencyConflictResultOnVersionMismatch()
	{
		// Arrange
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// First, append an event at version 0
		var firstEvents = new List<IDomainEvent> { CreateTestEvent() };
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", firstEvents, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act - Try to append with wrong expected version (should fail with concurrency conflict)
		var conflictEvents = new List<IDomainEvent> { CreateTestEvent() };
		var result = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", conflictEvents, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();

		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.ConcurrencyConflict);
	}

	[Fact]
	public async Task SetConcurrencyConflictOnDynamoDbConditionCheckFailed()
	{
		// Arrange
		// This tests the case where DynamoDB returns ConditionalCheckFailedException
		// which happens when there is a concurrency violation
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// First append sets version 0
		var firstEvents = new List<IDomainEvent> { CreateTestEvent() };
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", firstEvents, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Now we try to append again expecting version -1 (new aggregate)
		// This will cause version mismatch (current is 0, expected is -1)
		// The store should detect this and return concurrency conflict
		var secondEvents = new List<IDomainEvent> { CreateTestEvent() };
		var result = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", secondEvents, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();

		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.ConcurrencyConflict);
	}

	[Fact]
	public async Task SetSuccessResultOnGetUndispatched()
	{
		// Arrange
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();

		// Act
		_ = await eventStore.GetUndispatchedEventsAsync(100, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var getUndispatchedActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.GetUndispatched);

		_ = getUndispatchedActivity.ShouldNotBeNull();
		getUndispatchedActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task SetSuccessResultOnMarkDispatched()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var testEvent = CreateTestEvent();

		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", new List<IDomainEvent> { testEvent }, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act
		await eventStore.MarkEventAsDispatchedAsync(testEvent.EventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
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
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var testEvent = CreateTestEvent();

		// Act - Perform sequence: Append -> Load -> GetUndispatched -> MarkDispatched
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", new List<IDomainEvent> { testEvent }, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_ = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		_ = await eventStore.GetUndispatchedEventsAsync(100, CancellationToken.None)
			.ConfigureAwait(false);

		await eventStore.MarkEventAsDispatchedAsync(testEvent.EventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities()
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
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Create a parent activity to establish trace context
		using var parentActivity = new ActivitySource("Test.Parent", "1.0.0")
			.StartActivity("ParentOperation");

		_ = parentActivity.ShouldNotBeNull();

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", new List<IDomainEvent> { CreateTestEvent() }, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_ = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities()
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
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var nonExistentAggregateId = Guid.NewGuid().ToString();

		// Act - Load for an aggregate that does not exist
		var result = await ((IEventStore)eventStore).LoadAsync(nonExistentAggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();

		var activities = _fixture.GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Load);

		_ = loadActivity.ShouldNotBeNull();
		loadActivity.GetTagItem(EventSourcingTags.EventCount).ShouldBe(0);
		loadActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task HandleEmptyAppendGracefully()
	{
		// Arrange
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Act - Append empty event list
		var result = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", Array.Empty<IDomainEvent>(), -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - Should succeed immediately without creating a span
		result.Success.ShouldBeTrue();

		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		// Empty append should not create an activity span
		appendActivity.ShouldBeNull();
	}

	#endregion Empty Result Tests

	#region Helper Methods

	private static DynamoDbTelemetryTestDomainEvent CreateTestEvent()
	{
		return new DynamoDbTelemetryTestDomainEvent
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
/// Test domain event for DynamoDb telemetry integration tests.
/// </summary>
internal sealed class DynamoDbTelemetryTestDomainEvent : IDomainEvent
{
	public required string EventId { get; init; }
	public required string AggregateId { get; init; }
	public required long Version { get; init; }
	public required string EventType { get; init; }
	public required DateTimeOffset OccurredAt { get; init; }
	public IDictionary<string, object>? Metadata { get; init; }
	public required string Value { get; init; }
}
