// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Snapshots;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// Unit tests for IncrementalSnapshotStrategy (R27.61-R27.67).
/// Validates strategy behavior, compaction threshold, and integration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class IncrementalSnapshotStrategyShould
{
	/// <summary>
	/// R27.63: ShouldCreateSnapshot always returns true (deltas are cheap).
	/// </summary>
	[Fact]
	public void AlwaysReturnTrueForShouldCreateSnapshot()
	{
		// Arrange
		var strategy = new IncrementalSnapshotStrategy();
		var aggregate = A.Fake<Excalibur.Domain.Model.IAggregateRoot>();

		// Act
#pragma warning disable IL2026 // Members annotated with RequiresUnreferencedCode
#pragma warning disable IL3050 // Members annotated with RequiresDynamicCode
		var result = strategy.ShouldCreateSnapshot(aggregate);
#pragma warning restore IL3050
#pragma warning restore IL2026

		// Assert
		result.ShouldBeTrue();
	}

	/// <summary>
	/// Default compaction threshold is 10.
	/// </summary>
	[Fact]
	public void HaveDefaultCompactionThresholdOfTen()
	{
		var strategy = new IncrementalSnapshotStrategy();
		strategy.CompactionThreshold.ShouldBe(10);
	}

	/// <summary>
	/// Custom compaction threshold is respected.
	/// </summary>
	[Fact]
	public void AcceptCustomCompactionThreshold()
	{
		var strategy = new IncrementalSnapshotStrategy(compactionThreshold: 25);
		strategy.CompactionThreshold.ShouldBe(25);
	}

	/// <summary>
	/// Throws on invalid compaction threshold (< 1).
	/// </summary>
	[Fact]
	public void ThrowOnInvalidCompactionThreshold()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new IncrementalSnapshotStrategy(compactionThreshold: 0));
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new IncrementalSnapshotStrategy(compactionThreshold: -1));
	}

	/// <summary>
	/// Minimum threshold of 1 is valid (every save is a compaction).
	/// </summary>
	[Fact]
	public void AcceptMinimumCompactionThresholdOfOne()
	{
		var strategy = new IncrementalSnapshotStrategy(compactionThreshold: 1);
		strategy.CompactionThreshold.ShouldBe(1);
	}
}

/// <summary>
/// Unit tests for InMemoryIncrementalSnapshotStore (R27.61-R27.67).
/// Validates delta save, full save, load, and compaction behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryIncrementalSnapshotStoreShould
{
	private readonly InMemoryIncrementalSnapshotStore<SnapshotTestState> _store = new();

	/// <summary>
	/// AC-3.3: Save base + deltas -> load returns correct merged state.
	/// </summary>
	[Fact]
	public async Task SaveAndLoadDeltaCorrectly()
	{
		// Arrange
		var state = new SnapshotTestState { Name = "Order-1", Total = 100m };

		// Act -- save as delta
		await _store.SaveDeltaAsync("order-1", "Order", state, 1, CancellationToken.None);
		var loaded = await _store.LoadAsync("order-1", "Order", CancellationToken.None);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Name.ShouldBe("Order-1");
		loaded.Total.ShouldBe(100m);
	}

	/// <summary>
	/// AC-3.3: Multiple deltas accumulate correctly.
	/// </summary>
	[Fact]
	public async Task AccumulateMultipleDeltas()
	{
		// Arrange & Act -- save multiple deltas (each represents latest state in InMemory impl)
		await _store.SaveDeltaAsync("order-1", "Order", new SnapshotTestState { Name = "Order-1", Total = 100m }, 1, CancellationToken.None);
		await _store.SaveDeltaAsync("order-1", "Order", new SnapshotTestState { Name = "Order-1", Total = 250m }, 2, CancellationToken.None);
		await _store.SaveDeltaAsync("order-1", "Order", new SnapshotTestState { Name = "Order-1", Total = 400m }, 3, CancellationToken.None);

		var loaded = await _store.LoadAsync("order-1", "Order", CancellationToken.None);

		// Assert -- latest state
		loaded.ShouldNotBeNull();
		loaded.Total.ShouldBe(400m);
		_store.GetDeltaCount("order-1", "Order").ShouldBe(3);
	}

	/// <summary>
	/// AC-3.4: Full save compacts (resets delta count).
	/// </summary>
	[Fact]
	public async Task CompactOnFullSave()
	{
		// Arrange -- accumulate deltas
		for (var i = 1; i <= 5; i++)
		{
			await _store.SaveDeltaAsync("order-1", "Order",
				new SnapshotTestState { Name = "Order-1", Total = i * 100m }, i, CancellationToken.None);
		}

		_store.GetDeltaCount("order-1", "Order").ShouldBe(5);

		// Act -- full snapshot (compaction)
		await _store.SaveFullAsync("order-1", "Order",
			new SnapshotTestState { Name = "Order-1", Total = 500m }, 5, CancellationToken.None);

		// Assert -- delta count reset, state preserved
		_store.GetDeltaCount("order-1", "Order").ShouldBe(0);
		var loaded = await _store.LoadAsync("order-1", "Order", CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.Total.ShouldBe(500m);
	}

	/// <summary>
	/// AC-3.4: Compaction threshold triggers full snapshot decision.
	/// After 10 deltas, the next save should trigger compaction (tested via delta count).
	/// </summary>
	[Fact]
	public async Task TrackDeltaCountForCompactionDecision()
	{
		// Arrange -- save exactly compaction threshold (10) deltas
		for (var i = 1; i <= 10; i++)
		{
			await _store.SaveDeltaAsync("order-1", "Order",
				new SnapshotTestState { Name = "Order-1", Total = i * 10m }, i, CancellationToken.None);
		}

		// Assert -- 10 deltas accumulated (caller decides to compact)
		_store.GetDeltaCount("order-1", "Order").ShouldBe(10);

		// Act -- caller compacts
		await _store.SaveFullAsync("order-1", "Order",
			new SnapshotTestState { Name = "Order-1", Total = 100m }, 10, CancellationToken.None);

		// Assert -- compacted
		_store.GetDeltaCount("order-1", "Order").ShouldBe(0);
	}

	/// <summary>
	/// Returns null when no snapshot exists.
	/// </summary>
	[Fact]
	public async Task ReturnNullWhenNoSnapshotExists()
	{
		var result = await _store.LoadAsync("nonexistent", "Order", CancellationToken.None);
		result.ShouldBeNull();
	}

	/// <summary>
	/// Loaded state is a deep copy (no shared mutable state).
	/// </summary>
	[Fact]
	public async Task ReturnDeepCopyOnLoad()
	{
		// Arrange
		await _store.SaveDeltaAsync("order-1", "Order",
			new SnapshotTestState { Name = "Order-1", Total = 100m }, 1, CancellationToken.None);

		// Act
		var load1 = await _store.LoadAsync("order-1", "Order", CancellationToken.None);
		var load2 = await _store.LoadAsync("order-1", "Order", CancellationToken.None);

		// Assert -- different instances
		load1.ShouldNotBeSameAs(load2);
		load1!.Total.ShouldBe(load2!.Total);
	}

	/// <summary>
	/// Multiple aggregates stored independently.
	/// </summary>
	[Fact]
	public async Task StoreMultipleAggregatesIndependently()
	{
		// Arrange
		await _store.SaveDeltaAsync("order-1", "Order",
			new SnapshotTestState { Name = "Order-1", Total = 100m }, 1, CancellationToken.None);
		await _store.SaveDeltaAsync("order-2", "Order",
			new SnapshotTestState { Name = "Order-2", Total = 200m }, 1, CancellationToken.None);

		// Act
		var load1 = await _store.LoadAsync("order-1", "Order", CancellationToken.None);
		var load2 = await _store.LoadAsync("order-2", "Order", CancellationToken.None);

		// Assert
		load1!.Total.ShouldBe(100m);
		load2!.Total.ShouldBe(200m);
	}

	/// <summary>
	/// Validates null/empty argument guards.
	/// </summary>
	[Fact]
	public void ThrowOnNullOrEmptyArguments()
	{
		Should.Throw<ArgumentException>(() =>
			_store.LoadAsync(null!, "Order", CancellationToken.None).GetAwaiter().GetResult());
		Should.Throw<ArgumentException>(() =>
			_store.LoadAsync("id", "", CancellationToken.None).GetAwaiter().GetResult());

		Should.Throw<ArgumentException>(() =>
			_store.SaveDeltaAsync(null!, "Order", new SnapshotTestState(), 1, CancellationToken.None).GetAwaiter().GetResult());
		Should.Throw<ArgumentNullException>(() =>
			_store.SaveDeltaAsync("id", "Order", null!, 1, CancellationToken.None).GetAwaiter().GetResult());

		Should.Throw<ArgumentException>(() =>
			_store.SaveFullAsync(null!, "Order", new SnapshotTestState(), 1, CancellationToken.None).GetAwaiter().GetResult());
		Should.Throw<ArgumentNullException>(() =>
			_store.SaveFullAsync("id", "Order", null!, 1, CancellationToken.None).GetAwaiter().GetResult());
	}

	/// <summary>
	/// AC-3.5: Full rebuild from incremental snapshots produces correct state.
	/// Simulates: deltas 1-5, compaction at 5, more deltas 6-8, load -> correct.
	/// </summary>
	[Fact]
	public async Task SupportFullRebuildFromIncrementalSnapshots()
	{
		// Arrange -- 5 deltas then compaction
		for (var i = 1; i <= 5; i++)
		{
			await _store.SaveDeltaAsync("order-1", "Order",
				new SnapshotTestState { Name = "Order-1", Total = i * 50m, ItemCount = i }, i, CancellationToken.None);
		}

		// Compact at version 5
		await _store.SaveFullAsync("order-1", "Order",
			new SnapshotTestState { Name = "Order-1", Total = 250m, ItemCount = 5 }, 5, CancellationToken.None);

		// More deltas after compaction
		for (var i = 6; i <= 8; i++)
		{
			await _store.SaveDeltaAsync("order-1", "Order",
				new SnapshotTestState { Name = "Order-1", Total = i * 50m, ItemCount = i }, i, CancellationToken.None);
		}

		// Act
		var loaded = await _store.LoadAsync("order-1", "Order", CancellationToken.None);

		// Assert -- latest state from post-compaction deltas
		loaded.ShouldNotBeNull();
		loaded.Total.ShouldBe(400m); // 8 * 50
		loaded.ItemCount.ShouldBe(8);
		_store.GetDeltaCount("order-1", "Order").ShouldBe(3); // 3 deltas since compaction
	}
}

/// <summary>
/// Test state for incremental snapshot tests.
/// </summary>
public sealed class SnapshotTestState
{
	public string Name { get; set; } = string.Empty;
	public decimal Total { get; set; }
	public int ItemCount { get; set; }
}
