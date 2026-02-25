// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Tests.InMemory;

/// <summary>
/// Tests for <see cref="InMemoryEventStore"/> IEventStoreErasure implementation (GDPR erasure).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryEventStoreErasureShould
{
	private readonly InMemoryEventStore _store = new();

	[Fact]
	public async Task EraseEventsAsync_ReturnsCountOfErasedEvents()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		await _store.AppendAsync(aggregateId, "Order", CreateEvents(aggregateId, 3), -1, CancellationToken.None);

		// Act
		var erasedCount = await ((IEventStoreErasure)_store).EraseEventsAsync(
			aggregateId, "Order", Guid.NewGuid(), CancellationToken.None);

		// Assert
		erasedCount.ShouldBe(3);
	}

	[Fact]
	public async Task EraseEventsAsync_TombstonesAllEventsForAggregate()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		await _store.AppendAsync(aggregateId, "Order", CreateEvents(aggregateId, 2), -1, CancellationToken.None);

		// Act
		await ((IEventStoreErasure)_store).EraseEventsAsync(
			aggregateId, "Order", Guid.NewGuid(), CancellationToken.None);

		// Assert - events should be tombstoned
		var loaded = await _store.LoadAsync(aggregateId, "Order", CancellationToken.None);
		loaded.Count.ShouldBe(2);
		loaded.ShouldAllBe(e => e.EventType == "$erased");
		loaded.ShouldAllBe(e => e.Metadata == null);
	}

	[Fact]
	public async Task EraseEventsAsync_ReturnsZeroForNonExistentAggregate()
	{
		// Act
		var erasedCount = await ((IEventStoreErasure)_store).EraseEventsAsync(
			"non-existent", "Order", Guid.NewGuid(), CancellationToken.None);

		// Assert
		erasedCount.ShouldBe(0);
	}

	[Fact]
	public async Task IsErasedAsync_ReturnsTrueAfterErasure()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		await _store.AppendAsync(aggregateId, "Order", CreateEvents(aggregateId, 1), -1, CancellationToken.None);
		await ((IEventStoreErasure)_store).EraseEventsAsync(
			aggregateId, "Order", Guid.NewGuid(), CancellationToken.None);

		// Act
		var isErased = await ((IEventStoreErasure)_store).IsErasedAsync(
			aggregateId, "Order", CancellationToken.None);

		// Assert
		isErased.ShouldBeTrue();
	}

	[Fact]
	public async Task IsErasedAsync_ReturnsFalseForNonErasedAggregate()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		await _store.AppendAsync(aggregateId, "Order", CreateEvents(aggregateId, 1), -1, CancellationToken.None);

		// Act
		var isErased = await ((IEventStoreErasure)_store).IsErasedAsync(
			aggregateId, "Order", CancellationToken.None);

		// Assert
		isErased.ShouldBeFalse();
	}

	[Fact]
	public async Task IsErasedAsync_ReturnsFalseForNonExistentAggregate()
	{
		// Act
		var isErased = await ((IEventStoreErasure)_store).IsErasedAsync(
			"non-existent", "Order", CancellationToken.None);

		// Assert
		isErased.ShouldBeFalse();
	}

	[Fact]
	public async Task EraseEventsAsync_DoesNotAffectOtherAggregates()
	{
		// Arrange
		var aggregateId1 = Guid.NewGuid().ToString();
		var aggregateId2 = Guid.NewGuid().ToString();
		await _store.AppendAsync(aggregateId1, "Order", CreateEvents(aggregateId1, 2), -1, CancellationToken.None);
		await _store.AppendAsync(aggregateId2, "Order", CreateEvents(aggregateId2, 3), -1, CancellationToken.None);

		// Act - erase only first aggregate
		await ((IEventStoreErasure)_store).EraseEventsAsync(
			aggregateId1, "Order", Guid.NewGuid(), CancellationToken.None);

		// Assert - second aggregate should be untouched
		var loaded = await _store.LoadAsync(aggregateId2, "Order", CancellationToken.None);
		loaded.Count.ShouldBe(3);
		loaded.ShouldAllBe(e => e.EventType != "$erased");

		var isErased2 = await ((IEventStoreErasure)_store).IsErasedAsync(
			aggregateId2, "Order", CancellationToken.None);
		isErased2.ShouldBeFalse();
	}

	[Fact]
	public async Task EraseEventsAsync_ThrowsWhenCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await ((IEventStoreErasure)_store).EraseEventsAsync(
				"agg", "Order", Guid.NewGuid(), cts.Token));
	}

	[Fact]
	public async Task IsErasedAsync_ThrowsWhenCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await ((IEventStoreErasure)_store).IsErasedAsync("agg", "Order", cts.Token));
	}

	[Fact]
	public async Task Clear_ResetsErasureState()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		await _store.AppendAsync(aggregateId, "Order", CreateEvents(aggregateId, 1), -1, CancellationToken.None);
		await ((IEventStoreErasure)_store).EraseEventsAsync(
			aggregateId, "Order", Guid.NewGuid(), CancellationToken.None);

		// Act
		_store.Clear();

		// Assert - erasure state should be cleared
		var isErased = await ((IEventStoreErasure)_store).IsErasedAsync(
			aggregateId, "Order", CancellationToken.None);
		isErased.ShouldBeFalse();
	}

	[Fact]
	public async Task EraseEventsAsync_ErasedEventsHaveTombstonePayload()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		await _store.AppendAsync(aggregateId, "Order", CreateEvents(aggregateId, 1), -1, CancellationToken.None);

		// Act
		await ((IEventStoreErasure)_store).EraseEventsAsync(
			aggregateId, "Order", Guid.NewGuid(), CancellationToken.None);

		// Assert - event data should be replaced with tombstone payload
		var loaded = await _store.LoadAsync(aggregateId, "Order", CancellationToken.None);
		loaded[0].EventData.ShouldNotBeNull();
		System.Text.Encoding.UTF8.GetString(loaded[0].EventData).ShouldBe("ERASED");
	}

	[Fact]
	public async Task EraseEventsAsync_PreservesEventVersionAndTimestamp()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		await _store.AppendAsync(aggregateId, "Order", CreateEvents(aggregateId, 2), -1, CancellationToken.None);
		var originalEvents = await _store.LoadAsync(aggregateId, "Order", CancellationToken.None);
		var originalVersion0 = originalEvents[0].Version;
		var originalVersion1 = originalEvents[1].Version;
		var originalTimestamp0 = originalEvents[0].Timestamp;

		// Act
		await ((IEventStoreErasure)_store).EraseEventsAsync(
			aggregateId, "Order", Guid.NewGuid(), CancellationToken.None);

		// Assert - version and timestamp should be preserved
		var loaded = await _store.LoadAsync(aggregateId, "Order", CancellationToken.None);
		loaded[0].Version.ShouldBe(originalVersion0);
		loaded[1].Version.ShouldBe(originalVersion1);
		loaded[0].Timestamp.ShouldBe(originalTimestamp0);
	}

	private static IReadOnlyList<IDomainEvent> CreateEvents(string aggregateId, int count)
	{
		return Enumerable.Range(0, count).Select(i =>
			(IDomainEvent)new ErasureTestDomainEvent
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				Version = i,
				EventType = "TestEvent",
			}).ToList();
	}
}

public sealed class ErasureTestDomainEvent : IDomainEvent
{
	public required string EventId { get; init; }
	public required string AggregateId { get; init; }
	public required long Version { get; init; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public required string EventType { get; init; }
	public IDictionary<string, object>? Metadata { get; init; }
}
