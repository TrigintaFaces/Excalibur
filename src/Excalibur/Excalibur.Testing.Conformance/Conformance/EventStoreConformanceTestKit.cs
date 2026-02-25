// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0007 // Use implicit type (var)
#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IEventStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your event store implementation conforms to the IEventStore contract.
/// </para>
/// <para>
/// The test kit uses the abstract class pattern to provide shared test helpers
/// while allowing each provider to supply its own store factory and cleanup logic.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerEventStoreConformanceTests : EventStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override IEventStore CreateStore() =>
///         new SqlServerEventStore(_fixture.ConnectionString);
///
///     protected override async Task CleanupAsync() =>
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class EventStoreConformanceTestKit
{
	private const string DefaultAggregateType = "TestAggregate";

	/// <summary>
	/// Creates a fresh event store instance for testing.
	/// </summary>
	/// <returns>An IEventStore implementation to test.</returns>
	protected abstract IEventStore CreateStore();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Creates test events for the given aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="count">Number of events to create.</param>
	/// <param name="startVersion">Starting version number.</param>
	/// <returns>A list of test domain events.</returns>
	protected virtual IReadOnlyList<IDomainEvent> CreateTestEvents(
		string aggregateId,
		int count,
		long startVersion = 1)
	{
		return Enumerable.Range(0, count)
			.Select(i => TestDomainEvent.Create(aggregateId, startVersion + i))
			.ToList();
	}

	/// <summary>
	/// Generates a unique aggregate ID for test isolation.
	/// </summary>
	/// <returns>A unique aggregate identifier.</returns>
	protected virtual string GenerateAggregateId() => Guid.NewGuid().ToString();

	#region Append Tests

	/// <summary>
	/// Verifies that appending events to a new stream succeeds.
	/// </summary>
	public virtual async Task AppendAsync_ToNewStream_ShouldSucceed()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var events = CreateTestEvents(aggregateId, 3);

		var result = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		if (!result.Success)
		{
			throw new TestFixtureAssertionException(
				$"Expected append to succeed but got: {result.ErrorMessage}");
		}

		if (result.NextExpectedVersion != 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected NextExpectedVersion to be 3 but was {result.NextExpectedVersion}");
		}
	}

	/// <summary>
	/// Verifies that appending with wrong expected version fails with concurrency conflict.
	/// </summary>
	public virtual async Task AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var events1 = CreateTestEvents(aggregateId, 2);
		var events2 = CreateTestEvents(aggregateId, 1, 3);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events1,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var result = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events2,
			-1, // Wrong - stream already has version 2
			CancellationToken.None).ConfigureAwait(false);

		if (result.Success)
		{
			throw new TestFixtureAssertionException(
				"Expected append to fail due to version mismatch but it succeeded");
		}

		if (!result.IsConcurrencyConflict)
		{
			throw new TestFixtureAssertionException(
				$"Expected IsConcurrencyConflict to be true. Error: {result.ErrorMessage}");
		}
	}

	/// <summary>
	/// Verifies that appending with correct expected version succeeds.
	/// </summary>
	public virtual async Task AppendAsync_WithCorrectExpectedVersion_ShouldSucceed()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var events1 = CreateTestEvents(aggregateId, 2);
		var events2 = CreateTestEvents(aggregateId, 3, 3);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events1,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var result = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events2,
			2, // Correct - stream has version 2
			CancellationToken.None).ConfigureAwait(false);

		if (!result.Success)
		{
			throw new TestFixtureAssertionException(
				$"Expected append to succeed but got: {result.ErrorMessage}");
		}

		if (result.NextExpectedVersion != 5)
		{
			throw new TestFixtureAssertionException(
				$"Expected NextExpectedVersion to be 5 but was {result.NextExpectedVersion}");
		}
	}

	/// <summary>
	/// Verifies that appending empty events doesn't change version.
	/// </summary>
	public virtual async Task AppendAsync_EmptyEvents_ShouldNotChangeVersion()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var events1 = CreateTestEvents(aggregateId, 2);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events1,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var result = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			Array.Empty<IDomainEvent>(),
			2,
			CancellationToken.None).ConfigureAwait(false);

		// Empty append should succeed and not change version
		if (!result.Success)
		{
			throw new TestFixtureAssertionException(
				$"Expected empty append to succeed but got: {result.ErrorMessage}");
		}
	}

	#endregion

	#region Load Tests

	/// <summary>
	/// Verifies that loading from an empty/non-existent stream returns empty list.
	/// </summary>
	public virtual async Task LoadAsync_EmptyStream_ShouldReturnEmpty()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();

		var events = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (events.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected empty stream to return 0 events but got {events.Count}");
		}
	}

	/// <summary>
	/// Verifies that loading returns all events for an aggregate.
	/// </summary>
	public virtual async Task LoadAsync_ExistingStream_ShouldReturnAllEvents()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var testEvents = CreateTestEvents(aggregateId, 5);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			testEvents,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 5)
		{
			throw new TestFixtureAssertionException(
				$"Expected 5 events but loaded {loaded.Count}");
		}
	}

	/// <summary>
	/// Verifies that events are loaded in version order.
	/// </summary>
	public virtual async Task LoadAsync_ShouldReturnEventsInVersionOrder()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var testEvents = CreateTestEvents(aggregateId, 5);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			testEvents,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		for (int i = 1; i < loaded.Count; i++)
		{
			if (loaded[i].Version <= loaded[i - 1].Version)
			{
				throw new TestFixtureAssertionException(
					$"Events not in version order: version {loaded[i - 1].Version} followed by {loaded[i].Version}");
			}
		}
	}

	/// <summary>
	/// Verifies that loading from a specific version returns only events after that version.
	/// </summary>
	public virtual async Task LoadAsync_FromVersion_ShouldReturnEventsAfterVersion()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var testEvents = CreateTestEvents(aggregateId, 5);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			testEvents,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			2, // Load events after version 2
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 3) // versions 3, 4, 5
		{
			throw new TestFixtureAssertionException(
				$"Expected 3 events (versions 3-5) but loaded {loaded.Count}");
		}

		if (loaded[0].Version != 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected first event to be version 3 but was {loaded[0].Version}");
		}
	}

	/// <summary>
	/// Verifies that loading from a version beyond the stream returns empty.
	/// </summary>
	public virtual async Task LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var testEvents = CreateTestEvents(aggregateId, 3);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			testEvents,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			100, // Way beyond stream end
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected 0 events when loading from beyond stream but got {loaded.Count}");
		}
	}

	#endregion

	#region Isolation Tests

	/// <summary>
	/// Verifies that events are isolated by aggregate type.
	/// </summary>
	public virtual async Task LoadAsync_ShouldIsolateByAggregateType()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var eventsTypeA = CreateTestEvents(aggregateId, 2);
		var eventsTypeB = CreateTestEvents(aggregateId, 3);

		_ = await store.AppendAsync(
			aggregateId,
			"TypeA",
			eventsTypeA,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		_ = await store.AppendAsync(
			aggregateId,
			"TypeB",
			eventsTypeB,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loadedA = await store.LoadAsync(
			aggregateId,
			"TypeA",
			CancellationToken.None).ConfigureAwait(false);

		var loadedB = await store.LoadAsync(
			aggregateId,
			"TypeB",
			CancellationToken.None).ConfigureAwait(false);

		if (loadedA.Count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected 2 events for TypeA but loaded {loadedA.Count}");
		}

		if (loadedB.Count != 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected 3 events for TypeB but loaded {loadedB.Count}");
		}
	}

	/// <summary>
	/// Verifies that events are isolated by aggregate ID.
	/// </summary>
	public virtual async Task LoadAsync_ShouldIsolateByAggregateId()
	{
		var store = CreateStore();
		var aggregateId1 = GenerateAggregateId();
		var aggregateId2 = GenerateAggregateId();
		var events1 = CreateTestEvents(aggregateId1, 2);
		var events2 = CreateTestEvents(aggregateId2, 4);

		_ = await store.AppendAsync(
			aggregateId1,
			DefaultAggregateType,
			events1,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		_ = await store.AppendAsync(
			aggregateId2,
			DefaultAggregateType,
			events2,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded1 = await store.LoadAsync(
			aggregateId1,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		var loaded2 = await store.LoadAsync(
			aggregateId2,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded1.Count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected 2 events for aggregate1 but loaded {loaded1.Count}");
		}

		if (loaded2.Count != 4)
		{
			throw new TestFixtureAssertionException(
				$"Expected 4 events for aggregate2 but loaded {loaded2.Count}");
		}
	}

	#endregion

	#region Outbox Tests

	/// <summary>
	/// Verifies that newly appended events are marked as undispatched.
	/// </summary>
	public virtual async Task GetUndispatchedEventsAsync_ShouldReturnNewEvents()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var testEvents = CreateTestEvents(aggregateId, 3);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			testEvents,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var undispatched = await store.GetUndispatchedEventsAsync(
			100,
			CancellationToken.None).ConfigureAwait(false);

		// There should be at least our 3 events (could be more from other tests)
		if (undispatched.Count < 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 3 undispatched events but got {undispatched.Count}");
		}

		// All returned events should have IsDispatched = false
		foreach (var evt in undispatched)
		{
			if (evt.IsDispatched)
			{
				throw new TestFixtureAssertionException(
					$"Event {evt.EventId} should not be marked as dispatched");
			}
		}
	}

	/// <summary>
	/// Verifies that marking an event as dispatched removes it from undispatched list.
	/// </summary>
	public virtual async Task MarkEventAsDispatchedAsync_ShouldRemoveFromUndispatched()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var testEvents = CreateTestEvents(aggregateId, 1);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			testEvents,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var undispatched = await store.GetUndispatchedEventsAsync(
			100,
			CancellationToken.None).ConfigureAwait(false);

		var eventToMark = undispatched.FirstOrDefault(e => e.AggregateId == aggregateId);
		if (eventToMark is null)
		{
			throw new TestFixtureAssertionException(
				"Could not find the appended event in undispatched list");
		}

		await store.MarkEventAsDispatchedAsync(
			eventToMark.EventId,
			CancellationToken.None).ConfigureAwait(false);

		var afterMark = await store.GetUndispatchedEventsAsync(
			100,
			CancellationToken.None).ConfigureAwait(false);

		if (afterMark.Any(e => e.EventId == eventToMark.EventId))
		{
			throw new TestFixtureAssertionException(
				$"Event {eventToMark.EventId} should not appear in undispatched list after marking");
		}
	}

	/// <summary>
	/// Verifies that batch size limit is respected.
	/// </summary>
	public virtual async Task GetUndispatchedEventsAsync_ShouldRespectBatchSize()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var testEvents = CreateTestEvents(aggregateId, 10);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			testEvents,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var undispatched = await store.GetUndispatchedEventsAsync(
			3, // Request only 3
			CancellationToken.None).ConfigureAwait(false);

		if (undispatched.Count > 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected at most 3 events (batch size) but got {undispatched.Count}");
		}
	}

	#endregion

	#region Data Integrity Tests

	/// <summary>
	/// Verifies that event data is preserved through round-trip.
	/// </summary>
	public virtual async Task AppendAndLoad_ShouldPreserveEventData()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var testEvent = TestDomainEvent.Create(aggregateId, 1);
		var originalEventId = testEvent.EventId;

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			[testEvent],
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 1)
		{
			throw new TestFixtureAssertionException($"Expected 1 event but got {loaded.Count}");
		}

		var loadedEvent = loaded[0];
		if (loadedEvent.EventId != originalEventId)
		{
			throw new TestFixtureAssertionException(
				$"EventId mismatch: expected {originalEventId}, got {loadedEvent.EventId}");
		}

		if (loadedEvent.AggregateId != aggregateId)
		{
			throw new TestFixtureAssertionException(
				$"AggregateId mismatch: expected {aggregateId}, got {loadedEvent.AggregateId}");
		}

		if (loadedEvent.Version != 1)
		{
			throw new TestFixtureAssertionException(
				$"Version mismatch: expected 1, got {loadedEvent.Version}");
		}
	}

	/// <summary>
	/// Verifies that metadata is preserved through round-trip.
	/// </summary>
	public virtual async Task AppendAndLoad_ShouldPreserveMetadata()
	{
		var store = CreateStore();
		var aggregateId = GenerateAggregateId();
		var metadata = new Dictionary<string, object> { ["UserId"] = "user-123", ["TenantId"] = "tenant-456" };
		var testEvent = new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			Version = 1,
			OccurredAt = DateTimeOffset.UtcNow,
			Metadata = metadata,
			Payload = "test-with-metadata"
		};

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			[testEvent],
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 1)
		{
			throw new TestFixtureAssertionException($"Expected 1 event but got {loaded.Count}");
		}

		var loadedMetadata = loaded[0].Metadata;
		if (loadedMetadata is null || loadedMetadata.Length == 0)
		{
			throw new TestFixtureAssertionException("Metadata was not preserved");
		}
	}

	#endregion
}
