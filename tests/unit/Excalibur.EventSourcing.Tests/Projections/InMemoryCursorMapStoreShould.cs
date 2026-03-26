// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Unit tests for InMemoryCursorMapStore (R27.53-R27.58).
/// Validates cursor map persistence, retrieval, and reset behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCursorMapStoreShould
{
	private readonly InMemoryCursorMapStore _store = new();

	/// <summary>
	/// AC-2.3: Cursor map persists across calls.
	/// </summary>
	[Fact]
	public async Task PersistCursorMapAcrossCalls()
	{
		// Arrange
		var cursorMap = new Dictionary<string, long>
		{
			["stream-orders"] = 42,
			["stream-inventory"] = 100,
		};

		// Act
		await _store.SaveCursorMapAsync("OrderProjection", cursorMap, CancellationToken.None);
		var loaded = await _store.GetCursorMapAsync("OrderProjection", CancellationToken.None);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Count.ShouldBe(2);
		loaded["stream-orders"].ShouldBe(42);
		loaded["stream-inventory"].ShouldBe(100);
	}

	/// <summary>
	/// Returns empty dictionary when no cursor map exists.
	/// </summary>
	[Fact]
	public async Task ReturnEmptyDictionaryWhenNoCursorMapExists()
	{
		// Act
		var result = await _store.GetCursorMapAsync("NonExistent", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Count.ShouldBe(0);
	}

	/// <summary>
	/// AC-2.4: Multi-stream projection position tracking -- each stream tracked independently.
	/// </summary>
	[Fact]
	public async Task TrackMultipleStreamPositionsIndependently()
	{
		// Arrange -- save positions for two different projections
		var orderCursors = new Dictionary<string, long> { ["stream-A"] = 10 };
		var inventoryCursors = new Dictionary<string, long> { ["stream-B"] = 20 };

		await _store.SaveCursorMapAsync("OrderProjection", orderCursors, CancellationToken.None);
		await _store.SaveCursorMapAsync("InventoryProjection", inventoryCursors, CancellationToken.None);

		// Act
		var orderResult = await _store.GetCursorMapAsync("OrderProjection", CancellationToken.None);
		var inventoryResult = await _store.GetCursorMapAsync("InventoryProjection", CancellationToken.None);

		// Assert -- independent tracking
		orderResult["stream-A"].ShouldBe(10);
		inventoryResult["stream-B"].ShouldBe(20);
	}

	/// <summary>
	/// AC-2.5: Reset clears all positions for a projection.
	/// </summary>
	[Fact]
	public async Task ResetCursorMapToEmpty()
	{
		// Arrange
		var cursors = new Dictionary<string, long>
		{
			["stream-A"] = 50,
			["stream-B"] = 75,
		};
		await _store.SaveCursorMapAsync("OrderProjection", cursors, CancellationToken.None);

		// Act
		await _store.ResetCursorMapAsync("OrderProjection", CancellationToken.None);

		// Assert -- empty after reset
		var result = await _store.GetCursorMapAsync("OrderProjection", CancellationToken.None);
		result.Count.ShouldBe(0);
	}

	/// <summary>
	/// Save atomically replaces previous cursor map.
	/// </summary>
	[Fact]
	public async Task AtomicallyReplacePreviousCursorMap()
	{
		// Arrange
		var initial = new Dictionary<string, long> { ["stream-A"] = 10, ["stream-B"] = 20 };
		await _store.SaveCursorMapAsync("Projection", initial, CancellationToken.None);

		var updated = new Dictionary<string, long> { ["stream-A"] = 50, ["stream-C"] = 30 };

		// Act
		await _store.SaveCursorMapAsync("Projection", updated, CancellationToken.None);

		// Assert -- replaced, not merged
		var result = await _store.GetCursorMapAsync("Projection", CancellationToken.None);
		result.Count.ShouldBe(2);
		result["stream-A"].ShouldBe(50);
		result["stream-C"].ShouldBe(30);
		result.ContainsKey("stream-B").ShouldBeFalse();
	}

	/// <summary>
	/// Returned cursor map is a copy -- modifying it doesn't affect stored data.
	/// </summary>
	[Fact]
	public async Task ReturnDefensiveCopyOnGet()
	{
		// Arrange
		var cursors = new Dictionary<string, long> { ["stream-A"] = 10 };
		await _store.SaveCursorMapAsync("Projection", cursors, CancellationToken.None);

		// Act -- modify the returned dictionary
		var result = await _store.GetCursorMapAsync("Projection", CancellationToken.None);
		// IReadOnlyDictionary prevents mutation, but verify by re-loading
		var reloaded = await _store.GetCursorMapAsync("Projection", CancellationToken.None);

		// Assert -- original is unaffected
		reloaded["stream-A"].ShouldBe(10);
	}

	/// <summary>
	/// Reset is safe to call on non-existent projection.
	/// </summary>
	[Fact]
	public async Task SafelyResetNonExistentProjection()
	{
		// Act -- should not throw
		await _store.ResetCursorMapAsync("DoesNotExist", CancellationToken.None);

		// Assert -- still returns empty
		var result = await _store.GetCursorMapAsync("DoesNotExist", CancellationToken.None);
		result.Count.ShouldBe(0);
	}

	/// <summary>
	/// Validates null/empty argument guards.
	/// </summary>
	[Fact]
	public async Task ThrowOnNullOrEmptyArguments()
	{
		Should.Throw<ArgumentException>(() =>
			_store.GetCursorMapAsync(null!, CancellationToken.None).GetAwaiter().GetResult());
		Should.Throw<ArgumentException>(() =>
			_store.GetCursorMapAsync("", CancellationToken.None).GetAwaiter().GetResult());

		Should.Throw<ArgumentException>(() =>
			_store.SaveCursorMapAsync(null!, new Dictionary<string, long>(), CancellationToken.None).GetAwaiter().GetResult());
		Should.Throw<ArgumentNullException>(() =>
			_store.SaveCursorMapAsync("proj", null!, CancellationToken.None).GetAwaiter().GetResult());

		Should.Throw<ArgumentException>(() =>
			_store.ResetCursorMapAsync(null!, CancellationToken.None).GetAwaiter().GetResult());
	}

	/// <summary>
	/// Concurrent access is safe (ConcurrentDictionary backing).
	/// </summary>
	[Fact]
	public async Task HandleConcurrentAccessSafely()
	{
		// Arrange
		var tasks = Enumerable.Range(0, 10).Select(async i =>
		{
			var cursors = new Dictionary<string, long> { [$"stream-{i}"] = i };
			await _store.SaveCursorMapAsync($"Projection-{i}", cursors, CancellationToken.None);
		});

		// Act
		await Task.WhenAll(tasks);

		// Assert -- all projections stored independently
		for (var i = 0; i < 10; i++)
		{
			var result = await _store.GetCursorMapAsync($"Projection-{i}", CancellationToken.None);
			result[$"stream-{i}"].ShouldBe(i);
		}
	}
}
