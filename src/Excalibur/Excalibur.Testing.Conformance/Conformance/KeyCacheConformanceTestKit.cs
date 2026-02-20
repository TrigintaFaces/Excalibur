// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IKeyCache conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateCache"/> to verify that
/// your key cache implementation conforms to the IKeyCache contract.
/// </para>
/// <para>
/// The test kit verifies core caching operations including:
/// <list type="bullet">
/// <item><description>Cache-aside pattern via GetOrAddAsync (factory called once, cached on second call)</description></item>
/// <item><description>Synchronous lookup via TryGet</description></item>
/// <item><description>Manual cache population via Set</description></item>
/// <item><description>Cache invalidation via Remove, Invalidate, and Clear</description></item>
/// <item><description>Count property tracking</description></item>
/// <item><description>Null parameter validation (ArgumentNullException)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>FIRST MIXED SYNC/ASYNC KIT:</strong> IKeyCache includes both synchronous methods
/// (TryGet, Set, Remove, Invalidate, Clear) and asynchronous methods (GetOrAddAsync).
/// </para>
/// <para>
/// <strong>FIRST PROPERTY TESTING:</strong> The Count property verifies cache entry tracking.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class KeyCacheConformanceTests : KeyCacheConformanceTestKit
/// {
///     protected override IKeyCache CreateCache()
///     {
///         return new KeyCache(); // Uses default options
///     }
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class KeyCacheConformanceTestKit
{
	/// <summary>
	/// Creates a fresh key cache instance for testing.
	/// </summary>
	/// <returns>An IKeyCache implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// The simplest implementation uses the parameterless KeyCache constructor:
	/// <code>return new KeyCache();</code>
	/// </para>
	/// </remarks>
	protected abstract IKeyCache CreateCache();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	protected virtual void Cleanup()
	{
	}

	/// <summary>
	/// Generates a unique key ID for test isolation.
	/// </summary>
	/// <returns>A unique key identifier.</returns>
	protected virtual string GenerateKeyId() => $"test-key-{Guid.NewGuid():N}";

	/// <summary>
	/// Creates a test KeyMetadata instance.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <returns>A KeyMetadata instance for testing.</returns>
	protected virtual KeyMetadata CreateTestKeyMetadata(string keyId) => new()
	{
		KeyId = keyId,
		Version = 1,
		Status = KeyStatus.Active,
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
		CreatedAt = DateTimeOffset.UtcNow,
	};

	#region GetOrAddAsync Tests

	/// <summary>
	/// Verifies that GetOrAddAsync throws ArgumentNullException for null keyId.
	/// </summary>
	protected virtual async Task GetOrAddAsync_NullKeyId_ShouldThrowArgumentNullException()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await cache.GetOrAddAsync(
					null!,
					(_, _) => Task.FromResult<KeyMetadata?>(null),
					CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected GetOrAddAsync to throw ArgumentNullException for null keyId.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that GetOrAddAsync throws ArgumentNullException for null factory.
	/// </summary>
	protected virtual async Task GetOrAddAsync_NullFactory_ShouldThrowArgumentNullException()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			var keyId = GenerateKeyId();

			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await cache.GetOrAddAsync(
					keyId,
					null!,
					CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected GetOrAddAsync to throw ArgumentNullException for null factory.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that GetOrAddAsync calls factory and caches result.
	/// </summary>
	protected virtual async Task GetOrAddAsync_CacheMiss_ShouldCallFactoryAndCache()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			var keyId = GenerateKeyId();
			var metadata = CreateTestKeyMetadata(keyId);
			var factoryCallCount = 0;

			Task<KeyMetadata?> Factory(string id, CancellationToken ct)
			{
				factoryCallCount++;
				return Task.FromResult<KeyMetadata?>(metadata);
			}

			// Act
			var result = await cache.GetOrAddAsync(keyId, Factory, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result is null)
			{
				throw new TestFixtureAssertionException("Expected GetOrAddAsync to return metadata.");
			}

			if (result.KeyId != keyId)
			{
				throw new TestFixtureAssertionException(
					$"Expected KeyId to be '{keyId}', but got '{result.KeyId}'.");
			}

			if (factoryCallCount != 1)
			{
				throw new TestFixtureAssertionException(
					$"Expected factory to be called exactly once, but was called {factoryCallCount} times.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that GetOrAddAsync returns cached value without calling factory on second call.
	/// </summary>
	protected virtual async Task GetOrAddAsync_CacheHit_ShouldNotCallFactory()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			var keyId = GenerateKeyId();
			var metadata = CreateTestKeyMetadata(keyId);
			var factoryCallCount = 0;

			Task<KeyMetadata?> Factory(string id, CancellationToken ct)
			{
				factoryCallCount++;
				return Task.FromResult<KeyMetadata?>(metadata);
			}

			// First call - should invoke factory
			_ = await cache.GetOrAddAsync(keyId, Factory, CancellationToken.None).ConfigureAwait(false);

			// Act - Second call - should use cache
			var result = await cache.GetOrAddAsync(keyId, Factory, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result is null)
			{
				throw new TestFixtureAssertionException("Expected GetOrAddAsync to return cached metadata.");
			}

			if (factoryCallCount != 1)
			{
				throw new TestFixtureAssertionException(
					$"Expected factory to be called exactly once (on first call only), but was called {factoryCallCount} times.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that GetOrAddAsync with TTL throws ArgumentNullException for null keyId.
	/// </summary>
	protected virtual async Task GetOrAddAsync_WithTtl_NullKeyId_ShouldThrowArgumentNullException()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await cache.GetOrAddAsync(
					null!,
					TimeSpan.FromMinutes(5),
					(_, _) => Task.FromResult<KeyMetadata?>(null),
					CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected GetOrAddAsync with TTL to throw ArgumentNullException for null keyId.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region TryGet Tests

	/// <summary>
	/// Verifies that TryGet throws ArgumentNullException for null keyId.
	/// </summary>
	protected virtual void TryGet_NullKeyId_ShouldThrowArgumentNullException()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = cache.TryGet(null!);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected TryGet to throw ArgumentNullException for null keyId.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that TryGet returns null for missing key.
	/// </summary>
	protected virtual void TryGet_MissingKey_ShouldReturnNull()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			var keyId = GenerateKeyId();

			// Act
			var result = cache.TryGet(keyId);

			// Assert
			if (result is not null)
			{
				throw new TestFixtureAssertionException(
					"Expected TryGet to return null for missing key.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that TryGet returns cached value after Set.
	/// </summary>
	protected virtual void TryGet_AfterSet_ShouldReturnCachedValue()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			var keyId = GenerateKeyId();
			var metadata = CreateTestKeyMetadata(keyId);
			cache.Set(metadata);

			// Act
			var result = cache.TryGet(keyId);

			// Assert
			if (result is null)
			{
				throw new TestFixtureAssertionException(
					"Expected TryGet to return cached value after Set.");
			}

			if (result.KeyId != keyId)
			{
				throw new TestFixtureAssertionException(
					$"Expected KeyId to be '{keyId}', but got '{result.KeyId}'.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region Set Tests

	/// <summary>
	/// Verifies that Set throws ArgumentNullException for null keyMetadata.
	/// </summary>
	protected virtual void Set_NullKeyMetadata_ShouldThrowArgumentNullException()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				cache.Set(null!);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected Set to throw ArgumentNullException for null keyMetadata.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that Set increases Count.
	/// </summary>
	protected virtual void Set_ShouldIncreaseCount()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			var keyId = GenerateKeyId();
			var metadata = CreateTestKeyMetadata(keyId);
			var initialCount = cache.Count;

			// Act
			cache.Set(metadata);

			// Assert
			if (cache.Count != initialCount + 1)
			{
				throw new TestFixtureAssertionException(
					$"Expected Count to increase by 1 after Set, but got Count={cache.Count} (initial={initialCount}).");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that Set with TTL throws ArgumentNullException for null keyMetadata.
	/// </summary>
	protected virtual void Set_WithTtl_NullKeyMetadata_ShouldThrowArgumentNullException()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				cache.Set(null!, TimeSpan.FromMinutes(5));
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected Set with TTL to throw ArgumentNullException for null keyMetadata.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region Remove Tests

	/// <summary>
	/// Verifies that Remove throws ArgumentNullException for null keyId.
	/// </summary>
	protected virtual void Remove_NullKeyId_ShouldThrowArgumentNullException()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				cache.Remove(null!);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected Remove to throw ArgumentNullException for null keyId.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that Remove removes cached entry.
	/// </summary>
	protected virtual void Remove_ExistingKey_ShouldRemoveEntry()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			var keyId = GenerateKeyId();
			var metadata = CreateTestKeyMetadata(keyId);
			cache.Set(metadata);

			// Verify entry exists
			if (cache.TryGet(keyId) is null)
			{
				throw new TestFixtureAssertionException("Setup failed: entry not cached.");
			}

			// Act
			cache.Remove(keyId);

			// Assert
			if (cache.TryGet(keyId) is not null)
			{
				throw new TestFixtureAssertionException(
					"Expected Remove to remove cached entry.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region Invalidate Tests

	/// <summary>
	/// Verifies that Invalidate throws ArgumentNullException for null keyId.
	/// </summary>
	protected virtual void Invalidate_NullKeyId_ShouldThrowArgumentNullException()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				cache.Invalidate(null!);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected Invalidate to throw ArgumentNullException for null keyId.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that Invalidate removes cached entry.
	/// </summary>
	protected virtual void Invalidate_ExistingKey_ShouldRemoveEntry()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			var keyId = GenerateKeyId();
			var metadata = CreateTestKeyMetadata(keyId);
			cache.Set(metadata);

			// Verify entry exists
			if (cache.TryGet(keyId) is null)
			{
				throw new TestFixtureAssertionException("Setup failed: entry not cached.");
			}

			// Act
			cache.Invalidate(keyId);

			// Assert
			if (cache.TryGet(keyId) is not null)
			{
				throw new TestFixtureAssertionException(
					"Expected Invalidate to remove cached entry.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region Clear Tests

	/// <summary>
	/// Verifies that Clear removes all cached entries.
	/// </summary>
	protected virtual void Clear_ShouldRemoveAllEntries()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			// Add multiple entries
			for (var i = 0; i < 3; i++)
			{
				var keyId = GenerateKeyId();
				var metadata = CreateTestKeyMetadata(keyId);
				cache.Set(metadata);
			}

			// Verify entries exist
			if (cache.Count < 3)
			{
				throw new TestFixtureAssertionException("Setup failed: entries not cached.");
			}

			// Act
			cache.Clear();

			// Assert
			if (cache.Count != 0)
			{
				throw new TestFixtureAssertionException(
					$"Expected Clear to remove all entries, but Count={cache.Count}.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region Count Tests

	/// <summary>
	/// Verifies that Count reflects actual cache state.
	/// </summary>
	protected virtual void Count_ShouldReflectCacheState()
	{
		// Arrange
		var cache = CreateCache();
		try
		{
			// Empty cache
			var initialCount = cache.Count;
			if (initialCount != 0)
			{
				throw new TestFixtureAssertionException(
					$"Expected initial Count to be 0, but got {initialCount}.");
			}

			// Add entries
			var keyId1 = GenerateKeyId();
			var keyId2 = GenerateKeyId();
			cache.Set(CreateTestKeyMetadata(keyId1));
			cache.Set(CreateTestKeyMetadata(keyId2));

			// Assert Count after adds
			if (cache.Count != 2)
			{
				throw new TestFixtureAssertionException(
					$"Expected Count to be 2 after adding 2 entries, but got {cache.Count}.");
			}

			// Remove one entry
			cache.Remove(keyId1);

			// Assert Count after remove
			if (cache.Count != 1)
			{
				throw new TestFixtureAssertionException(
					$"Expected Count to be 1 after removing 1 entry, but got {cache.Count}.");
			}
		}
		finally
		{
			Cleanup();
			(cache as IDisposable)?.Dispose();
		}
	}

	#endregion
}
