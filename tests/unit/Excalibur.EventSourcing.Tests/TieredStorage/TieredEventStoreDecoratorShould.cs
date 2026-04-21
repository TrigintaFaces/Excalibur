// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.TieredStorage;
using Microsoft.Extensions.Logging.Abstractions;

using IEventStore = Excalibur.EventSourcing.Abstractions.IEventStore;
using StoredEvent = Excalibur.EventSourcing.Abstractions.StoredEvent;
using AppendResult = Excalibur.EventSourcing.Abstractions.AppendResult;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

/// <summary>
/// D.8 (r02v6u): Unit tests for TieredEventStoreDecorator --
/// read-through, gap detection, snapshot-aware gap coverage, archive policy.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TieredEventStoreDecoratorShould
{
	private readonly IEventStore _hotStore = A.Fake<IEventStore>();
	private readonly IColdEventStore _coldStore = A.Fake<IColdEventStore>();
	private readonly ISnapshotStore _snapshotStore = A.Fake<ISnapshotStore>();
	private readonly TieredEventStoreDecorator _decorator;
	private readonly TieredEventStoreDecorator _decoratorNoSnapshot;

	public TieredEventStoreDecoratorShould()
	{
		_decorator = new TieredEventStoreDecorator(
			_hotStore, _coldStore,
			NullLogger<TieredEventStoreDecorator>.Instance,
			_snapshotStore);

		_decoratorNoSnapshot = new TieredEventStoreDecorator(
			_hotStore, _coldStore,
			NullLogger<TieredEventStoreDecorator>.Instance);
	}

	// --- Writes always go to hot store ---

	[Fact]
	public async Task RouteAppendToHotStore()
	{
		_ = A.CallTo(() => _hotStore.AppendAsync(
			A<string>._, A<string>._, A<IEnumerable<Dispatch.Abstractions.IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 1));

		var result = await _decorator.AppendAsync(
			"agg-1", "Order", Array.Empty<Dispatch.Abstractions.IDomainEvent>(), 0, CancellationToken.None);

		result.Success.ShouldBeTrue();
		A.CallTo(() => _coldStore.WriteAsync(A<string>._, A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	// --- No gap: hot events start from version 1 ---

	[Fact]
	public async Task ReturnHotEventsWhenNoGap()
	{
		var hotEvents = CreateEvents("agg-1", 1, 2, 3);
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(hotEvents);

		var result = await _decorator.LoadAsync("agg-1", "Order", CancellationToken.None);

		result.Count.ShouldBe(3);
		A.CallTo(() => _coldStore.ReadAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	// --- No hot events: fallback to cold ---

	[Fact]
	public async Task ReadFromColdWhenNoHotEvents()
	{
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new List<StoredEvent>());
		_ = A.CallTo(() => _coldStore.HasArchivedEventsAsync("agg-1", A<CancellationToken>._))
			.Returns(true);

		var coldEvents = CreateEvents("agg-1", 1, 2, 3);
		_ = A.CallTo(() => _coldStore.ReadAsync("agg-1", A<CancellationToken>._))
			.Returns(coldEvents);

		var result = await _decorator.LoadAsync("agg-1", "Order", CancellationToken.None);

		result.Count.ShouldBe(3);
	}

	[Fact]
	public async Task ReturnEmptyWhenNeitherHotNorColdHasEvents()
	{
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new List<StoredEvent>());
		_ = A.CallTo(() => _coldStore.HasArchivedEventsAsync("agg-1", A<CancellationToken>._))
			.Returns(false);

		var result = await _decorator.LoadAsync("agg-1", "Order", CancellationToken.None);

		result.Count.ShouldBe(0);
	}

	// --- Gap detection: hot events start after version 1 ---

	[Fact]
	public async Task MergeHotAndColdWhenGapDetected()
	{
		// Hot has events 5-7, cold has 1-4
		var hotEvents = CreateEvents("agg-1", 5, 6, 7);
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(hotEvents);
		_ = A.CallTo(() => _snapshotStore.GetLatestSnapshotAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns((ISnapshot?)null); // no snapshot

		var coldEvents = CreateEvents("agg-1", 1, 2, 3, 4);
		_ = A.CallTo(() => _coldStore.ReadAsync("agg-1", 0L, A<CancellationToken>._))
			.Returns(coldEvents);

		var result = await _decoratorNoSnapshot.LoadAsync("agg-1", "Order", CancellationToken.None);

		// Cold + hot merged in order
		result.Count.ShouldBe(7);
		result[0].Version.ShouldBe(1);
		result[6].Version.ShouldBe(7);
	}

	// --- Snapshot-aware: gap covered by snapshot ---

	[Fact]
	public async Task SkipColdReadWhenSnapshotCoversGap()
	{
		// Hot has events 6-8, snapshot at version 5 covers 1-5
		var hotEvents = CreateEvents("agg-1", 6, 7, 8);
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(hotEvents);

		var snapshot = A.Fake<ISnapshot>();
		_ = A.CallTo(() => snapshot.Version).Returns(5);
		_ = A.CallTo(() => _snapshotStore.GetLatestSnapshotAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(snapshot);

		var result = await _decorator.LoadAsync("agg-1", "Order", CancellationToken.None);

		result.Count.ShouldBe(3); // only hot events, no cold read
		A.CallTo(() => _coldStore.ReadAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _coldStore.ReadAsync(A<string>._, A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReadColdWhenSnapshotDoesNotCoverFullGap()
	{
		// Hot has events 10-12, snapshot at version 5 covers 1-5, gap 6-9 still missing
		var hotEvents = CreateEvents("agg-1", 10, 11, 12);
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(hotEvents);

		var snapshot = A.Fake<ISnapshot>();
		_ = A.CallTo(() => snapshot.Version).Returns(5);
		_ = A.CallTo(() => _snapshotStore.GetLatestSnapshotAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(snapshot);

		var coldEvents = CreateEvents("agg-1", 6, 7, 8, 9);
		_ = A.CallTo(() => _coldStore.ReadAsync("agg-1", 0L, A<CancellationToken>._))
			.Returns(coldEvents);

		var result = await _decorator.LoadAsync("agg-1", "Order", CancellationToken.None);

		result.Count.ShouldBe(7); // cold 6-9 + hot 10-12
	}

	// --- Load with fromVersion ---

	[Fact]
	public async Task LoadFromVersionWithNoGap()
	{
		var hotEvents = CreateEvents("agg-1", 5, 6, 7);
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-1", "Order", 4L, A<CancellationToken>._))
			.Returns(hotEvents);

		var result = await _decorator.LoadAsync("agg-1", "Order", 4, CancellationToken.None);

		result.Count.ShouldBe(3);
		A.CallTo(() => _coldStore.ReadAsync(A<string>._, A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task LoadFromVersionFallbackToColdWhenHotEmpty()
	{
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-1", "Order", 0L, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var coldEvents = CreateEvents("agg-1", 1, 2, 3);
		_ = A.CallTo(() => _coldStore.ReadAsync("agg-1", 0L, A<CancellationToken>._))
			.Returns(coldEvents);

		var result = await _decorator.LoadAsync("agg-1", "Order", 0, CancellationToken.None);

		result.Count.ShouldBe(3);
	}

	// --- Null guards ---

	[Fact]
	public void ThrowOnNullHotStore()
	{
		Should.Throw<ArgumentNullException>(
			() => new TieredEventStoreDecorator(null!, _coldStore,
				NullLogger<TieredEventStoreDecorator>.Instance));
	}

	[Fact]
	public void ThrowOnNullColdStore()
	{
		Should.Throw<ArgumentNullException>(
			() => new TieredEventStoreDecorator(_hotStore, null!,
				NullLogger<TieredEventStoreDecorator>.Instance));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(
			() => new TieredEventStoreDecorator(_hotStore, _coldStore, null!));
	}

	// --- Helpers ---

	private static List<StoredEvent> CreateEvents(string aggregateId, params long[] versions)
	{
		return versions.Select(v => new StoredEvent(
			EventId: Guid.NewGuid().ToString(),
			AggregateId: aggregateId,
			AggregateType: "Order",
			EventType: "TestEvent",
			EventData: Array.Empty<byte>(),
			Metadata: null,
			Version: v,
			Timestamp: DateTimeOffset.UtcNow)).ToList();
	}
}
