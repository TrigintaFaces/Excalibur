// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="ProjectionStoreExtensions"/>.
/// Covers ExistsAsync and DistinctValuesAsync extension methods with
/// ISP-optimized paths and fallback paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionStoreExtensionsShould
{
	// ═══════════════════════════════════════════════════
	// ExistsAsync — ISP optimized path
	// ═══════════════════════════════════════════════════

	[Fact]
	public async Task ExistsAsync_UseOptimizedPath_WhenStoreImplementsISP()
	{
		// Arrange
		var store = new OptimizedProjectionStore();
		store.SetExists("order-1", true);

		// Act
		var exists = await store.ExistsAsync("order-1", CancellationToken.None);

		// Assert
		exists.ShouldBeTrue();
		store.ExistsCallCount.ShouldBe(1);
		store.GetByIdCallCount.ShouldBe(0); // Should NOT fall back to GetByIdAsync
	}

	[Fact]
	public async Task ExistsAsync_ReturnFalse_WhenOptimizedPathReportsMissing()
	{
		// Arrange
		var store = new OptimizedProjectionStore();
		// No items added — ExistsAsync returns false

		// Act
		var exists = await store.ExistsAsync("missing-id", CancellationToken.None);

		// Assert
		exists.ShouldBeFalse();
		store.ExistsCallCount.ShouldBe(1);
	}

	// ═══════════════════════════════════════════════════
	// ExistsAsync — Fallback path
	// ═══════════════════════════════════════════════════

	[Fact]
	public async Task ExistsAsync_FallBackToGetById_WhenStoreDoesNotImplementISP()
	{
		// Arrange
		var store = new BasicProjectionStore();
		store.Add("order-1", new TestProjection { Name = "Order 1" });

		// Act
		var exists = await store.ExistsAsync("order-1", CancellationToken.None);

		// Assert
		exists.ShouldBeTrue();
		store.GetByIdCallCount.ShouldBe(1);
	}

	[Fact]
	public async Task ExistsAsync_ReturnFalse_WhenFallbackGetByIdReturnsNull()
	{
		// Arrange
		var store = new BasicProjectionStore();

		// Act
		var exists = await store.ExistsAsync("missing-id", CancellationToken.None);

		// Assert
		exists.ShouldBeFalse();
		store.GetByIdCallCount.ShouldBe(1);
	}

	// ═══════════════════════════════════════════════════
	// ExistsAsync — Null guards
	// ═══════════════════════════════════════════════════

	[Fact]
	public async Task ExistsAsync_ThrowArgumentNullException_WhenStoreIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await ProjectionStoreExtensions.ExistsAsync<TestProjection>(null!, "id", CancellationToken.None));
	}

	[Fact]
	public async Task ExistsAsync_ThrowArgumentException_WhenIdIsNull()
	{
		// Arrange
		var store = new BasicProjectionStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await store.ExistsAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExistsAsync_ThrowArgumentException_WhenIdIsEmpty()
	{
		// Arrange
		var store = new BasicProjectionStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.ExistsAsync(string.Empty, CancellationToken.None));
	}

	// ═══════════════════════════════════════════════════
	// DistinctValuesAsync — ISP optimized path
	// ═══════════════════════════════════════════════════

	[Fact]
	public async Task DistinctValuesAsync_UseOptimizedPath_WhenStoreImplementsISP()
	{
		// Arrange
		var store = new OptimizedProjectionStore();
		store.SetDistinctValues("Status", new List<object> { "Active", "Completed" });

		// Act
		var values = await store.DistinctValuesAsync("Status", null, CancellationToken.None);

		// Assert
		values.Count.ShouldBe(2);
		values.ShouldContain("Active");
		values.ShouldContain("Completed");
		store.DistinctValuesCallCount.ShouldBe(1);
		store.QueryCallCount.ShouldBe(0); // Should NOT fall back to QueryAsync
	}

	// ═══════════════════════════════════════════════════
	// DistinctValuesAsync — Reflection fallback path
	// ═══════════════════════════════════════════════════

	[Fact]
	public async Task DistinctValuesAsync_FallBackToReflection_WhenStoreDoesNotImplementISP()
	{
		// Arrange
		var store = new BasicProjectionStore();
		store.Add("p1", new TestProjection { Name = "Alice", Status = "Active" });
		store.Add("p2", new TestProjection { Name = "Bob", Status = "Active" });
		store.Add("p3", new TestProjection { Name = "Charlie", Status = "Completed" });

		// Act
		var values = await store.DistinctValuesAsync("Status", null, CancellationToken.None);

		// Assert
		values.Count.ShouldBe(2);
		values.ShouldContain("Active");
		values.ShouldContain("Completed");
		store.QueryCallCount.ShouldBe(1);
	}

	[Fact]
	public async Task DistinctValuesAsync_ExcludeNullValues_InReflectionFallback()
	{
		// Arrange
		var store = new BasicProjectionStore();
		store.Add("p1", new TestProjection { Name = "Alice", Status = "Active" });
		store.Add("p2", new TestProjection { Name = "Bob", Status = null! });

		// Act
		var values = await store.DistinctValuesAsync("Status", null, CancellationToken.None);

		// Assert
		values.Count.ShouldBe(1);
		values.ShouldContain("Active");
	}

	[Fact]
	public async Task DistinctValuesAsync_ReturnEmpty_WhenNoProjectionsExist()
	{
		// Arrange
		var store = new BasicProjectionStore();

		// Act
		var values = await store.DistinctValuesAsync("Status", null, CancellationToken.None);

		// Assert
		values.Count.ShouldBe(0);
	}

	[Fact]
	public async Task DistinctValuesAsync_ThrowArgumentException_WhenPropertyNotFound()
	{
		// Arrange
		var store = new BasicProjectionStore();
		store.Add("p1", new TestProjection { Name = "Alice" });

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DistinctValuesAsync("NonExistentProperty", null, CancellationToken.None));
	}

	// ═══════════════════════════════════════════════════
	// DistinctValuesAsync — Null guards
	// ═══════════════════════════════════════════════════

	[Fact]
	public async Task DistinctValuesAsync_ThrowArgumentNullException_WhenStoreIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await ProjectionStoreExtensions.DistinctValuesAsync<TestProjection>(
				null!, "Status", null, CancellationToken.None));
	}

	[Fact]
	public async Task DistinctValuesAsync_ThrowArgumentException_WhenPropertyNameIsNull()
	{
		var store = new BasicProjectionStore();

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await store.DistinctValuesAsync(null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task DistinctValuesAsync_ThrowArgumentException_WhenPropertyNameIsEmpty()
	{
		var store = new BasicProjectionStore();

		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DistinctValuesAsync(string.Empty, null, CancellationToken.None));
	}

	// ═══════════════════════════════════════════════════
	// DistinctValuesAsync — Filters passthrough
	// ═══════════════════════════════════════════════════

	[Fact]
	public async Task DistinctValuesAsync_PassFiltersToOptimizedStore()
	{
		// Arrange
		var store = new OptimizedProjectionStore();
		store.SetDistinctValues("Status", new List<object> { "Active" });
		var filters = new Dictionary<string, object> { ["Category"] = "Premium" };

		// Act
		var values = await store.DistinctValuesAsync("Status", filters, CancellationToken.None);

		// Assert
		store.LastDistinctValuesFilters.ShouldNotBeNull();
		store.LastDistinctValuesFilters!["Category"].ShouldBe("Premium");
		store.DistinctValuesCallCount.ShouldBe(1);
	}

	[Fact]
	public async Task DistinctValuesAsync_PassNullFiltersToFallbackQuery()
	{
		// Arrange
		var store = new BasicProjectionStore();
		store.Add("p1", new TestProjection { Name = "Alice", Status = "Active" });

		// Act
		var values = await store.DistinctValuesAsync("Status", null, CancellationToken.None);

		// Assert
		store.QueryCallCount.ShouldBe(1);
		store.LastQueryFilters.ShouldBeNull();
	}

	// ═══════════════════════════════════════════════════
	// Test doubles
	// ═══════════════════════════════════════════════════

	private sealed class TestProjection
	{
		public string Name { get; set; } = string.Empty;
		public string? Status { get; set; }
	}

	/// <summary>
	/// Basic store without ISP interfaces — forces fallback paths.
	/// </summary>
	private sealed class BasicProjectionStore : IProjectionStore<TestProjection>
	{
		private readonly Dictionary<string, TestProjection> _store = new(StringComparer.Ordinal);

		public int GetByIdCallCount { get; private set; }
		public int QueryCallCount { get; private set; }
		public IDictionary<string, object>? LastQueryFilters { get; private set; }

		public void Add(string id, TestProjection projection) => _store[id] = projection;

		public Task<TestProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
		{
			GetByIdCallCount++;
			_store.TryGetValue(id, out var result);
			return Task.FromResult(result);
		}

		public Task UpsertAsync(string id, TestProjection projection, CancellationToken cancellationToken)
		{
			_store[id] = projection;
			return Task.CompletedTask;
		}

		public Task DeleteAsync(string id, CancellationToken cancellationToken)
		{
			_store.Remove(id);
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<TestProjection>> QueryAsync(
			IDictionary<string, object>? filters,
			QueryOptions? options,
			CancellationToken cancellationToken)
		{
			QueryCallCount++;
			LastQueryFilters = filters;
			return Task.FromResult<IReadOnlyList<TestProjection>>(_store.Values.ToList());
		}

		public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
			=> Task.FromResult((long)_store.Count);
	}

	/// <summary>
	/// Store implementing both ISP sub-interfaces — verifies optimized paths.
	/// </summary>
	private sealed class OptimizedProjectionStore :
		IProjectionStore<TestProjection>,
		IExistsProjectionStore<TestProjection>,
		IDistinctValuesProjectionStore<TestProjection>
	{
		private readonly Dictionary<string, TestProjection> _store = new(StringComparer.Ordinal);
		private readonly Dictionary<string, bool> _existsMap = new(StringComparer.Ordinal);
		private readonly Dictionary<string, IReadOnlyList<object>> _distinctValuesMap = new(StringComparer.Ordinal);

		public int ExistsCallCount { get; private set; }
		public int DistinctValuesCallCount { get; private set; }
		public int GetByIdCallCount { get; private set; }
		public int QueryCallCount { get; private set; }
		public IDictionary<string, object>? LastDistinctValuesFilters { get; private set; }

		public void SetExists(string id, bool exists) => _existsMap[id] = exists;
		public void SetDistinctValues(string propertyName, IReadOnlyList<object> values) => _distinctValuesMap[propertyName] = values;

		// ISP: IExistsProjectionStore
		public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
		{
			ExistsCallCount++;
			return Task.FromResult(_existsMap.TryGetValue(id, out var exists) && exists);
		}

		// ISP: IDistinctValuesProjectionStore
		public Task<IReadOnlyList<object>> DistinctValuesAsync(
			string propertyName,
			IDictionary<string, object>? filters,
			CancellationToken cancellationToken)
		{
			DistinctValuesCallCount++;
			LastDistinctValuesFilters = filters;
			return Task.FromResult(
				_distinctValuesMap.TryGetValue(propertyName, out var values)
					? values
					: (IReadOnlyList<object>)Array.Empty<object>());
		}

		// Base IProjectionStore
		public Task<TestProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
		{
			GetByIdCallCount++;
			_store.TryGetValue(id, out var result);
			return Task.FromResult(result);
		}

		public Task UpsertAsync(string id, TestProjection projection, CancellationToken cancellationToken)
		{
			_store[id] = projection;
			return Task.CompletedTask;
		}

		public Task DeleteAsync(string id, CancellationToken cancellationToken)
		{
			_store.Remove(id);
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<TestProjection>> QueryAsync(
			IDictionary<string, object>? filters,
			QueryOptions? options,
			CancellationToken cancellationToken)
		{
			QueryCallCount++;
			return Task.FromResult<IReadOnlyList<TestProjection>>(_store.Values.ToList());
		}

		public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
			=> Task.FromResult((long)_store.Count);
	}
}
