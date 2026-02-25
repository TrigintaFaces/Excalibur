// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryEventStore" /> specific functionality.
/// </summary>
/// <remarks>
/// These tests cover InMemoryEventStore-specific methods not part of IEventStore.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class InMemoryEventStoreShould : UnitTestBase
{
	private readonly InMemoryEventStore _store;

	public InMemoryEventStoreShould()
	{
		_store = new InMemoryEventStore();
	}

	[Fact]
	public void Constructor_CreatesEmptyStore()
	{
		// Arrange & Act
		var store = new InMemoryEventStore();

		// Assert
		store.GetEventCount().ShouldBe(0);
	}

	[Fact]
	public async Task Clear_RemovesAllEvents()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[] { CreateTestEvent(aggregateId) };

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		_store.GetEventCount().ShouldBe(1);

		// Act
		_store.Clear();

		// Assert
		_store.GetEventCount().ShouldBe(0);
	}

	[Fact]
	public async Task Clear_AllowsNewAppendAfterClear()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[] { CreateTestEvent(aggregateId) };

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		_store.Clear();

		// Act
		var result = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		_store.GetEventCount().ShouldBe(1);
	}

	[Fact]
	public async Task GetEventCount_ReturnsCorrectCount()
	{
		// Arrange
		var aggregateId1 = Guid.NewGuid().ToString();
		var aggregateId2 = Guid.NewGuid().ToString();

		_ = await _store.AppendAsync(
			aggregateId1,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId1), CreateTestEvent(aggregateId1) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		_ = await _store.AppendAsync(
			aggregateId2,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId2) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var count = _store.GetEventCount();

		// Assert
		count.ShouldBe(3);
	}

	[Fact]
	public async Task GetUndispatchedEventCount_ReturnsCorrectCount()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// All events are undispatched initially
		_store.GetUndispatchedEventCount().ShouldBe(3);

		// Mark one as dispatched
		var undispatched = await _store.GetUndispatchedEventsAsync(1, CancellationToken.None)
			.ConfigureAwait(false);
		await _store.MarkEventAsDispatchedAsync(undispatched[0].EventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		var count = _store.GetUndispatchedEventCount();

		// Assert
		count.ShouldBe(2);
	}

	[Fact]
	public async Task GetUndispatchedEventCount_ReturnsZero_WhenAllDispatched()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[] { CreateTestEvent(aggregateId), CreateTestEvent(aggregateId) };

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var undispatched = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);

		foreach (var evt in undispatched)
		{
			await _store.MarkEventAsDispatchedAsync(evt.EventId, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Act
		var count = _store.GetUndispatchedEventCount();

		// Assert
		count.ShouldBe(0);
	}

	[Fact]
	public void GetEventCount_ReturnsZero_OnEmptyStore()
	{
		// Act
		var count = _store.GetEventCount();

		// Assert
		count.ShouldBe(0);
	}

	[Fact]
	public void GetUndispatchedEventCount_ReturnsZero_OnEmptyStore()
	{
		// Act
		var count = _store.GetUndispatchedEventCount();

		// Assert
		count.ShouldBe(0);
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_NonExistentEvent_NoOp()
	{
		// Arrange - nothing to arrange, store is empty

		// Act - should not throw
		await _store.MarkEventAsDispatchedAsync("non-existent-id", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - no exception thrown
		_store.GetEventCount().ShouldBe(0);
	}

	[Fact]
	public async Task LoadAsync_WithCancellationToken_ThrowsWhenCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await _store.LoadAsync("any-id", "TestAggregate", cts.Token).ConfigureAwait(false));
	}

	[Fact]
	public async Task AppendAsync_WithCancellationToken_ThrowsWhenCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		var events = new[] { CreateTestEvent("test") };

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await _store.AppendAsync("test", "TestAggregate", events, -1, cts.Token).ConfigureAwait(false));
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_WithCancellationToken_ThrowsWhenCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await _store.GetUndispatchedEventsAsync(10, cts.Token).ConfigureAwait(false));
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_WithCancellationToken_ThrowsWhenCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await _store.MarkEventAsDispatchedAsync("any-id", cts.Token).ConfigureAwait(false));
	}

	[Fact]
	public async Task LoadAsync_FromNegativeVersion_ReturnsAllEvents()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[] { CreateTestEvent(aggregateId), CreateTestEvent(aggregateId) };

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", fromVersion: -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(2);
	}

	[Fact]
	public async Task AppendAsync_WithMetadata_PreservesMetadata()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var eventWithMetadata = new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			Data = "TestData",
			Metadata = new Dictionary<string, object>
			{
				["key1"] = "value1",
				["key2"] = 42
			}
		};

		// Act
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new IDomainEvent[] { eventWithMetadata },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(1);
		_ = loaded[0].Metadata.ShouldNotBeNull();
	}

	#region Coverage gap: LoadAsync fromVersion edge cases (lines 115-121)

	[Fact]
	public async Task LoadAsync_FromVersion_EqualToLastVersion_ReturnsEmpty()
	{
		// Arrange - Append 3 events (versions 0, 1, 2)
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Load from version 2 (the last version); all events have version <= 2
		var loaded = await _store.LoadAsync(
			aggregateId,
			"TestAggregate",
			fromVersion: 2,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - No events with version > 2, so empty result
		loaded.ShouldBeEmpty();
	}

	[Fact]
	public async Task LoadAsync_FromVersion_SingleEvent_EqualToVersion_ReturnsEmpty()
	{
		// Arrange - Append a single event (version 0)
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[] { CreateTestEvent(aggregateId) };

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Load from version 0; the only event has version 0 which is not > 0
		var loaded = await _store.LoadAsync(
			aggregateId,
			"TestAggregate",
			fromVersion: 0,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Single event at version 0 is not > fromVersion 0
		loaded.ShouldBeEmpty();
	}

	[Fact]
	public async Task LoadAsync_FromVersion_ReturnsPartialResults_WhenSomeEventsMatch()
	{
		// Arrange - Append 5 events (versions 0, 1, 2, 3, 4)
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Load from version 3 (should get version 4 only, since version > 3)
		var loaded = await _store.LoadAsync(
			aggregateId,
			"TestAggregate",
			fromVersion: 3,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(1);
		loaded[0].Version.ShouldBe(4);
	}

	#endregion Coverage gap: LoadAsync fromVersion edge cases (lines 115-121)

	#region Coverage gap: GetUndispatchedEventsAsync Array.Resize path (lines 262-266)

	[Fact]
	public async Task GetUndispatchedEventsAsync_WhenUndispatchedExceedsPreAllocatedArray_ResizesAndReturnsCorrectBatch()
	{
		// Arrange - Create events across multiple aggregates so _eventsById has many entries.
		// Use a small batchSize so that Math.Min(batchSize, _eventsById.Count) is small,
		// but the actual undispatched count exceeds the pre-allocated array size.
		// With batchSize=2 and 10 total events (all undispatched):
		//   pre-allocated = Math.Min(2, 10) = 2
		//   The loop finds 10 undispatched events, exceeding the 2-slot array -> triggers Array.Resize
		for (var i = 0; i < 10; i++)
		{
			var aggregateId = Guid.NewGuid().ToString();
			_ = await _store.AppendAsync(
				aggregateId,
				"TestAggregate",
				new[] { CreateTestEvent(aggregateId) },
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false);
		}

		_store.GetEventCount().ShouldBe(10);
		_store.GetUndispatchedEventCount().ShouldBe(10);

		// Act - Use a batch size smaller than total undispatched events to trigger the resize path
		var undispatched = await _store.GetUndispatchedEventsAsync(
			2,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Should still respect batchSize even after resize
		undispatched.Count.ShouldBe(2);
		undispatched.ShouldAllBe(e => !e.IsDispatched);
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_WhenUndispatchedExceedsPreAllocatedArray_ReturnsEventsOrderedByTimestamp()
	{
		// Arrange - Create events with different timestamps to verify sort after resize
		for (var i = 0; i < 8; i++)
		{
			var aggregateId = Guid.NewGuid().ToString();
			_ = await _store.AppendAsync(
				aggregateId,
				"TestAggregate",
				new[] { CreateTestEvent(aggregateId) },
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false);
		}

		// Act - batchSize=3, total=8, pre-allocated=3, triggers resize at the 4th undispatched event
		var undispatched = await _store.GetUndispatchedEventsAsync(
			3,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Events should be sorted by timestamp
		undispatched.Count.ShouldBe(3);
		for (var i = 0; i < undispatched.Count - 1; i++)
		{
			undispatched[i].Timestamp.ShouldBeLessThanOrEqualTo(undispatched[i + 1].Timestamp);
		}
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_LargeBatchSize_ReturnsAllUndispatched()
	{
		// Arrange - Batch size larger than total events means pre-allocated = _eventsById.Count
		// No resize should occur, but this verifies the non-resize path works correctly
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - batchSize=100 > 3 total events, so pre-allocated=3, no resize needed
		var undispatched = await _store.GetUndispatchedEventsAsync(
			100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		undispatched.Count.ShouldBe(3);
	}

	#endregion Coverage gap: GetUndispatchedEventsAsync Array.Resize path (lines 262-266)

	#region Coverage gap: AppendAsync with IEnumerable (not IReadOnlyCollection) input

	[Fact]
	public async Task AppendAsync_WithLazyEnumerable_MaterializesAndAppendsCorrectly()
	{
		// Arrange - Pass a lazy IEnumerable (not IReadOnlyCollection) to exercise
		// the materialization path at line 155: events.ToList()
		var aggregateId = Guid.NewGuid().ToString();
		IEnumerable<IDomainEvent> LazyEvents()
		{
			yield return CreateTestEvent(aggregateId);
			yield return CreateTestEvent(aggregateId);
		}

		// Act
		var result = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			LazyEvents(),
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		_store.GetEventCount().ShouldBe(2);

		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);
		loaded.Count.ShouldBe(2);
	}

	#endregion Coverage gap: AppendAsync with IEnumerable (not IReadOnlyCollection) input

	#region Coverage gap: AppendAsync with null metadata

	[Fact]
	public async Task AppendAsync_WithNullMetadata_StoresEventWithNullMetadata()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var eventWithoutMetadata = new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			Data = "TestData",
			Metadata = null
		};

		// Act
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new IDomainEvent[] { eventWithoutMetadata },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(1);
		loaded[0].Metadata.ShouldBeNull();
	}

	#endregion Coverage gap: AppendAsync with null metadata

	#region Coverage gap: LoadAsync overload without fromVersion delegates to fromVersion=-1

	[Fact]
	public async Task LoadAsync_WithoutFromVersion_DelegatesToFromVersionNegativeOne()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Call overload without fromVersion (line 66: delegates to LoadAsync with -1)
		var loaded = await _store.LoadAsync(
			aggregateId,
			"TestAggregate",
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Should return all events (same as fromVersion: -1)
		loaded.Count.ShouldBe(3);
		loaded[0].Version.ShouldBe(0);
		loaded[1].Version.ShouldBe(1);
		loaded[2].Version.ShouldBe(2);
	}

	#endregion Coverage gap: LoadAsync overload without fromVersion delegates to fromVersion=-1

	#region Coverage gap: GetUndispatchedEventsAsync with mix of dispatched and undispatched

	[Fact]
	public async Task GetUndispatchedEventsAsync_WithMixedDispatchStatus_OnlyReturnsUndispatched()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Mark the first two events as dispatched
		var allUndispatched = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);

		await _store.MarkEventAsDispatchedAsync(allUndispatched[0].EventId, CancellationToken.None)
			.ConfigureAwait(false);
		await _store.MarkEventAsDispatchedAsync(allUndispatched[1].EventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		var remaining = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - Only 3 undispatched should remain
		remaining.Count.ShouldBe(3);
		remaining.ShouldAllBe(e => !e.IsDispatched);
	}

	#endregion Coverage gap: GetUndispatchedEventsAsync with mix of dispatched and undispatched

	#region Coverage gap: AppendAsync position tracking

	[Fact]
	public async Task AppendAsync_MultipleEvents_ReturnsCorrectFirstEventPosition()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		// Act
		var result = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.FirstEventPosition.ShouldBeGreaterThan(0);
		result.NextExpectedVersion.ShouldBe(2);
	}

	[Fact]
	public async Task AppendAsync_SequentialAppends_PositionsAreMonotonicallyIncreasing()
	{
		// Arrange
		var aggregateId1 = Guid.NewGuid().ToString();
		var aggregateId2 = Guid.NewGuid().ToString();

		// Act
		var result1 = await _store.AppendAsync(
			aggregateId1,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId1) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var result2 = await _store.AppendAsync(
			aggregateId2,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId2) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Positions should be monotonically increasing across aggregates
		result1.Success.ShouldBeTrue();
		result2.Success.ShouldBeTrue();
		result2.FirstEventPosition.ShouldBeGreaterThan(result1.FirstEventPosition);
	}

	#endregion Coverage gap: AppendAsync position tracking

	#region Coverage gap: LoadAsync with fromVersion on specific boundary

	[Fact]
	public async Task LoadAsync_FromVersion_OneBeforeLastVersion_ReturnsSingleEvent()
	{
		// Arrange - Append 3 events (versions 0, 1, 2)
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - fromVersion 1 should return only version 2
		var loaded = await _store.LoadAsync(
			aggregateId,
			"TestAggregate",
			fromVersion: 1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(1);
		loaded[0].Version.ShouldBe(2);
	}

	#endregion Coverage gap: LoadAsync with fromVersion on specific boundary

	#region Coverage gap: MarkEventAsDispatchedAsync updates both dictionaries

	[Fact]
	public async Task MarkEventAsDispatchedAsync_UpdatesEventInLoadedStream()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var undispatched = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);
		var eventId = undispatched[0].EventId;

		// Act - Mark as dispatched
		await _store.MarkEventAsDispatchedAsync(eventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - Event should still be loadable via LoadAsync and show IsDispatched = true
		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);
		loaded.Count.ShouldBe(1);
		loaded[0].IsDispatched.ShouldBeTrue();
	}

	#endregion Coverage gap: MarkEventAsDispatchedAsync updates both dictionaries

	#region Coverage gap: LoadAsync with fromVersion on non-existent aggregate

	[Fact]
	public async Task LoadAsync_WithFromVersion_NonExistentAggregate_ReturnsEmpty()
	{
		// Arrange - no events stored for this aggregate

		// Act - Load with specific fromVersion for an aggregate that doesn't exist
		var loaded = await _store.LoadAsync(
			"non-existent-aggregate",
			"TestAggregate",
			fromVersion: 5,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Should return empty (hits the TryGetValue false branch at line 83)
		loaded.ShouldBeEmpty();
	}

	#endregion Coverage gap: LoadAsync with fromVersion on non-existent aggregate

	#region Coverage gap: LoadAsync fromVersion = 0 with first event at version 0

	[Fact]
	public async Task LoadAsync_FromVersionZero_WithMultipleEvents_ReturnsEventsAfterVersionZero()
	{
		// Arrange - Append 4 events (versions 0, 1, 2, 3)
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - fromVersion 0 means "events with version > 0"
		var loaded = await _store.LoadAsync(
			aggregateId,
			"TestAggregate",
			fromVersion: 0,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Should return versions 1, 2, 3
		loaded.Count.ShouldBe(3);
		loaded[0].Version.ShouldBe(1);
		loaded[1].Version.ShouldBe(2);
		loaded[2].Version.ShouldBe(3);
	}

	#endregion Coverage gap: LoadAsync fromVersion = 0 with first event at version 0

	#region Coverage gap: AppendAsync with IReadOnlyCollection bypasses ToList

	[Fact]
	public async Task AppendAsync_WithReadOnlyCollection_BypassesToListAndAppendsCorrectly()
	{
		// Arrange - Pass an IReadOnlyCollection to exercise the "as IReadOnlyCollection" path
		// at line 155, which bypasses the ToList() materialization
		var aggregateId = Guid.NewGuid().ToString();
		IReadOnlyCollection<IDomainEvent> readOnlyEvents = new List<IDomainEvent>
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		}.AsReadOnly();

		// Act
		var result = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			readOnlyEvents,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		_store.GetEventCount().ShouldBe(2);
	}

	#endregion Coverage gap: AppendAsync with IReadOnlyCollection bypasses ToList

	#region Coverage gap: AppendAsync empty lazy enumerable

	[Fact]
	public async Task AppendAsync_WithEmptyLazyEnumerable_ReturnsSuccessWithNoEvents()
	{
		// Arrange - Pass an empty lazy IEnumerable to exercise the materialization
		// path followed by the count == 0 early return at line 157-161
		var aggregateId = Guid.NewGuid().ToString();
		IEnumerable<IDomainEvent> EmptyLazy()
		{
			yield break;
		}

		// Act
		var result = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			EmptyLazy(),
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(-1);
		_store.GetEventCount().ShouldBe(0);
	}

	#endregion Coverage gap: AppendAsync empty lazy enumerable

	#region Coverage gap: GetUndispatchedEventsAsync when all events are dispatched

	[Fact]
	public async Task GetUndispatchedEventsAsync_WhenAllEventsDispatched_ReturnsEmpty()
	{
		// Arrange - Add events and dispatch them all
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Dispatch all events
		var allEvents = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);
		foreach (var evt in allEvents)
		{
			await _store.MarkEventAsDispatchedAsync(evt.EventId, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Act - All dispatched, so the count == 0 path at line 271 should be hit
		var undispatched = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		undispatched.ShouldBeEmpty();
	}

	#endregion Coverage gap: GetUndispatchedEventsAsync when all events are dispatched

	#region Coverage gap: GetUndispatchedEventsAsync on empty store

	[Fact]
	public async Task GetUndispatchedEventsAsync_OnEmptyStore_ReturnsEmpty()
	{
		// Arrange - store is empty

		// Act - batchSize doesn't matter, store has no events
		var undispatched = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		undispatched.ShouldBeEmpty();
	}

	#endregion Coverage gap: GetUndispatchedEventsAsync on empty store

	#region Coverage gap: MarkEventAsDispatchedAsync idempotency

	[Fact]
	public async Task MarkEventAsDispatchedAsync_CalledTwice_IsIdempotent()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var undispatched = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);
		var eventId = undispatched[0].EventId;

		// Act - Mark as dispatched twice
		await _store.MarkEventAsDispatchedAsync(eventId, CancellationToken.None)
			.ConfigureAwait(false);
		await _store.MarkEventAsDispatchedAsync(eventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - Should still be dispatched, no errors
		_store.GetUndispatchedEventCount().ShouldBe(0);
		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);
		loaded[0].IsDispatched.ShouldBeTrue();
	}

	#endregion Coverage gap: MarkEventAsDispatchedAsync idempotency

	#region Coverage gap: AppendAsync concurrency conflict returns correct actual version

	[Fact]
	public async Task AppendAsync_ConcurrencyConflict_ReturnsActualVersion()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId), CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Attempt to append with wrong expected version (expect -1, actual is 1)
		var result = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - ConcurrencyConflict with actual version in NextExpectedVersion
		result.Success.ShouldBeFalse();
		result.NextExpectedVersion.ShouldBe(1); // Actual current version
	}

	#endregion Coverage gap: AppendAsync concurrency conflict returns correct actual version

	#region Coverage gap: LoadAsync with fromVersion greater than all versions

	[Fact]
	public async Task LoadAsync_FromVersion_GreaterThanAllVersions_ReturnsEmpty()
	{
		// Arrange - Append 2 events (versions 0, 1)
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Load from version 100 (far beyond any existing version)
		var loaded = await _store.LoadAsync(
			aggregateId,
			"TestAggregate",
			fromVersion: 100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - All events have version <= 100, so empty result
		loaded.ShouldBeEmpty();
	}

	#endregion Coverage gap: LoadAsync with fromVersion greater than all versions

	#region Coverage gap: Multiple aggregates with different types

	[Fact]
	public async Task LoadAsync_DifferentAggregateTypes_IsolatesEvents()
	{
		// Arrange - Same aggregate ID but different types
		var aggregateId = Guid.NewGuid().ToString();

		_ = await _store.AppendAsync(
			aggregateId,
			"TypeA",
			new[] { CreateTestEvent(aggregateId), CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		_ = await _store.AppendAsync(
			aggregateId,
			"TypeB",
			new[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var typeAEvents = await _store.LoadAsync(aggregateId, "TypeA", CancellationToken.None)
			.ConfigureAwait(false);
		var typeBEvents = await _store.LoadAsync(aggregateId, "TypeB", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		typeAEvents.Count.ShouldBe(2);
		typeBEvents.Count.ShouldBe(1);
		_store.GetEventCount().ShouldBe(3);
	}

	#endregion Coverage gap: Multiple aggregates with different types

	#region Coverage gap: GetUndispatchedEventsAsync multiple resizes

	[Fact]
	public async Task GetUndispatchedEventsAsync_MultipleResizes_HandlesCorrectly()
	{
		// Arrange - Create enough events across many aggregates to trigger multiple
		// Array.Resize calls. With batchSize=1, pre-allocated = Math.Min(1, N) = 1,
		// so first undispatched fills slot 0, second triggers resize to 2, third to 4, etc.
		for (var i = 0; i < 20; i++)
		{
			var aggregateId = Guid.NewGuid().ToString();
			_ = await _store.AppendAsync(
				aggregateId,
				"TestAggregate",
				new[] { CreateTestEvent(aggregateId) },
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false);
		}

		// Act - Use batchSize=5 but 20 undispatched events with pre-allocated=5
		// Forces multiple resizes: 5 -> 10 -> 20
		var undispatched = await _store.GetUndispatchedEventsAsync(
			5,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		undispatched.Count.ShouldBe(5);
		// Verify timestamp ordering
		for (var i = 0; i < undispatched.Count - 1; i++)
		{
			undispatched[i].Timestamp.ShouldBeLessThanOrEqualTo(undispatched[i + 1].Timestamp);
		}
	}

	#endregion Coverage gap: GetUndispatchedEventsAsync multiple resizes

	#region Coverage gap: AppendAsync stores correct EventType name

	[Fact]
	public async Task AppendAsync_StoresCorrectEventTypeName()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var testEvent = CreateTestEvent(aggregateId);

		// Act
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new IDomainEvent[] { testEvent },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - EventType should be set from EventTypeNameHelper
		loaded.Count.ShouldBe(1);
		loaded[0].EventType.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion Coverage gap: AppendAsync stores correct EventType name

	#region Coverage gap: AppendAsync and LoadAsync with fromVersion boundary - version equals fromVersion exactly

	[Fact]
	public async Task LoadAsync_FromVersion_EqualsMiddleVersion_ReturnsRemainingEvents()
	{
		// Arrange - Append 6 events (versions 0 through 5)
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - fromVersion 2 means events with version > 2, i.e., 3, 4, 5
		var loaded = await _store.LoadAsync(
			aggregateId,
			"TestAggregate",
			fromVersion: 2,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(3);
		loaded[0].Version.ShouldBe(3);
		loaded[1].Version.ShouldBe(4);
		loaded[2].Version.ShouldBe(5);
	}

	#endregion Coverage gap: AppendAsync and LoadAsync with fromVersion boundary - version equals fromVersion exactly

	#region Coverage gap: Clear resets position counter

	[Fact]
	public async Task Clear_ResetsPositionCounter_AllowingFreshAppends()
	{
		// Arrange - Append some events to advance position counter
		var aggregateId = Guid.NewGuid().ToString();
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId), CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Clear and append fresh events
		_store.Clear();

		var newAggregateId = Guid.NewGuid().ToString();
		var result = await _store.AppendAsync(
			newAggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(newAggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Position should start from 1 again after clear
		result.Success.ShouldBeTrue();
		result.FirstEventPosition.ShouldBe(1);
	}

	#endregion Coverage gap: Clear resets position counter

	#region Coverage gap: MarkEventAsDispatchedAsync with multiple events in same aggregate

	[Fact]
	public async Task MarkEventAsDispatchedAsync_MarkMiddleEvent_OnlyThatEventIsDispatched()
	{
		// Arrange - Append 3 events
		var aggregateId = Guid.NewGuid().ToString();
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[]
			{
				CreateTestEvent(aggregateId),
				CreateTestEvent(aggregateId),
				CreateTestEvent(aggregateId)
			},
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Get the middle event
		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);
		var middleEventId = loaded[1].EventId;

		// Act - Mark only the middle event
		await _store.MarkEventAsDispatchedAsync(middleEventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var reloaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);
		reloaded[0].IsDispatched.ShouldBeFalse();
		reloaded[1].IsDispatched.ShouldBeTrue();
		reloaded[2].IsDispatched.ShouldBeFalse();
		_store.GetUndispatchedEventCount().ShouldBe(2);
	}

	#endregion Coverage gap: MarkEventAsDispatchedAsync with multiple events in same aggregate

	#region Coverage gap: AppendAsync preserves event data across serialization

	[Fact]
	public async Task AppendAsync_PreservesEventDataInStoredEvent()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var testEvent = new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			Data = "ImportantData"
		};

		// Act
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new IDomainEvent[] { testEvent },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - EventData should be non-null serialized bytes
		loaded.Count.ShouldBe(1);
		loaded[0].EventData.ShouldNotBeNull();
		loaded[0].EventData.Length.ShouldBeGreaterThan(0);
		loaded[0].AggregateId.ShouldBe(aggregateId);
		loaded[0].AggregateType.ShouldBe("TestAggregate");
	}

	#endregion Coverage gap: AppendAsync preserves event data across serialization

	#region Coverage gap: AppendAsync with single event returns correct version

	[Fact]
	public async Task AppendAsync_SingleEvent_ReturnsVersionZero()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();

		// Act
		var result = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(0);
		result.FirstEventPosition.ShouldBe(1);
	}

	#endregion Coverage gap: AppendAsync with single event returns correct version

	#region Coverage gap: LoadAsync with fromVersion cancellation

	[Fact]
	public async Task LoadAsync_WithFromVersion_CancelledToken_Throws()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert - The overload with fromVersion should also respect cancellation
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await _store.LoadAsync("any-id", "TestAggregate", fromVersion: 5, cts.Token)
				.ConfigureAwait(false));
	}

	#endregion Coverage gap: LoadAsync with fromVersion cancellation

	#region Coverage gap: GetUndispatchedEventsAsync batch exactly equals undispatched count

	[Fact]
	public async Task GetUndispatchedEventsAsync_BatchSizeEqualsUndispatchedCount_ReturnsAll()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[]
			{
				CreateTestEvent(aggregateId),
				CreateTestEvent(aggregateId),
				CreateTestEvent(aggregateId)
			},
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - batchSize exactly equals total undispatched
		var undispatched = await _store.GetUndispatchedEventsAsync(3, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		undispatched.Count.ShouldBe(3);
	}

	#endregion Coverage gap: GetUndispatchedEventsAsync batch exactly equals undispatched count

	#region Coverage gap: AppendAsync with metadata containing complex types

	[Fact]
	public async Task AppendAsync_WithMetadataContainingVariousTypes_SerializesCorrectly()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var eventWithMetadata = new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			Data = "TestData",
			Metadata = new Dictionary<string, object>
			{
				["stringKey"] = "stringValue",
				["intKey"] = 123,
				["boolKey"] = true,
				["doubleKey"] = 3.14
			}
		};

		// Act
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new IDomainEvent[] { eventWithMetadata },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(1);
		loaded[0].Metadata.ShouldNotBeNull();
		loaded[0].Metadata.Length.ShouldBeGreaterThan(0);
	}

	#endregion Coverage gap: AppendAsync with metadata containing complex types

	#region Coverage gap: Timestamp preservation in stored events

	[Fact]
	public async Task AppendAsync_PreservesTimestampFromDomainEvent()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var specificTime = new DateTime(2025, 6, 15, 12, 30, 0, DateTimeKind.Utc);
		var testEvent = new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			OccurredAt = specificTime,
			Data = "TimestampTest"
		};

		// Act
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new IDomainEvent[] { testEvent },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - Timestamp should be preserved from OccurredAt
		loaded.Count.ShouldBe(1);
		loaded[0].Timestamp.ShouldBe(new DateTimeOffset(specificTime, TimeSpan.Zero));
	}

	#endregion Coverage gap: Timestamp preservation in stored events

	#region Coverage gap: EventId preservation in stored events

	[Fact]
	public async Task AppendAsync_PreservesEventIdFromDomainEvent()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var expectedEventId = Guid.NewGuid().ToString();
		var testEvent = new TestDomainEvent
		{
			EventId = expectedEventId,
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			Data = "EventIdTest"
		};

		// Act
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new IDomainEvent[] { testEvent },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(1);
		loaded[0].EventId.ShouldBe(expectedEventId);
	}

	#endregion Coverage gap: EventId preservation in stored events

	#region Coverage gap: Multiple sequential appends to same aggregate

	[Fact]
	public async Task AppendAsync_MultipleSequentialAppends_MaintainsVersionSequence()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();

		// Act - Three sequential appends
		var result1 = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var result2 = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId) },
			expectedVersion: 0,
			CancellationToken.None).ConfigureAwait(false);

		var result3 = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId), CreateTestEvent(aggregateId) },
			expectedVersion: 1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result1.Success.ShouldBeTrue();
		result1.NextExpectedVersion.ShouldBe(0);
		result2.Success.ShouldBeTrue();
		result2.NextExpectedVersion.ShouldBe(1);
		result3.Success.ShouldBeTrue();
		result3.NextExpectedVersion.ShouldBe(3);

		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);
		loaded.Count.ShouldBe(4);
		loaded[0].Version.ShouldBe(0);
		loaded[1].Version.ShouldBe(1);
		loaded[2].Version.ShouldBe(2);
		loaded[3].Version.ShouldBe(3);
	}

	#endregion Coverage gap: Multiple sequential appends to same aggregate

	#region Coverage gap: GetUndispatchedEventsAsync with dispatched events mixed across aggregates

	[Fact]
	public async Task GetUndispatchedEventsAsync_DispatchedAndUndispatchedAcrossAggregates_FiltersCorrectly()
	{
		// Arrange - Two aggregates, dispatch some events from each
		var aggId1 = Guid.NewGuid().ToString();
		var aggId2 = Guid.NewGuid().ToString();

		_ = await _store.AppendAsync(
			aggId1,
			"TestAggregate",
			new[] { CreateTestEvent(aggId1), CreateTestEvent(aggId1) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		_ = await _store.AppendAsync(
			aggId2,
			"TestAggregate",
			new[] { CreateTestEvent(aggId2), CreateTestEvent(aggId2) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Dispatch one event from each aggregate
		var all = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);

		// Find one event from each aggregate and dispatch
		var fromAgg1 = all.First(e => e.AggregateId == aggId1);
		var fromAgg2 = all.First(e => e.AggregateId == aggId2);

		await _store.MarkEventAsDispatchedAsync(fromAgg1.EventId, CancellationToken.None)
			.ConfigureAwait(false);
		await _store.MarkEventAsDispatchedAsync(fromAgg2.EventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		var remaining = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		remaining.Count.ShouldBe(2);
		remaining.ShouldAllBe(e => !e.IsDispatched);
	}

	#endregion Coverage gap: GetUndispatchedEventsAsync with dispatched events mixed across aggregates

	#region Coverage gap: Observability with ActivitySource active

	[Fact]
	public async Task LoadAsync_WithActivityListener_ExercisesTracingCodePath()
	{
		// Arrange - Add an activity listener to exercise the tracing code paths
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.Contains("EventSourcing", StringComparison.Ordinal),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = _ => { },
			ActivityStopped = _ => { }
		};
		ActivitySource.AddActivityListener(listener);

		var aggregateId = Guid.NewGuid().ToString();
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId), CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Load with activity tracing active
		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(2);
	}

	[Fact]
	public async Task AppendAsync_WithActivityListener_ExercisesTracingCodePath()
	{
		// Arrange - Add an activity listener
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.Contains("EventSourcing", StringComparison.Ordinal),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = _ => { },
			ActivityStopped = _ => { }
		};
		ActivitySource.AddActivityListener(listener);

		var aggregateId = Guid.NewGuid().ToString();

		// Act - Append with activity tracing active
		var result = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_WithActivityListener_ExercisesTracingCodePath()
	{
		// Arrange - Add an activity listener
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.Contains("EventSourcing", StringComparison.Ordinal),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = _ => { },
			ActivityStopped = _ => { }
		};
		ActivitySource.AddActivityListener(listener);

		var aggregateId = Guid.NewGuid().ToString();
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var undispatched = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		undispatched.Count.ShouldBe(1);
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_WithActivityListener_ExercisesTracingCodePath()
	{
		// Arrange - Add an activity listener
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.Contains("EventSourcing", StringComparison.Ordinal),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = _ => { },
			ActivityStopped = _ => { }
		};
		ActivitySource.AddActivityListener(listener);

		var aggregateId = Guid.NewGuid().ToString();
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var undispatched = await _store.GetUndispatchedEventsAsync(10, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		await _store.MarkEventAsDispatchedAsync(undispatched[0].EventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_store.GetUndispatchedEventCount().ShouldBe(0);
	}

	#endregion Coverage gap: Observability with ActivitySource active

	#region Coverage gap: LoadAsync non-existent aggregate hit first branch

	[Fact]
	public async Task LoadAsync_NonExistentAggregate_ReturnsEmptyWithoutFromVersion()
	{
		// Arrange - Store is empty, no such aggregate exists

		// Act - Call the overload without fromVersion for non-existent aggregate
		var loaded = await _store.LoadAsync(
			"does-not-exist",
			"NonExistentType",
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Should hit line 83-88 TryGetValue false branch
		loaded.ShouldBeEmpty();
	}

	#endregion Coverage gap: LoadAsync non-existent aggregate hit first branch

	#region Coverage gap: LoadAsync fromVersion with 2 events returns last

	[Fact]
	public async Task LoadAsync_FromVersion_TwoEvents_FromVersionZero_ReturnsSecondOnly()
	{
		// Arrange - Append exactly 2 events (versions 0, 1)
		var aggregateId = Guid.NewGuid().ToString();
		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { CreateTestEvent(aggregateId), CreateTestEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - fromVersion 0 should return only version 1
		var loaded = await _store.LoadAsync(
			aggregateId,
			"TestAggregate",
			fromVersion: 0,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(1);
		loaded[0].Version.ShouldBe(1);
	}

	#endregion Coverage gap: LoadAsync fromVersion with 2 events returns last

	#region Coverage gap: Empty array path in AppendAsync

	[Fact]
	public async Task AppendAsync_EmptyArray_ReturnsSuccess()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var emptyArray = Array.Empty<IDomainEvent>();

		// Act
		var result = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			emptyArray,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Should hit early return at line 158-160
		result.Success.ShouldBeTrue();
		result.FirstEventPosition.ShouldBe(0);
		result.NextExpectedVersion.ShouldBe(-1);
	}

	#endregion Coverage gap: Empty array path in AppendAsync

	#region Coverage gap: LoadAsync fromVersion finds event mid-loop

	[Fact]
	public async Task LoadAsync_FromVersion_FindsEventInMiddleOfLoop()
	{
		// Arrange - Append 10 events (versions 0-9)
		var aggregateId = Guid.NewGuid().ToString();
		var events = Enumerable.Range(0, 10).Select(_ => CreateTestEvent(aggregateId)).ToArray();

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - fromVersion 4 should find first match at index 5 (version 5)
		var loaded = await _store.LoadAsync(
			aggregateId,
			"TestAggregate",
			fromVersion: 4,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Should return 5 events (versions 5-9)
		loaded.Count.ShouldBe(5);
		loaded[0].Version.ShouldBe(5);
		loaded[4].Version.ShouldBe(9);
	}

	#endregion Coverage gap: LoadAsync fromVersion finds event mid-loop

	#region Coverage gap: GetUndispatchedEventsAsync when pre-allocated fits exactly

	[Fact]
	public async Task GetUndispatchedEventsAsync_PreAllocatedFitsExactly_NoResizeNeeded()
	{
		// Arrange - Add exactly as many events as batchSize
		// With batchSize=5 and 5 events, pre-allocated = Math.Min(5, 5) = 5
		// All 5 undispatched events fit without resize
		var aggregateId = Guid.NewGuid().ToString();
		var events = Enumerable.Range(0, 5).Select(_ => CreateTestEvent(aggregateId)).ToArray();

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var undispatched = await _store.GetUndispatchedEventsAsync(5, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		undispatched.Count.ShouldBe(5);
	}

	#endregion Coverage gap: GetUndispatchedEventsAsync when pre-allocated fits exactly

	#region Coverage gap: MarkEventAsDispatchedAsync updates aggregate list correctly

	[Fact]
	public async Task MarkEventAsDispatchedAsync_UpdatesEventAtCorrectIndexInAggregateList()
	{
		// Arrange - Create 5 events to ensure FindIndex works on middle elements
		var aggregateId = Guid.NewGuid().ToString();
		var events = Enumerable.Range(0, 5).Select(_ => CreateTestEvent(aggregateId)).ToArray();

		_ = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Get the event at index 2 (middle)
		var loaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);
		var targetEventId = loaded[2].EventId;

		// Act - Mark the middle event
		await _store.MarkEventAsDispatchedAsync(targetEventId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - Reload and verify only index 2 is dispatched
		var reloaded = await _store.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		reloaded[0].IsDispatched.ShouldBeFalse();
		reloaded[1].IsDispatched.ShouldBeFalse();
		reloaded[2].IsDispatched.ShouldBeTrue();
		reloaded[3].IsDispatched.ShouldBeFalse();
		reloaded[4].IsDispatched.ShouldBeFalse();
	}

	#endregion Coverage gap: MarkEventAsDispatchedAsync updates aggregate list correctly

	#region Coverage gap: AppendAsync firstPosition is set correctly for first event

	[Fact]
	public async Task AppendAsync_FirstPosition_IsSetOnFirstEvent()
	{
		// Arrange - This tests that firstPosition is correctly set to the first event's position
		var aggregateId = Guid.NewGuid().ToString();
		var events = new[]
		{
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId),
			CreateTestEvent(aggregateId)
		};

		// Act
		var result = await _store.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - FirstEventPosition should be set from the first event in the batch
		result.Success.ShouldBeTrue();
		result.FirstEventPosition.ShouldBe(1); // First ever append starts at position 1
	}

	#endregion Coverage gap: AppendAsync firstPosition is set correctly for first event

	private static TestDomainEvent CreateTestEvent(string aggregateId) => new()
	{
		EventId = Guid.NewGuid().ToString(),
		AggregateId = aggregateId,
		OccurredAt = DateTimeOffset.UtcNow,
		Data = $"TestData-{Guid.NewGuid():N}"
	};
}
