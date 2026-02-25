// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Observability;

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Integration tests for PostgresEventStore OpenTelemetry instrumentation.
/// Validates that ActivitySource spans are correctly created with proper tags
/// when executing actual database operations against a Postgres container.
/// </summary>
[Collection("EventStore Telemetry Tests")]
public sealed class PostgresEventStoreTelemetryShould : IClassFixture<PostgresEventStoreTelemetryTestFixture>, IAsyncLifetime
{
	private readonly PostgresEventStoreTelemetryTestFixture _fixture;

	public PostgresEventStoreTelemetryShould(PostgresEventStoreTelemetryTestFixture fixture)
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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — filter by aggregateId to avoid picking up activities from concurrent tests
		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Append &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);

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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act
		_ = await eventStore.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Load &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);

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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act - Load from version 1
		_ = await eventStore.LoadAsync(aggregateId, "TestAggregate", 1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Load &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);

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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — filter by aggregateId to avoid picking up activities from concurrent tests
		// (the ActivityListener is process-global and captures all EventSourcing activities)
		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Append &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.EventCount).ShouldNotBeNull();
		((int)appendActivity.GetTagItem(EventSourcingTags.EventCount)!).ShouldBeGreaterThanOrEqualTo(5);
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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Append &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);

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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act
		_ = await eventStore.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Load &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);

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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Append &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);

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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act
		_ = await eventStore.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Load &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);

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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", firstEvents, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_fixture.ClearRecordedActivities();

		// Act - Try to append with wrong expected version (should fail with concurrency conflict)
		var conflictEvents = new List<IDomainEvent> { CreateTestEvent() };
		var result = await eventStore.AppendAsync(aggregateId, "TestAggregate", conflictEvents, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();

		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Append &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);

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

		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", new List<IDomainEvent> { testEvent }, -1, CancellationToken.None)
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
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", new List<IDomainEvent> { testEvent }, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_ = await eventStore.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		_ = await eventStore.GetUndispatchedEventsAsync(100, CancellationToken.None)
			.ConfigureAwait(false);

		await eventStore.MarkEventAsDispatchedAsync(testEvent.EventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities()
			.Where(a => a.Source.Name == EventSourcingActivitySource.Name)
			.ToList();

		activities.Count.ShouldBeGreaterThanOrEqualTo(4);

		// Validate only activities created by this test's operation sequence.
		var appendActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Append &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);
		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.OperationResult)?.ToString()
			.ShouldBe(EventSourcingTagValues.Success);

		var loadActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Load &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);
		_ = loadActivity.ShouldNotBeNull();
		loadActivity.GetTagItem(EventSourcingTags.OperationResult)?.ToString()
			.ShouldBe(EventSourcingTagValues.Success);

		var markDispatchedActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.MarkDispatched &&
			(string?)a.GetTagItem(EventSourcingTags.EventId) == testEvent.EventId);
		_ = markDispatchedActivity.ShouldNotBeNull();
		markDispatchedActivity.GetTagItem(EventSourcingTags.OperationResult)?.ToString()
			.ShouldBe(EventSourcingTagValues.Success);

		// GetUndispatched has no aggregate tag; assert that at least one successful call exists.
		activities.ShouldContain(a =>
			a.OperationName == EventSourcingActivities.GetUndispatched &&
			a.GetTagItem(EventSourcingTags.OperationResult).ToString() == EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task MaintainTraceContextAcrossOperations()
	{
		// Arrange
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Create a parent activity to establish trace context.
		// Use a dedicated listener so the parent ActivitySource is sampled
		// without broadening the fixture's ShouldListenTo filter.
		using var parentSource = new ActivitySource("Test.Parent", "1.0.0");
		using var parentListener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Test.Parent",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
		};
		ActivitySource.AddActivityListener(parentListener);

		using var parentActivity = parentSource.StartActivity("ParentOperation");
		_ = parentActivity.ShouldNotBeNull();

		// Act
		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", new List<IDomainEvent> { CreateTestEvent() }, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_ = await eventStore.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
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

		// Act - Load for an aggregate that doesn't exist
		var result = await eventStore.LoadAsync(nonExistentAggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();

		var activities = _fixture.GetRecordedActivities();
		var loadActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Load &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == nonExistentAggregateId);

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
		var result = await eventStore.AppendAsync(aggregateId, "TestAggregate", Array.Empty<IDomainEvent>(), -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - Should succeed immediately without creating a span
		result.Success.ShouldBeTrue();

		var activities = _fixture.GetRecordedActivities();
		var appendActivity = activities.FirstOrDefault(a =>
			a.OperationName == EventSourcingActivities.Append &&
			(string?)a.GetTagItem(EventSourcingTags.AggregateId) == aggregateId);

		// Empty append should not create an activity span
		appendActivity.ShouldBeNull();
	}

	#endregion Empty Result Tests

	#region Helper Methods

	private static PostgresTelemetryTestDomainEvent CreateTestEvent()
	{
		return new PostgresTelemetryTestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = Guid.NewGuid().ToString(),
			Version = 0,
			EventType = "TestEvent",
			OccurredAt = DateTimeOffset.UtcNow,
			Value = $"Test-{Guid.NewGuid():N}",
		};
	}

	#endregion Helper Methods
}

/// <summary>
/// Test domain event for Postgres telemetry integration tests.
/// </summary>
internal sealed class PostgresTelemetryTestDomainEvent : IDomainEvent
{
	public required string EventId { get; init; }
	public required string AggregateId { get; init; }
	public required long Version { get; init; }
	public required string EventType { get; init; }
	public required DateTimeOffset OccurredAt { get; init; }
	public IDictionary<string, object>? Metadata { get; init; }
	public required string Value { get; init; }
}
