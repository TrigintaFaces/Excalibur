// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.EventSourcing;

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
	public async ValueTask InitializeAsync()
	{
		Store = await CreateStoreAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
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
		result.Success.ShouldBeTrue($"Append to new stream should succeed. Store reported: {result.ErrorMessage ?? "(no error message)"}");
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
		result.Success.ShouldBeTrue($"Append with empty events should succeed (no-op). Store reported: {result.ErrorMessage ?? "(no error message)"}");
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
		result.Success.ShouldBeTrue($"Append single event should succeed. Store reported: {result.ErrorMessage ?? "(no error message)"}");
		result.NextExpectedVersion.ShouldBe(0); // First event has version 0
	}

	#endregion AppendAsync - New Stream Tests

	#region AppendAsync - AggregateId Validation Tests

	/// <summary>
	/// SAFETY: a null, empty, or whitespace aggregate id is a caller error every store MUST reject rather
	/// than persist under an unaddressable stream key (a stream that can never be loaded back). Each provider
	/// guards the argument with <c>ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId)</c>; this shared fact
	/// is the regression lock that was missing — the cloud providers (Cosmos/Dynamo/Firestore) had the guard
	/// but no conformance coverage binding it, so a dropped guard would ship silently.
	/// </summary>
	/// <remarks>
	/// The LIVENESS counterpart — a valid aggregate id IS accepted — is already asserted by
	/// <see cref="AppendAsync_NewStream_Succeeds"/> and its siblings, so a store that rejected everything would
	/// fail those. Together they pin the real invariant: reject the unaddressable key, accept the addressable
	/// one. <see cref="ArgumentNullException"/> (thrown for <see langword="null"/>) derives from
	/// <see cref="ArgumentException"/>, so a single assertion covers all three inputs.
	/// </remarks>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task AppendAsync_NullOrWhitespaceAggregateId_Throws(string? invalidAggregateId)
	{
		// Arrange: a well-formed event whose only defect is the unaddressable aggregate id under test.
		var events = new[] { CreateTestEvent(Guid.NewGuid().ToString()) };

		// Act + Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await Store.AppendAsync(
				invalidAggregateId!,
				TestAggregateType,
				events,
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion AppendAsync - AggregateId Validation Tests

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
		result.Success.ShouldBeTrue($"Append with correct expected version should succeed. Store reported: {result.ErrorMessage ?? "(no error message)"}");
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

	[Fact]
	public async Task AppendAsync_ExistingStream_WithVersionBeyondTail_Fails()
	{
		// Arrange - seed a 3-event stream so the current tail version is 2 (0-indexed).
		var aggregateId = Guid.NewGuid().ToString();
		_ = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			CreateTestEvents(aggregateId, 3),
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - append with an expectedVersion BEYOND the tail (2). Version contiguity: the store must
		// re-check the current version and reject, not silently write a non-contiguous version (a hole in
		// the stream). A collision-only guard (id-uniqueness / attribute_not_exists) would let this succeed.
		var result = await Store.AppendAsync(
			aggregateId,
			TestAggregateType,
			CreateTestEvents(aggregateId, 1),
			expectedVersion: 5, // Beyond the tail (2) - would create a gap
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse("Append with expectedVersion beyond the stream tail should fail");
		result.IsConcurrencyConflict.ShouldBeTrue("Should indicate concurrency conflict (non-contiguous version rejected)");
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
		//
		// The store reports a refusal by RETURNING AppendResult.CreateFailure(reason) rather than throwing, so
		// asserting the boolean alone discards the reason the store went to the trouble of capturing and the
		// failure reads only "should be true but was false". Every failing result is surfaced here instead:
		// appends to DISTINCT aggregates must not contend, so the reason is the whole finding.
		var failures = results.Where(r => !r.Success).ToList();

		failures.ShouldBeEmpty(
			$"all {concurrentAttempts} concurrent appends to DIFFERENT aggregates must succeed -- distinct "
			+ "aggregates share no stream, so nothing here should contend. "
			+ $"{failures.Count} failed: "
			+ string.Join(
				" | ",
				failures.Select(f => f.IsConcurrencyConflict
					? $"CONFLICT: {f.ErrorMessage}"
					: f.ErrorMessage ?? "(no message)")));
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
	/// <summary>
	/// Concurrent first callers must not fault: lazy initialisation has to be serialised.
	/// </summary>
	/// <remarks>
	/// Deliberately builds a SECOND, fresh store rather than using the fixture's, because the window
	/// this exercises exists only before initialisation completes and the fixture's store is already
	/// past it. Reads a key that does not exist, so nothing is mutated and any fault is the finding.
	/// </para>
	/// <para>
	/// SCOPE, stated because a test that reads as broader than it is would be worse than none. This
	/// was measured against a store with a genuinely unsynchronised initialisation and did NOT detect
	/// it: 0 failures in 5 runs. The reason is structural -- that store assigns its client, database
	/// and collection in three consecutive SYNCHRONOUS statements with no await between them, so a
	/// second caller can only observe the half-built state through true parallelism in a window of a
	/// few instructions. CI hit it under load; a barrier on a quiet machine does not.
	/// </para>
	/// <para>
	/// It is also VACUOUS for a store the deriver hands back already initialised. 22 of the 77
	/// conformance derivers call InitializeAsync inside their factory (or share one document store
	/// across the class), so for those the store has already passed through the window before this
	/// fact runs and no number of concurrent callers can re-enter it. That is nearly a third, and it
	/// is not a defect in those derivers -- eager initialisation is what their production wiring does
	/// -- but it does mean this fact must not be read as covering them.
	/// </para>
	/// <para>
	/// So this is a guard against GROSS concurrency faults -- an operation that throws, deadlocks, or
	/// corrupts shared state when entered many times at once -- and it is NOT the detector for the
	/// narrow lazy-init race. The name said "Race First Use", which claimed exactly the thing the
	/// paragraphs above disclaim; it now says what it asserts. The race itself is bound by two tests
	/// that do not depend on observing it: LazyInitialisationRunsExactlyOnceShould forces the
	/// interleaving deterministically and asserts the body runs once (25/25 runs, no container), and
	/// LazyInitialisationIsGuardedTests asserts structurally that every store has the guard at all.
	/// </remarks>
	[Fact]
	public virtual async Task Should_Not_Fault_When_Many_Callers_Use_The_Store_Concurrently()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var absent = Guid.NewGuid().ToString();

		await ConcurrentFirstUse.ShouldNotFaultAsync(
			async () => _ = await store.LoadAsync(absent, TestAggregateType, CancellationToken.None).ConfigureAwait(false),
			"the event store").ConfigureAwait(false);
	}
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
