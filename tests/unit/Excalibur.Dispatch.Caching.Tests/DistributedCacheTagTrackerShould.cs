// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="DistributedCacheTagTracker"/> (Sprint 723 T.3 wfga2r).
/// Uses MemoryDistributedCache as backing store for deterministic testing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DistributedCacheTagTrackerShould
{
	private static DistributedCacheTagTracker CreateTracker(
		IDistributedCache? cache = null,
		int capacity = 10_000,
		TimeSpan? defaultExpiration = null)
	{
		var distCache = cache ?? new MemoryDistributedCache(
			Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Behavior = { DefaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(5) },
			TagTrackerCapacity = capacity
		});
		return new DistributedCacheTagTracker(distCache, options);
	}

	#region RegisterKeyAsync + GetKeysByTagsAsync

	[Fact]
	public async Task RegisterAndRetrieveKeysByTag()
	{
		// Arrange
		var tracker = CreateTracker();

		// Act
		await tracker.RegisterKeyAsync("key1", ["tagA", "tagB"], CancellationToken.None);

		// Assert
		var keysA = await tracker.GetKeysByTagsAsync(["tagA"], CancellationToken.None);
		var keysB = await tracker.GetKeysByTagsAsync(["tagB"], CancellationToken.None);
		keysA.ShouldContain("key1");
		keysB.ShouldContain("key1");
	}

	[Fact]
	public async Task ReturnUnionOfKeysForMultipleTags()
	{
		// Arrange
		var tracker = CreateTracker();
		await tracker.RegisterKeyAsync("key1", ["tagA"], CancellationToken.None);
		await tracker.RegisterKeyAsync("key2", ["tagB"], CancellationToken.None);

		// Act
		var keys = await tracker.GetKeysByTagsAsync(["tagA", "tagB"], CancellationToken.None);

		// Assert
		keys.ShouldContain("key1");
		keys.ShouldContain("key2");
	}

	[Fact]
	public async Task ReturnEmptyForNonExistentTag()
	{
		// Arrange
		var tracker = CreateTracker();

		// Act
		var keys = await tracker.GetKeysByTagsAsync(["nonexistent"], CancellationToken.None);

		// Assert
		keys.ShouldBeEmpty();
	}

	[Fact]
	public async Task HandleEmptyTags_GracefullyOnRegister()
	{
		// Arrange
		var tracker = CreateTracker();

		// Act & Assert -- should not throw
		await tracker.RegisterKeyAsync("key1", [], CancellationToken.None);
		var keys = await tracker.GetKeysByTagsAsync(["anyTag"], CancellationToken.None);
		keys.ShouldBeEmpty();
	}

	[Fact]
	public async Task HandleNullTags_GracefullyOnRegister()
	{
		// Arrange
		var tracker = CreateTracker();

		// Act & Assert -- should not throw
		await tracker.RegisterKeyAsync("key1", null!, CancellationToken.None);
	}

	[Fact]
	public async Task HandleEmptyTags_GracefullyOnGet()
	{
		// Arrange
		var tracker = CreateTracker();

		// Act
		var keys = await tracker.GetKeysByTagsAsync([], CancellationToken.None);

		// Assert
		keys.ShouldBeEmpty();
	}

	[Fact]
	public async Task HandleNullTags_GracefullyOnGet()
	{
		// Arrange
		var tracker = CreateTracker();

		// Act
		var keys = await tracker.GetKeysByTagsAsync(null!, CancellationToken.None);

		// Assert
		keys.ShouldBeEmpty();
	}

	#endregion

	#region UnregisterKeyAsync

	[Fact]
	public async Task UnregisterKey_RemovesFromAllTags()
	{
		// Arrange
		var tracker = CreateTracker();
		await tracker.RegisterKeyAsync("key1", ["tagA", "tagB"], CancellationToken.None);

		// Act
		await tracker.UnregisterKeyAsync("key1", CancellationToken.None);

		// Assert
		var keysA = await tracker.GetKeysByTagsAsync(["tagA"], CancellationToken.None);
		var keysB = await tracker.GetKeysByTagsAsync(["tagB"], CancellationToken.None);
		keysA.ShouldNotContain("key1");
		keysB.ShouldNotContain("key1");
	}

	[Fact]
	public async Task UnregisterNonExistentKey_DoesNotThrow()
	{
		// Arrange
		var tracker = CreateTracker();

		// Act & Assert -- should not throw
		await tracker.UnregisterKeyAsync("nonexistent", CancellationToken.None);
	}

	#endregion

	#region Constructor validation

	[Fact]
	public void ThrowArgumentNullException_WhenCacheIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DistributedCacheTagTracker(null!, Microsoft.Extensions.Options.Options.Create(new CacheOptions())));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		var cache = new MemoryDistributedCache(
			Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
		Should.Throw<ArgumentNullException>(() =>
			new DistributedCacheTagTracker(cache, null!));
	}

	[Fact]
	public void UseDefaultTtl_WhenExpirationIsZero()
	{
		// Should not throw -- uses fallback 20 min TTL
		var tracker = CreateTracker(defaultExpiration: TimeSpan.Zero);
		tracker.ShouldNotBeNull();
	}

	#endregion

	#region Re-registration

	[Fact]
	public async Task ReRegister_ShouldReplaceTags()
	{
		// Arrange
		var tracker = CreateTracker();
		await tracker.RegisterKeyAsync("key1", ["tagA", "tagB"], CancellationToken.None);

		// Act -- re-register with different tags
		await tracker.RegisterKeyAsync("key1", ["tagC"], CancellationToken.None);

		// Assert -- key should be in tagC
		var keysC = await tracker.GetKeysByTagsAsync(["tagC"], CancellationToken.None);
		keysC.ShouldContain("key1");
	}

	#endregion
}
