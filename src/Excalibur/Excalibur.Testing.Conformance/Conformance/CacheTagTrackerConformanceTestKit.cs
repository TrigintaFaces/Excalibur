// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Caching;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for validating <see cref="ICacheTagTracker"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// This test kit ensures all <see cref="ICacheTagTracker"/> implementations correctly implement
/// the tag tracking contract for cache invalidation in scenarios where the underlying cache
/// doesn't natively support tag-based invalidation.
/// </para>
/// <para>
/// <strong>CACHING INFRASTRUCTURE PATTERN:</strong> ICacheTagTracker provides bi-directional
/// mapping between cache keys and tags for tag-based cache invalidation.
/// </para>
/// <para>
/// <strong>KEY PATTERN:</strong> TAG-TRACKING - Register key with tags, query keys by tags, unregister key.
/// Unlike DUPLICATE-CHECK (Deduplicator) or ROUND-TRIP (ClaimCheckProvider), this pattern validates
/// bi-directional mapping between keys and tags.
/// </para>
/// <para>
/// <strong>METHODS TESTED (3 methods - SIMPLEST kit!):</strong>
/// <list type="bullet">
/// <item><description><c>RegisterKeyAsync</c> - Register cache key with associated tags</description></item>
/// <item><description><c>GetKeysByTagsAsync</c> - Get all keys for specified tags (UNION)</description></item>
/// <item><description><c>UnregisterKeyAsync</c> - Remove key-to-tag mappings</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>NO SYNC METHODS:</strong> This is the first enterprise integration kit without a sync method.
/// All methods are Task-based.
/// </para>
/// <para>
/// <strong>GRACEFUL HANDLING:</strong> Empty/null tags return empty results or no-op - no exceptions.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MyCacheTagTrackerConformanceTests : CacheTagTrackerConformanceTestKit
/// {
///     protected override ICacheTagTracker CreateTracker() =>
///         new MyCacheTagTracker();
///
///     [Fact]
///     public Task RegisterKeyAsync_WithTags_ShouldRegister_Test() =>
///         RegisterKeyAsync_WithTags_ShouldRegister();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class CacheTagTrackerConformanceTestKit
{
	/// <summary>
	/// Creates a new instance of the cache tag tracker for testing.
	/// </summary>
	/// <returns>A new tracker instance.</returns>
	/// <remarks>
	/// Each test should get a fresh tracker instance to ensure isolation.
	/// Implementers should create a new instance with default configuration.
	/// </remarks>
	protected abstract ICacheTagTracker CreateTracker();

	#region RegisterKeyAsync Tests

	/// <summary>
	/// Verifies that <c>RegisterKeyAsync</c> with valid tags registers the key correctly.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RegisterKeyAsync_WithTags_ShouldRegister()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var key = "user:123";
		var tags = new[] { "users", "tenant:abc" };
		var queryTags = new[] { "users" };

		// Act
		await tracker.RegisterKeyAsync(key, tags, cts.Token).ConfigureAwait(false);
		var keys = await tracker.GetKeysByTagsAsync(queryTags, cts.Token).ConfigureAwait(false);

		// Assert
		if (!keys.Contains(key))
		{
			throw new TestFixtureAssertionException(
				$"Expected GetKeysByTagsAsync to return key '{key}' after registration");
		}
	}

	/// <summary>
	/// Verifies that <c>RegisterKeyAsync</c> with empty tags is a no-op.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RegisterKeyAsync_EmptyTags_ShouldBeNoOp()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var key = "user:123";
		var queryTags = new[] { "any-tag" };

		// Act - should not throw
		await tracker.RegisterKeyAsync(key, [], cts.Token).ConfigureAwait(false);

		// Assert - key should not be found for any tag
		var keys = await tracker.GetKeysByTagsAsync(queryTags, cts.Token).ConfigureAwait(false);
		if (keys.Contains(key))
		{
			throw new TestFixtureAssertionException(
				"Expected empty tags registration to be no-op, but key was found");
		}
	}

	/// <summary>
	/// Verifies that <c>RegisterKeyAsync</c> with null tags is a no-op.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RegisterKeyAsync_NullTags_ShouldBeNoOp()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var key = "user:123";
		var queryTags = new[] { "any-tag" };

		// Act - should not throw
		await tracker.RegisterKeyAsync(key, null!, cts.Token).ConfigureAwait(false);

		// Assert - key should not be found for any tag
		var keys = await tracker.GetKeysByTagsAsync(queryTags, cts.Token).ConfigureAwait(false);
		if (keys.Contains(key))
		{
			throw new TestFixtureAssertionException(
				"Expected null tags registration to be no-op, but key was found");
		}
	}

	/// <summary>
	/// Verifies that re-registering a key replaces its tags.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RegisterKeyAsync_ReRegister_ShouldReplaceTags()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var key = "user:123";
		var firstTags = new[] { "users", "premium" };
		var secondTags = new[] { "users", "basic" };
		var queryUsersTags = new[] { "users" };
		var queryBasicTags = new[] { "basic" };

		// Act - Register with first set of tags
		await tracker.RegisterKeyAsync(key, firstTags, cts.Token).ConfigureAwait(false);

		// Re-register with different tags
		await tracker.RegisterKeyAsync(key, secondTags, cts.Token).ConfigureAwait(false);

		// Assert - key should be in "users" and "basic" but not "premium"
		var usersKeys = await tracker.GetKeysByTagsAsync(queryUsersTags, cts.Token).ConfigureAwait(false);
		var basicKeys = await tracker.GetKeysByTagsAsync(queryBasicTags, cts.Token).ConfigureAwait(false);

		if (!usersKeys.Contains(key))
		{
			throw new TestFixtureAssertionException(
				"Expected key to be in 'users' tag after re-registration");
		}

		if (!basicKeys.Contains(key))
		{
			throw new TestFixtureAssertionException(
				"Expected key to be in 'basic' tag after re-registration");
		}

		// Note: The old "premium" tag mapping may still exist until unregister
		// This is implementation-specific behavior
	}

	#endregion

	#region GetKeysByTagsAsync Tests

	/// <summary>
	/// Verifies that <c>GetKeysByTagsAsync</c> returns keys for a single tag.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task GetKeysByTagsAsync_SingleTag_ShouldReturnKeys()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var usersTags = new[] { "users" };
		var ordersTags = new[] { "orders" };

		await tracker.RegisterKeyAsync("user:1", usersTags, cts.Token).ConfigureAwait(false);
		await tracker.RegisterKeyAsync("user:2", usersTags, cts.Token).ConfigureAwait(false);
		await tracker.RegisterKeyAsync("order:1", ordersTags, cts.Token).ConfigureAwait(false);

		// Act
		var keys = await tracker.GetKeysByTagsAsync(usersTags, cts.Token).ConfigureAwait(false);

		// Assert
		if (!keys.Contains("user:1") || !keys.Contains("user:2"))
		{
			throw new TestFixtureAssertionException(
				"Expected GetKeysByTagsAsync to return both user keys for 'users' tag");
		}

		if (keys.Contains("order:1"))
		{
			throw new TestFixtureAssertionException(
				"Expected GetKeysByTagsAsync to NOT return order key for 'users' tag");
		}
	}

	/// <summary>
	/// Verifies that <c>GetKeysByTagsAsync</c> returns UNION of keys for multiple tags.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task GetKeysByTagsAsync_MultipleTags_ShouldReturnUnion()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var usersTags = new[] { "users" };
		var ordersTags = new[] { "orders" };
		var usersPremiumTags = new[] { "users", "premium" };
		var queryTags = new[] { "users", "orders" };

		await tracker.RegisterKeyAsync("user:1", usersTags, cts.Token).ConfigureAwait(false);
		await tracker.RegisterKeyAsync("order:1", ordersTags, cts.Token).ConfigureAwait(false);
		await tracker.RegisterKeyAsync("user:2", usersPremiumTags, cts.Token).ConfigureAwait(false);

		// Act - Get keys for multiple tags (should be UNION)
		var keys = await tracker.GetKeysByTagsAsync(queryTags, cts.Token).ConfigureAwait(false);

		// Assert - should contain all keys from both tags
		if (!keys.Contains("user:1") || !keys.Contains("user:2") || !keys.Contains("order:1"))
		{
			throw new TestFixtureAssertionException(
				$"Expected GetKeysByTagsAsync to return UNION of keys, got {keys.Count} keys");
		}
	}

	/// <summary>
	/// Verifies that <c>GetKeysByTagsAsync</c> with empty tags returns empty result.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task GetKeysByTagsAsync_EmptyTags_ShouldReturnEmpty()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var usersTags = new[] { "users" };

		await tracker.RegisterKeyAsync("user:1", usersTags, cts.Token).ConfigureAwait(false);

		// Act
		var keys = await tracker.GetKeysByTagsAsync([], cts.Token).ConfigureAwait(false);

		// Assert
		if (keys.Count != 0)
		{
			throw new TestFixtureAssertionException(
				"Expected GetKeysByTagsAsync with empty tags to return empty HashSet");
		}
	}

	/// <summary>
	/// Verifies that <c>GetKeysByTagsAsync</c> with null tags returns empty result.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task GetKeysByTagsAsync_NullTags_ShouldReturnEmpty()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var usersTags = new[] { "users" };

		await tracker.RegisterKeyAsync("user:1", usersTags, cts.Token).ConfigureAwait(false);

		// Act
		var keys = await tracker.GetKeysByTagsAsync(null!, cts.Token).ConfigureAwait(false);

		// Assert
		if (keys.Count != 0)
		{
			throw new TestFixtureAssertionException(
				"Expected GetKeysByTagsAsync with null tags to return empty HashSet");
		}
	}

	/// <summary>
	/// Verifies that <c>GetKeysByTagsAsync</c> for non-existent tag returns empty result.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task GetKeysByTagsAsync_NonExistentTag_ShouldReturnEmpty()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var usersTags = new[] { "users" };
		var queryTags = new[] { "non-existent-tag" };

		await tracker.RegisterKeyAsync("user:1", usersTags, cts.Token).ConfigureAwait(false);

		// Act
		var keys = await tracker.GetKeysByTagsAsync(queryTags, cts.Token).ConfigureAwait(false);

		// Assert
		if (keys.Count != 0)
		{
			throw new TestFixtureAssertionException(
				"Expected GetKeysByTagsAsync for non-existent tag to return empty HashSet");
		}
	}

	#endregion

	#region UnregisterKeyAsync Tests

	/// <summary>
	/// Verifies that <c>UnregisterKeyAsync</c> removes key from all associated tags.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task UnregisterKeyAsync_ShouldRemoveFromAllTags()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var key = "user:123";
		var registerTags = new[] { "users", "premium", "tenant:abc" };
		var queryUsersTags = new[] { "users" };
		var queryPremiumTags = new[] { "premium" };
		var queryTenantTags = new[] { "tenant:abc" };

		await tracker.RegisterKeyAsync(key, registerTags, cts.Token).ConfigureAwait(false);

		// Act
		await tracker.UnregisterKeyAsync(key, cts.Token).ConfigureAwait(false);

		// Assert - key should not be found in any tag
		var usersKeys = await tracker.GetKeysByTagsAsync(queryUsersTags, cts.Token).ConfigureAwait(false);
		var premiumKeys = await tracker.GetKeysByTagsAsync(queryPremiumTags, cts.Token).ConfigureAwait(false);
		var tenantKeys = await tracker.GetKeysByTagsAsync(queryTenantTags, cts.Token).ConfigureAwait(false);

		if (usersKeys.Contains(key) || premiumKeys.Contains(key) || tenantKeys.Contains(key))
		{
			throw new TestFixtureAssertionException(
				"Expected UnregisterKeyAsync to remove key from ALL tags");
		}
	}

	/// <summary>
	/// Verifies that <c>UnregisterKeyAsync</c> for non-existent key is safe (no exception).
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task UnregisterKeyAsync_NonExistentKey_ShouldBeNoOp()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();

		// Act - should not throw
		await tracker.UnregisterKeyAsync("non-existent-key", cts.Token).ConfigureAwait(false);

		// Assert - just verify no exception was thrown
		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Verifies that <c>UnregisterKeyAsync</c> cleans up empty tag entries.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task UnregisterKeyAsync_ShouldCleanupEmptyTagEntries()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var uniqueTags = new[] { "unique-tag" };

		// Register single key with a unique tag
		await tracker.RegisterKeyAsync("single-key", uniqueTags, cts.Token).ConfigureAwait(false);

		// Verify it's registered
		var beforeKeys = await tracker.GetKeysByTagsAsync(uniqueTags, cts.Token).ConfigureAwait(false);
		if (!beforeKeys.Contains("single-key"))
		{
			throw new TestFixtureAssertionException("Expected key to be registered before unregister");
		}

		// Act - Unregister the only key for this tag
		await tracker.UnregisterKeyAsync("single-key", cts.Token).ConfigureAwait(false);

		// Assert - tag should return empty (not error)
		var afterKeys = await tracker.GetKeysByTagsAsync(uniqueTags, cts.Token).ConfigureAwait(false);
		if (afterKeys.Count != 0)
		{
			throw new TestFixtureAssertionException(
				"Expected tag entry to be cleaned up after last key unregistered");
		}
	}

	#endregion

	#region Edge Case Tests

	/// <summary>
	/// Verifies that a key can be registered with multiple tags.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RegisterKeyAsync_MultipleTags_ShouldBeFoundInAll()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var key = "user:premium:vip";
		var tags = new[] { "users", "premium", "vip", "tenant:abc" };

		// Act
		await tracker.RegisterKeyAsync(key, tags, cts.Token).ConfigureAwait(false);

		// Assert - key should be found in each tag individually
		foreach (var tag in tags)
		{
			var queryTags = new[] { tag };
			var keys = await tracker.GetKeysByTagsAsync(queryTags, cts.Token).ConfigureAwait(false);
			if (!keys.Contains(key))
			{
				throw new TestFixtureAssertionException(
					$"Expected key to be found in tag '{tag}'");
			}
		}
	}

	#endregion
}
