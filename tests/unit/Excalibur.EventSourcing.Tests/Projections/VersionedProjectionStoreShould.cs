// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Tests for <see cref="VersionedProjection{TProjection}"/> and
/// <see cref="IVersionedProjectionStore{TProjection}"/> contract (bd-yhxua9).
/// Uses an in-memory implementation to verify the interface contract and
/// consumer-facing patterns without SqlServer dependency.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class VersionedProjectionStoreShould
{
	// ───────────────────────────────────────────────────────────────
	// VersionedProjection<T> construction and property access
	// ───────────────────────────────────────────────────────────────

	[Fact]
	public void CreateVersionedProjectionWithCorrectProperties()
	{
		// Arrange
		var projection = new OrderView { CustomerName = "Alice", Total = 100m };

		// Act
		var versioned = new VersionedProjection<OrderView>(projection, 5);

		// Assert
		versioned.Projection.ShouldBeSameAs(projection);
		versioned.Version.ShouldBe(5);
	}

	[Fact]
	public void SupportVersionOne()
	{
		// Arrange & Act — version 1 is the initial version
		var versioned = new VersionedProjection<OrderView>(new OrderView(), 1);

		// Assert
		versioned.Version.ShouldBe(1);
	}

	[Fact]
	public void SupportLargeVersionNumbers()
	{
		// Arrange & Act — version can be very large (long)
		var versioned = new VersionedProjection<OrderView>(new OrderView(), long.MaxValue);

		// Assert
		versioned.Version.ShouldBe(long.MaxValue);
	}

	// ───────────────────────────────────────────────────────────────
	// IVersionedProjectionStore<T> contract tests (via in-memory impl)
	// ───────────────────────────────────────────────────────────────

	[Fact]
	public async Task ReturnNullForNonExistentProjection()
	{
		// Arrange
		var store = new InMemoryVersionedProjectionStore<OrderView>();

		// Act
		var result = await store.GetVersionedAsync("non-existent", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task InsertWithNullExpectedVersion()
	{
		// Arrange
		var store = new InMemoryVersionedProjectionStore<OrderView>();
		var projection = new OrderView { CustomerName = "Alice", Total = 100m };

		// Act — null expectedVersion = initial insert, no concurrency check
		await store.UpsertVersionedAsync("order-1", projection, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var result = await store.GetVersionedAsync("order-1", CancellationToken.None)
			.ConfigureAwait(false);
		result.ShouldNotBeNull();
		result.Projection.CustomerName.ShouldBe("Alice");
		result.Version.ShouldBe(1); // first insert starts at version 1
	}

	[Fact]
	public async Task IncrementVersionOnEachUpdate()
	{
		// Arrange
		var store = new InMemoryVersionedProjectionStore<OrderView>();
		var projection = new OrderView { CustomerName = "Alice", Total = 100m };

		// Act — insert, then two updates
		await store.UpsertVersionedAsync("order-1", projection, null, CancellationToken.None)
			.ConfigureAwait(false);

		projection.Total = 200m;
		await store.UpsertVersionedAsync("order-1", projection, 1, CancellationToken.None)
			.ConfigureAwait(false);

		projection.Total = 300m;
		await store.UpsertVersionedAsync("order-1", projection, 2, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var result = await store.GetVersionedAsync("order-1", CancellationToken.None)
			.ConfigureAwait(false);
		result.ShouldNotBeNull();
		result.Version.ShouldBe(3);
		result.Projection.Total.ShouldBe(300m);
	}

	[Fact]
	public async Task ThrowConcurrencyExceptionOnVersionMismatch()
	{
		// Arrange
		var store = new InMemoryVersionedProjectionStore<OrderView>();
		var projection = new OrderView { CustomerName = "Alice", Total = 100m };
		await store.UpsertVersionedAsync("order-1", projection, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Act & Assert — expect version 1, but pass stale version 99
		await Should.ThrowAsync<ConcurrencyException>(
			store.UpsertVersionedAsync("order-1", projection, 99, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task SucceedWhenExpectedVersionMatches()
	{
		// Arrange
		var store = new InMemoryVersionedProjectionStore<OrderView>();
		var projection = new OrderView { CustomerName = "Alice", Total = 100m };
		await store.UpsertVersionedAsync("order-1", projection, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Act — correct expected version
		projection.Total = 200m;
		await store.UpsertVersionedAsync("order-1", projection, 1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var result = await store.GetVersionedAsync("order-1", CancellationToken.None)
			.ConfigureAwait(false);
		result.ShouldNotBeNull();
		result.Version.ShouldBe(2);
		result.Projection.Total.ShouldBe(200m);
	}

	[Fact]
	public async Task ThrowConcurrencyExceptionForStaleVersion()
	{
		// Arrange — simulate two readers, one writes first
		var store = new InMemoryVersionedProjectionStore<OrderView>();
		var projection = new OrderView { CustomerName = "Alice", Total = 100m };
		await store.UpsertVersionedAsync("order-1", projection, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Reader A reads at version 1
		var readA = await store.GetVersionedAsync("order-1", CancellationToken.None)
			.ConfigureAwait(false);
		readA.ShouldNotBeNull();

		// Reader B reads at version 1
		var readB = await store.GetVersionedAsync("order-1", CancellationToken.None)
			.ConfigureAwait(false);
		readB.ShouldNotBeNull();

		// Writer A succeeds (version 1 → 2)
		var updatedA = new OrderView { CustomerName = "Alice", Total = 200m };
		await store.UpsertVersionedAsync("order-1", updatedA, readA.Version, CancellationToken.None)
			.ConfigureAwait(false);

		// Writer B fails — stale version 1, actual is now 2
		var updatedB = new OrderView { CustomerName = "Alice", Total = 300m };
		await Should.ThrowAsync<ConcurrencyException>(
			store.UpsertVersionedAsync("order-1", updatedB, readB.Version, CancellationToken.None))
			.ConfigureAwait(false);
	}

	// ───────────────────────────────────────────────────────────────
	// Pattern matching detection (consumer-facing pattern)
	// ───────────────────────────────────────────────────────────────

	[Fact]
	public void DetectVersionedStoreViaPatternMatching()
	{
		// Arrange — store implements both IProjectionStore<T> and IVersionedProjectionStore<T>
		IProjectionStore<OrderView> store = new InMemoryVersionedProjectionStore<OrderView>();

		// Act — consumer pattern matching
		var isVersioned = store is IVersionedProjectionStore<OrderView>;

		// Assert
		isVersioned.ShouldBeTrue();
	}

	[Fact]
	public void NotDetectVersionedStoreOnPlainStore()
	{
		// Arrange — plain store without versioning
		IProjectionStore<OrderView> store = new InMemoryProjectionStore<OrderView>();

		// Act
		var isVersioned = store is IVersionedProjectionStore<OrderView>;

		// Assert
		isVersioned.ShouldBeFalse();
	}

	[Fact]
	public async Task WorkThroughPatternMatchedReference()
	{
		// Arrange
		IProjectionStore<OrderView> store = new InMemoryVersionedProjectionStore<OrderView>();
		var projection = new OrderView { CustomerName = "Test", Total = 50m };

		// Act — consumer pattern: detect, use versioned API
		if (store is IVersionedProjectionStore<OrderView> versioned)
		{
			await versioned.UpsertVersionedAsync("order-pm", projection, null, CancellationToken.None)
				.ConfigureAwait(false);

			var result = await versioned.GetVersionedAsync("order-pm", CancellationToken.None)
				.ConfigureAwait(false);

			// Assert
			result.ShouldNotBeNull();
			result.Projection.CustomerName.ShouldBe("Test");
			result.Version.ShouldBe(1);
		}
		else
		{
			// Should not reach here
			throw new InvalidOperationException("Pattern matching failed");
		}
	}

	// ───────────────────────────────────────────────────────────────
	// Edge cases
	// ───────────────────────────────────────────────────────────────

	[Fact]
	public async Task AllowInsertWithNullVersionForNewId()
	{
		// Arrange
		var store = new InMemoryVersionedProjectionStore<OrderView>();

		// Act — insert two different projections with null version (both are new)
		await store.UpsertVersionedAsync("order-a", new OrderView { CustomerName = "A" }, null, CancellationToken.None)
			.ConfigureAwait(false);
		await store.UpsertVersionedAsync("order-b", new OrderView { CustomerName = "B" }, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var a = await store.GetVersionedAsync("order-a", CancellationToken.None).ConfigureAwait(false);
		var b = await store.GetVersionedAsync("order-b", CancellationToken.None).ConfigureAwait(false);
		a.ShouldNotBeNull();
		b.ShouldNotBeNull();
		a.Version.ShouldBe(1);
		b.Version.ShouldBe(1);
	}
}

/// <summary>
/// Test projection for versioned store tests.
/// </summary>
internal sealed class OrderView
{
	public string? CustomerName { get; set; }
	public decimal Total { get; set; }
}

/// <summary>
/// In-memory implementation of <see cref="IVersionedProjectionStore{TProjection}"/>
/// for testing the contract without SqlServer dependency.
/// </summary>
internal sealed class InMemoryVersionedProjectionStore<T> : IVersionedProjectionStore<T>
	where T : class
{
	private readonly Dictionary<string, (T Projection, long Version)> _store = new(StringComparer.Ordinal);

	public Task<VersionedProjection<T>?> GetVersionedAsync(string id, CancellationToken cancellationToken)
	{
		if (_store.TryGetValue(id, out var entry))
		{
			return Task.FromResult<VersionedProjection<T>?>(
				new VersionedProjection<T>(entry.Projection, entry.Version));
		}

		return Task.FromResult<VersionedProjection<T>?>(null);
	}

	public Task UpsertVersionedAsync(string id, T projection, long? expectedVersion, CancellationToken cancellationToken)
	{
		if (expectedVersion is null)
		{
			// Initial insert — no concurrency check
			_store[id] = (projection, 1);
			return Task.CompletedTask;
		}

		if (!_store.TryGetValue(id, out var existing))
		{
			return Task.FromException(new ConcurrencyException(
				$"Projection '{id}' not found for expected version {expectedVersion}."));
		}

		if (existing.Version != expectedVersion.Value)
		{
			return Task.FromException(new ConcurrencyException(
				$"Version mismatch for projection '{id}': expected {expectedVersion}, actual {existing.Version}."));
		}

		_store[id] = (projection, existing.Version + 1);
		return Task.CompletedTask;
	}

	// Base IProjectionStore<T> methods

	public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		if (_store.TryGetValue(id, out var entry))
		{
			return Task.FromResult<T?>(entry.Projection);
		}

		return Task.FromResult<T?>(null);
	}

	public Task UpsertAsync(string id, T projection, CancellationToken cancellationToken)
	{
		if (_store.TryGetValue(id, out var existing))
		{
			_store[id] = (projection, existing.Version + 1);
		}
		else
		{
			_store[id] = (projection, 1);
		}

		return Task.CompletedTask;
	}

	public Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		_store.Remove(id);
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<T>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
		=> Task.FromResult<IReadOnlyList<T>>(_store.Values.Select(e => e.Projection).ToList());

	public Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
		=> Task.FromResult((long)_store.Count);
}
