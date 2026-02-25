// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.InMemory;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.EventSourcing.Tests.Concurrency;

/// <summary>
/// Concurrency and stress tests for <see cref="InMemoryEventStore"/> to verify thread-safety
/// of event append operations, optimistic concurrency control, and concurrent access patterns.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EventStoreConcurrencyShould
{
	private const string AggregateType = "TestAggregate";
	private readonly InMemoryEventStore _store = new();

	#region Concurrent Append — Same Aggregate

	[Fact]
	public async Task ConcurrentAppends_ToSameAggregate_ProduceConcurrencyConflicts()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var concurrency = 10;

		// Act: All try to append at expectedVersion -1 (new aggregate)
		var tasks = Enumerable.Range(0, concurrency)
			.Select(_ => _store.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { CreateTestEvent(aggregateId) },
				expectedVersion: -1,
				CancellationToken.None).AsTask())
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert: Exactly one should succeed, rest should be concurrency conflicts
		var successes = results.Count(r => r.Success);
		var conflicts = results.Count(r => r.IsConcurrencyConflict);

		successes.ShouldBe(1, "Exactly one concurrent append should succeed for a new aggregate");
		conflicts.ShouldBe(concurrency - 1, "Remaining appends should be concurrency conflicts");
	}

	[Fact]
	public async Task ConcurrentAppends_ToSameAggregate_AtSameVersion_OnlyOneSucceeds()
	{
		// Arrange: Create the aggregate with initial event
		var aggregateId = Guid.NewGuid().ToString();
		var initialResult = await _store.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		initialResult.Success.ShouldBeTrue();
		var currentVersion = initialResult.NextExpectedVersion;

		// Act: 5 concurrent appends all targeting the same version
		var concurrency = 5;
		var tasks = Enumerable.Range(0, concurrency)
			.Select(_ => _store.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { CreateTestEvent(aggregateId) },
				expectedVersion: currentVersion,
				CancellationToken.None).AsTask())
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		var successes = results.Count(r => r.Success);
		successes.ShouldBe(1, "Only one append should succeed at the same expected version");
	}

	[Fact]
	public async Task SequentialAppends_ToSameAggregate_AllSucceed()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var version = -1L;

		// Act: Sequential appends with correct version chaining
		for (var i = 0; i < 20; i++)
		{
			var result = await _store.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { CreateTestEvent(aggregateId) },
				expectedVersion: version,
				CancellationToken.None).ConfigureAwait(false);

			result.Success.ShouldBeTrue($"Append {i} should succeed");
			version = result.NextExpectedVersion;
		}

		// Assert
		var events = await _store.LoadAsync(aggregateId, AggregateType, CancellationToken.None)
			.ConfigureAwait(false);
		events.Count.ShouldBe(20, "All 20 sequential appends should be stored");
	}

	#endregion

	#region Concurrent Append — Different Aggregates

	[Fact]
	public async Task ConcurrentAppends_ToDifferentAggregates_AllSucceed()
	{
		// Arrange
		var concurrency = 20;
		var aggregateIds = Enumerable.Range(0, concurrency)
			.Select(_ => Guid.NewGuid().ToString())
			.ToArray();

		// Act: Each task appends to its own aggregate
		var tasks = aggregateIds
			.Select(id => _store.AppendAsync(
				id,
				AggregateType,
				new IDomainEvent[] { CreateTestEvent(id) },
				expectedVersion: -1,
				CancellationToken.None).AsTask())
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert: All should succeed since they target different aggregates
		var allSucceeded = results.All(r => r.Success);
		allSucceeded.ShouldBeTrue("All appends to different aggregates should succeed");
		_store.GetEventCount().ShouldBe(concurrency);
	}

	[Fact]
	public async Task ConcurrentAppends_MixedAggregates_CorrectConflictCounts()
	{
		// Arrange: 3 aggregate IDs, each targeted by 4 concurrent tasks
		var aggregateIds = Enumerable.Range(0, 3)
			.Select(_ => Guid.NewGuid().ToString())
			.ToArray();

		// Act
		var tasks = aggregateIds
			.SelectMany(id => Enumerable.Range(0, 4)
				.Select(_ => _store.AppendAsync(
					id,
					AggregateType,
					new IDomainEvent[] { CreateTestEvent(id) },
					expectedVersion: -1,
					CancellationToken.None).AsTask()))
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert: 3 successes (one per aggregate), 9 conflicts
		var successes = results.Count(r => r.Success);
		successes.ShouldBe(3, "One append per aggregate should succeed");

		// Verify each aggregate has exactly 1 event
		foreach (var id in aggregateIds)
		{
			var events = await _store.LoadAsync(id, AggregateType, CancellationToken.None)
				.ConfigureAwait(false);
			events.Count.ShouldBe(1, $"Aggregate {id} should have exactly 1 event");
		}
	}

	#endregion

	#region Concurrent Load + Append

	[Fact]
	public async Task ConcurrentLoad_DuringAppend_ReturnsConsistentState()
	{
		// Arrange: Seed initial events
		var aggregateId = Guid.NewGuid().ToString();
		var version = -1L;

		for (var i = 0; i < 5; i++)
		{
			var r = await _store.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { CreateTestEvent(aggregateId) },
				expectedVersion: version,
				CancellationToken.None).ConfigureAwait(false);

			version = r.NextExpectedVersion;
		}

		// Act: Concurrent loads while appending more events
		var appendTask = Task.Run(async () =>
		{
			for (var i = 0; i < 10; i++)
			{
				var r = await _store.AppendAsync(
					aggregateId,
					AggregateType,
					new IDomainEvent[] { CreateTestEvent(aggregateId) },
					expectedVersion: version,
					CancellationToken.None).ConfigureAwait(false);

				if (r.Success)
				{
					version = r.NextExpectedVersion;
				}
			}
		});

		var loadTasks = Enumerable.Range(0, 5)
			.Select(_ => _store.LoadAsync(aggregateId, AggregateType, CancellationToken.None).AsTask())
			.ToArray();

		await Task.WhenAll(loadTasks.Append(appendTask)).ConfigureAwait(false);

		// Assert: Each load should return a valid event list (no corruption)
		foreach (var loadResult in loadTasks)
		{
			var events = await loadResult.ConfigureAwait(false);
			events.Count.ShouldBeGreaterThanOrEqualTo(5,
				"Load during concurrent append should return at least the initial events");

			// Verify version ordering
			for (var i = 1; i < events.Count; i++)
			{
				events[i].Version.ShouldBeGreaterThan(events[i - 1].Version,
					"Events should be in ascending version order");
			}
		}
	}

	[Fact]
	public async Task ConcurrentLoadFromVersion_ReturnsSubsetCorrectly()
	{
		// Arrange: Seed 10 events
		var aggregateId = Guid.NewGuid().ToString();
		var version = -1L;

		for (var i = 0; i < 10; i++)
		{
			var r = await _store.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { CreateTestEvent(aggregateId) },
				expectedVersion: version,
				CancellationToken.None).ConfigureAwait(false);

			version = r.NextExpectedVersion;
		}

		// Act: Concurrent loads from different versions
		var loadTasks = Enumerable.Range(0, 10)
			.Select(i => _store.LoadAsync(
				aggregateId, AggregateType, fromVersion: i, CancellationToken.None).AsTask())
			.ToArray();

		var results = await Task.WhenAll(loadTasks).ConfigureAwait(false);

		// Assert: Each result should contain events with version > fromVersion
		// Events have versions 0..9. fromVersion is exclusive, so fromVersion=0 returns versions 1..9 (9 events).
		for (var from = 0; from < 10; from++)
		{
			var events = results[from];
			var expectedCount = 9 - from; // versions are 0..9, fromVersion is exclusive (version > from)
			events.Count.ShouldBe(expectedCount,
				$"LoadAsync(fromVersion={from}) should return {expectedCount} events");

			foreach (var e in events)
			{
				e.Version.ShouldBeGreaterThan(from,
					$"All events from LoadAsync(fromVersion={from}) should have version > {from}");
			}
		}
	}

	#endregion

	#region Concurrent Undispatched Events + MarkDispatched

	[Fact]
	public async Task ConcurrentMarkDispatched_AllEventsMarked()
	{
		// Arrange: Seed 10 events across 5 aggregates
		for (var i = 0; i < 5; i++)
		{
			var id = $"agg-{i}";
			await _store.AppendAsync(
				id,
				AggregateType,
				new IDomainEvent[]
				{
					CreateTestEvent(id),
					CreateTestEvent(id),
				},
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false);
		}

		var undispatched = await _store.GetUndispatchedEventsAsync(100, CancellationToken.None)
			.ConfigureAwait(false);
		undispatched.Count.ShouldBe(10, "Should have 10 undispatched events");

		// Act: Concurrently mark all as dispatched
		var markTasks = undispatched
			.Select(e => _store.MarkEventAsDispatchedAsync(e.EventId, CancellationToken.None).AsTask())
			.ToArray();

		await Task.WhenAll(markTasks).ConfigureAwait(false);

		// Assert
		var remaining = await _store.GetUndispatchedEventsAsync(100, CancellationToken.None)
			.ConfigureAwait(false);
		remaining.Count.ShouldBe(0, "All events should be marked as dispatched");
	}

	[Fact]
	public async Task ConcurrentAppendAndGetUndispatched_NoDuplicatesOrLoss()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();

		// Act: Append events and get undispatched concurrently
		var appendTasks = Enumerable.Range(0, 5)
			.Select(i => _store.AppendAsync(
				$"{aggregateId}-{i}",
				AggregateType,
				new IDomainEvent[] { CreateTestEvent($"{aggregateId}-{i}") },
				expectedVersion: -1,
				CancellationToken.None).AsTask())
			.ToArray();

		await Task.WhenAll(appendTasks).ConfigureAwait(false);

		// Get undispatched after all appends complete
		var undispatched = await _store.GetUndispatchedEventsAsync(100, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert: All events should be present
		undispatched.Count.ShouldBe(5, "All appended events should be undispatched");

		// Verify no duplicate event IDs
		var uniqueIds = undispatched.Select(e => e.EventId).Distinct().Count();
		uniqueIds.ShouldBe(5, "No duplicate event IDs should exist");
	}

	#endregion

	#region Version Ordering Under Concurrency

	[Fact]
	public async Task AppendResult_VersionMonotonicallyIncreases()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var version = -1L;

		// Act: Append 50 events sequentially
		for (var i = 0; i < 50; i++)
		{
			var result = await _store.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { CreateTestEvent(aggregateId) },
				expectedVersion: version,
				CancellationToken.None).ConfigureAwait(false);

			result.Success.ShouldBeTrue();
			result.NextExpectedVersion.ShouldBeGreaterThan(version,
				"NextExpectedVersion should increase after each append");
			version = result.NextExpectedVersion;
		}

		// Assert: Verify loaded events are in order
		var events = await _store.LoadAsync(aggregateId, AggregateType, CancellationToken.None)
			.ConfigureAwait(false);
		events.Count.ShouldBe(50);

		for (var i = 1; i < events.Count; i++)
		{
			events[i].Version.ShouldBe(events[i - 1].Version + 1,
				"Versions should be sequential");
		}
	}

	[Fact]
	public async Task BatchAppend_VersionIncreasesPerEvent()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var batch = Enumerable.Range(0, 10)
			.Select(_ => CreateTestEvent(aggregateId))
			.Cast<IDomainEvent>()
			.ToList();

		// Act
		var result = await _store.AppendAsync(
			aggregateId,
			AggregateType,
			batch,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(9, "10 events starting from -1 should end at version 9");

		var events = await _store.LoadAsync(aggregateId, AggregateType, CancellationToken.None)
			.ConfigureAwait(false);
		events.Count.ShouldBe(10);

		for (var i = 0; i < events.Count; i++)
		{
			events[i].Version.ShouldBe(i, $"Event {i} should have version {i}");
		}
	}

	#endregion

	#region Stress Tests

	[Fact]
	public async Task HighConcurrency_ManyAggregates_NoCorruption()
	{
		// Arrange: 50 aggregates, each getting 5 events sequentially across concurrent tasks
		var aggregateCount = 50;
		var eventsPerAggregate = 5;

		// Act
		var tasks = Enumerable.Range(0, aggregateCount)
			.Select(async i =>
			{
				var id = $"stress-agg-{i}";
				var version = -1L;

				for (var j = 0; j < eventsPerAggregate; j++)
				{
					var r = await _store.AppendAsync(
						id,
						AggregateType,
						new IDomainEvent[] { CreateTestEvent(id) },
						expectedVersion: version,
						CancellationToken.None).ConfigureAwait(false);

					r.Success.ShouldBeTrue($"Append to aggregate {id} at step {j} should succeed");
					version = r.NextExpectedVersion;
				}
			})
			.ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		_store.GetEventCount().ShouldBe(aggregateCount * eventsPerAggregate);

		// Verify each aggregate has correct event count and ordering
		for (var i = 0; i < aggregateCount; i++)
		{
			var id = $"stress-agg-{i}";
			var events = await _store.LoadAsync(id, AggregateType, CancellationToken.None)
				.ConfigureAwait(false);
			events.Count.ShouldBe(eventsPerAggregate,
				$"Aggregate {id} should have {eventsPerAggregate} events");

			for (var j = 1; j < events.Count; j++)
			{
				events[j].Version.ShouldBeGreaterThan(events[j - 1].Version,
					$"Events in {id} should be in ascending version order");
			}
		}
	}

	[Fact]
	public async Task Stress_ConcurrentClear_DoesNotCorruptState()
	{
		// Arrange: Seed some events
		var aggregateId = Guid.NewGuid().ToString();
		await _store.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act: Clear and re-append concurrently
		_store.Clear();

		// After clear, we should be able to append at version -1 again
		var result = await _store.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue("Append after clear should succeed at version -1");
		_store.GetEventCount().ShouldBe(1);
	}

	#endregion

	#region Concurrency Conflict Result Details

	[Fact]
	public async Task ConcurrencyConflict_ReportsCorrectExpectedAndActualVersions()
	{
		// Arrange: Create aggregate with 3 events
		var aggregateId = Guid.NewGuid().ToString();
		var version = -1L;

		for (var i = 0; i < 3; i++)
		{
			var r = await _store.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { CreateTestEvent(aggregateId) },
				expectedVersion: version,
				CancellationToken.None).ConfigureAwait(false);

			version = r.NextExpectedVersion;
		}

		// Current version is now 2

		// Act: Try to append at stale version 0
		var result = await _store.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { CreateTestEvent(aggregateId) },
			expectedVersion: 0,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(2,
			"NextExpectedVersion should report the actual current version");
		result.ErrorMessage.ShouldNotBeNullOrEmpty(
			"Concurrency conflict should include an error message");
	}

	[Fact]
	public async Task AppendWithWrongVersion_DoesNotModifyStore()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		await _store.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var eventCountBefore = _store.GetEventCount();

		// Act: Append with wrong version
		var result = await _store.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { CreateTestEvent(aggregateId) },
			expectedVersion: 99,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		_store.GetEventCount().ShouldBe(eventCountBefore,
			"Failed append should not add events to the store");
	}

	#endregion

	private static TestDomainEvent CreateTestEvent(string aggregateId) => new()
	{
		EventId = Guid.NewGuid().ToString(),
		AggregateId = aggregateId,
		OccurredAt = DateTimeOffset.UtcNow,
		Data = $"ConcurrencyTest-{Guid.NewGuid():N}",
	};
}
