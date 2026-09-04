// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Observability;

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Integration tests for CosmosDbEventStore OpenTelemetry instrumentation.
/// Validates that ActivitySource spans are correctly created with proper tags
/// when executing actual database operations against a CosmosDb emulator.
/// </summary>
/// <remarks>
/// <para>
/// In CI the emulator is required, not optional: an unavailable emulator fails this suite with a named
/// diagnostic rather than skipping it. A skipped test is recorded as not-executed, so it is not counted
/// as a pass — but the run still exits green with no failures, and this is the only place the CosmosDb
/// provider's telemetry is exercised. A skip here is therefore a silent hole in CI coverage even though
/// the counters describe it honestly, which is why CI fails instead. The counter that shows it is
/// executed against expected; failed-count and exit code cannot.
/// </para>
/// <para>
/// Outside CI the emulator remains optional, so a developer without a container runtime gets a skip
/// rather than a spurious failure.
/// </para>
/// </remarks>
[Collection("EventStore Telemetry Tests")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Component", "Platform")]
[Trait("Infrastructure", "CosmosEmulator")]
public sealed class CosmosDbEventStoreTelemetryShould : IClassFixture<CosmosDbEventStoreTelemetryTestFixture>, IAsyncLifetime
{
	/// <summary>
	/// Names the environment variable holding the path of the evidence file. When unset, no evidence is
	/// written.
	/// </summary>
	private const string EvidenceFileVariable = "COSMOS_EVIDENCE_FILE";

	private readonly CosmosDbEventStoreTelemetryTestFixture _fixture;

	public CosmosDbEventStoreTelemetryShould(CosmosDbEventStoreTelemetryTestFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync() => default;

	public async ValueTask DisposeAsync()
	{
		if (_fixture.IsInitialized)
		{
			await _fixture.CleanupContainerAsync().ConfigureAwait(false);
		}

		_fixture.ClearRecordedActivities();
	}

	#region Event Store Span Creation Tests

	[Fact]
	public async Task CreateActivitySpanForAppendOperation()
	{
		// Arrange
		RequireEmulator();
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
		RequireEmulator();
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
		RequireEmulator();
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

	#endregion Event Store Span Creation Tests

	#region Tag Verification Tests

	[Fact]
	public async Task SetEventCountTagOnSuccessfulAppend()
	{
		// Arrange
		RequireEmulator();
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
		RequireEmulator();
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
		RequireEmulator();
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
		RequireEmulator();
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
		RequireEmulator();
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
		RequireEmulator();
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
	public async Task SetConcurrencyConflictOnCosmosDbConflictException()
	{
		// Arrange
		// This tests the case where CosmosDb returns HttpStatusCode.Conflict (409)
		// which happens when there's a concurrency violation
		RequireEmulator();
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

	#endregion Operation Result Tests

	#region Multiple Operations Sequence Tests

	[Fact]
	public async Task RecordMultipleOperationsInSequence()
	{
		// Arrange
		RequireEmulator();
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var testEvent = CreateTestEvent();

		// Act - Perform sequence: Append -> Load
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", new List<IDomainEvent> { testEvent }, -1, CancellationToken.None)
			.ConfigureAwait(false);

		_ = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var activities = _fixture.GetRecordedActivities()
			.Where(a => a.Source.Name == EventSourcingActivitySource.Name)
			.ToList();

		activities.Count.ShouldBe(2);
		activities.ShouldContain(a => a.OperationName == EventSourcingActivities.Append);
		activities.ShouldContain(a => a.OperationName == EventSourcingActivities.Load);

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
		RequireEmulator();
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
		RequireEmulator();
		_fixture.ClearRecordedActivities();
		var eventStore = _fixture.CreateEventStore();
		var nonExistentAggregateId = Guid.NewGuid().ToString();

		// Act - Load for an aggregate that doesn't exist
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
		RequireEmulator();
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

	private static CosmosDbTelemetryTestDomainEvent CreateTestEvent()
	{
		return new CosmosDbTelemetryTestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = Guid.NewGuid().ToString(),
			Version = 0,
			OccurredAt = DateTimeOffset.UtcNow,
			Value = $"Test-{Guid.NewGuid():N}",
		};
	}

	/// <summary>
	/// Gates a test on emulator availability, failing in CI and skipping only outside it, then records
	/// that the emulator was genuinely reached.
	/// </summary>
	/// <param name="testName">
	/// The calling test method, supplied by the compiler. Written to the evidence file so a reader can
	/// tell which tests executed rather than only how many.
	/// </param>
	private void RequireEmulator([CallerMemberName] string testName = "")
	{
		if (EmulatorRequirement.IsRequired(
			Environment.GetEnvironmentVariable("CI"),
			Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
		{
			_fixture.IsInitialized.ShouldBeTrue(
				$"The CosmosDb emulator is REQUIRED in CI and was unavailable: " +
				$"{nameof(CosmosDbEventStoreTelemetryTestFixture)} failed to initialize, so " +
				$"{nameof(CosmosDbEventStoreTelemetryShould)} could verify nothing. This suite must not " +
				$"skip in CI: a skipped run still exits green with zero failures, so nothing in the " +
				$"result summary would report the gap. Check that a container runtime is present and " +
				$"that the emulator image started.");
		}
		else
		{
			Assert.SkipUnless(_fixture.IsInitialized, "CosmosDb emulator not available");
		}

		// Reached only when the emulator is genuinely available: both branches above throw otherwise, so a
		// skipped or failed run cannot write the marker.
		RecordEmulatorReached(testName);
	}

	/// <summary>
	/// Appends one line of positive evidence that a test genuinely reached the emulator.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Contract: when the environment variable named by <see cref="EvidenceFileVariable"/> holds a path,
	/// each genuinely-executed test appends exactly one line to that file, tab-separated as
	/// <c>{UTC timestamp, round-trip format}\t{test class name}\t{test method name}</c>. When the
	/// variable is unset or blank, nothing is written, so local development is unaffected.
	/// </para>
	/// <para>
	/// The marker is positive evidence only. A lost line under-reports and can only make a reader
	/// conclude less was verified than actually was; nothing here can fabricate a line for a test that
	/// did not run. All failures are swallowed deliberately: evidence collection is diagnostic and must
	/// never turn a passing test red.
	/// </para>
	/// </remarks>
	/// <param name="testName">The test method that reached the emulator.</param>
	private static void RecordEmulatorReached(string testName)
	{
		try
		{
			var path = Environment.GetEnvironmentVariable(EvidenceFileVariable);
			if (string.IsNullOrWhiteSpace(path))
			{
				return;
			}

			var line = string.Concat(
				DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
				"\t",
				nameof(CosmosDbEventStoreTelemetryShould),
				"\t",
				testName,
				Environment.NewLine);

			File.AppendAllText(path, line);
		}
		catch (Exception)
		{
			// Diagnostic only. An unwritable path, a racing append, or a missing directory must not
			// convert a genuinely passing test into a failure.
		}
	}

	#endregion Helper Methods
}

/// <summary>
/// Test domain event for CosmosDb telemetry integration tests.
/// </summary>
[MessageName("Test.CosmosDbTelemetryTestDomainEvent")]
internal sealed class CosmosDbTelemetryTestDomainEvent : IDomainEvent
{
	public required string EventId { get; init; }
	public required string AggregateId { get; init; }
	public required long Version { get; init; }
	public required DateTimeOffset OccurredAt { get; init; }
	public IDictionary<string, object>? Metadata { get; init; }
	public required string Value { get; init; }
}
