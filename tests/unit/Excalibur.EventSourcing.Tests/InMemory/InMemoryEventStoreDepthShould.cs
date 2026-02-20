// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Tests.InMemory;

/// <summary>
/// Depth coverage tests for <see cref="Excalibur.EventSourcing.InMemory.InMemoryEventStore"/>.
/// Covers append, load, load with from version, concurrency conflict, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryEventStoreDepthShould
{
	[Fact]
	public async Task AppendAsync_ReturnsSuccess_ForNewAggregate()
	{
		// Arrange
		var store = new InMemoryEventStore();
		var events = CreateEvents("agg-1", 1);

		// Act
		var result = await store.AppendAsync("agg-1", "Order", events, -1, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task AppendAsync_ReturnsConcurrencyConflict_OnVersionMismatch()
	{
		// Arrange
		var store = new InMemoryEventStore();
		await store.AppendAsync("agg-1", "Order", CreateEvents("agg-1", 1), -1, CancellationToken.None);

		// Act — append with wrong expected version
		var result = await store.AppendAsync("agg-1", "Order", CreateEvents("agg-1", 1), 5, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();
	}

	[Fact]
	public async Task LoadAsync_ReturnsEmpty_WhenNoEvents()
	{
		// Arrange
		var store = new InMemoryEventStore();

		// Act
		var events = await store.LoadAsync("non-existent", "Order", CancellationToken.None);

		// Assert
		events.ShouldBeEmpty();
	}

	[Fact]
	public async Task LoadAsync_ReturnsAllEvents_ForExistingAggregate()
	{
		// Arrange
		var store = new InMemoryEventStore();
		await store.AppendAsync("agg-1", "Order", CreateEvents("agg-1", 3), -1, CancellationToken.None);

		// Act
		var events = await store.LoadAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		events.Count.ShouldBe(3);
	}

	[Fact]
	public async Task LoadAsync_WithFromVersion_ReturnsEventsAfterVersion()
	{
		// Arrange
		var store = new InMemoryEventStore();
		await store.AppendAsync("agg-1", "Order", CreateEvents("agg-1", 5), -1, CancellationToken.None);

		// Act — load events after version 2
		var events = await store.LoadAsync("agg-1", "Order", 2, CancellationToken.None);

		// Assert
		events.Count.ShouldBe(2); // versions 3 and 4
	}

	[Fact]
	public async Task MultipleAppends_AccumulateEvents()
	{
		// Arrange
		var store = new InMemoryEventStore();

		// Act
		await store.AppendAsync("agg-1", "Order", CreateEvents("agg-1", 2), -1, CancellationToken.None);
		await store.AppendAsync("agg-1", "Order", CreateEvents("agg-1", 2), 1, CancellationToken.None);

		// Assert
		var events = await store.LoadAsync("agg-1", "Order", CancellationToken.None);
		events.Count.ShouldBe(4);
	}

	[Fact]
	public async Task DifferentAggregates_AreIsolated()
	{
		// Arrange
		var store = new InMemoryEventStore();

		// Act
		await store.AppendAsync("agg-1", "Order", CreateEvents("agg-1", 2), -1, CancellationToken.None);
		await store.AppendAsync("agg-2", "Order", CreateEvents("agg-2", 3), -1, CancellationToken.None);

		// Assert
		(await store.LoadAsync("agg-1", "Order", CancellationToken.None)).Count.ShouldBe(2);
		(await store.LoadAsync("agg-2", "Order", CancellationToken.None)).Count.ShouldBe(3);
	}

	private static IReadOnlyList<IDomainEvent> CreateEvents(string aggregateId, int count)
	{
		return Enumerable.Range(0, count).Select(i =>
			(IDomainEvent)new TestDomainEvent
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				Version = i,
				EventType = "TestEvent",
			}).ToList();
	}

	private sealed class TestDomainEvent : IDomainEvent
	{
		public required string EventId { get; init; }
		public required string AggregateId { get; init; }
		public required long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public required string EventType { get; init; }
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
