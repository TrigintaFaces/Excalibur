// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="KeyCache"/> validating IKeyCache contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// KeyCache provides in-memory caching for encryption key metadata with configurable TTL.
/// </para>
/// <para>
/// <strong>FIRST MIXED SYNC/ASYNC KIT:</strong> IKeyCache includes both synchronous methods
/// (TryGet, Set, Remove, Invalidate, Clear) and asynchronous methods (GetOrAddAsync).
/// </para>
/// <para>
/// <strong>FIRST PROPERTY TESTING:</strong> The Count property verifies cache entry tracking.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>GetOrAddAsync null throws ArgumentNullException (x2)</description></item>
/// <item><description>GetOrAddAsync cache-aside pattern (factory called once)</description></item>
/// <item><description>GetOrAddAsync cache hit (factory not called on second call)</description></item>
/// <item><description>GetOrAddAsync with TTL null throws ArgumentNullException</description></item>
/// <item><description>TryGet null throws ArgumentNullException</description></item>
/// <item><description>TryGet missing key returns null</description></item>
/// <item><description>TryGet after Set returns cached value</description></item>
/// <item><description>Set null throws ArgumentNullException</description></item>
/// <item><description>Set increases Count</description></item>
/// <item><description>Set with TTL null throws ArgumentNullException</description></item>
/// <item><description>Remove null throws ArgumentNullException</description></item>
/// <item><description>Remove existing key removes entry</description></item>
/// <item><description>Invalidate null throws ArgumentNullException</description></item>
/// <item><description>Invalidate existing key removes entry</description></item>
/// <item><description>Clear removes all entries</description></item>
/// <item><description>Count reflects cache state</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "CACHE")]
public sealed class KeyCacheConformanceTests : KeyCacheConformanceTestKit, IDisposable
{
	private KeyCache? _cache;

	/// <inheritdoc />
	public void Dispose()
	{
		_cache?.Dispose();
	}

	/// <inheritdoc />
	protected override IKeyCache CreateCache()
	{
		// Create key cache with default options
		_cache = new KeyCache();
		return _cache;
	}

	#region GetOrAddAsync Tests

	[Fact]
	public Task GetOrAddAsync_NullKeyId_ShouldThrowArgumentNullException_Test() =>
		GetOrAddAsync_NullKeyId_ShouldThrowArgumentNullException();

	[Fact]
	public Task GetOrAddAsync_NullFactory_ShouldThrowArgumentNullException_Test() =>
		GetOrAddAsync_NullFactory_ShouldThrowArgumentNullException();

	[Fact]
	public Task GetOrAddAsync_CacheMiss_ShouldCallFactoryAndCache_Test() =>
		GetOrAddAsync_CacheMiss_ShouldCallFactoryAndCache();

	[Fact]
	public Task GetOrAddAsync_CacheHit_ShouldNotCallFactory_Test() =>
		GetOrAddAsync_CacheHit_ShouldNotCallFactory();

	[Fact]
	public Task GetOrAddAsync_WithTtl_NullKeyId_ShouldThrowArgumentNullException_Test() =>
		GetOrAddAsync_WithTtl_NullKeyId_ShouldThrowArgumentNullException();

	#endregion GetOrAddAsync Tests

	#region TryGet Tests

	[Fact]
	public void TryGet_NullKeyId_ShouldThrowArgumentNullException_Test() =>
		TryGet_NullKeyId_ShouldThrowArgumentNullException();

	[Fact]
	public void TryGet_MissingKey_ShouldReturnNull_Test() =>
		TryGet_MissingKey_ShouldReturnNull();

	[Fact]
	public void TryGet_AfterSet_ShouldReturnCachedValue_Test() =>
		TryGet_AfterSet_ShouldReturnCachedValue();

	#endregion TryGet Tests

	#region Set Tests

	[Fact]
	public void Set_NullKeyMetadata_ShouldThrowArgumentNullException_Test() =>
		Set_NullKeyMetadata_ShouldThrowArgumentNullException();

	[Fact]
	public void Set_ShouldIncreaseCount_Test() =>
		Set_ShouldIncreaseCount();

	[Fact]
	public void Set_WithTtl_NullKeyMetadata_ShouldThrowArgumentNullException_Test() =>
		Set_WithTtl_NullKeyMetadata_ShouldThrowArgumentNullException();

	#endregion Set Tests

	#region Remove Tests

	[Fact]
	public void Remove_NullKeyId_ShouldThrowArgumentNullException_Test() =>
		Remove_NullKeyId_ShouldThrowArgumentNullException();

	[Fact]
	public void Remove_ExistingKey_ShouldRemoveEntry_Test() =>
		Remove_ExistingKey_ShouldRemoveEntry();

	#endregion Remove Tests

	#region Invalidate Tests

	[Fact]
	public void Invalidate_NullKeyId_ShouldThrowArgumentNullException_Test() =>
		Invalidate_NullKeyId_ShouldThrowArgumentNullException();

	[Fact]
	public void Invalidate_ExistingKey_ShouldRemoveEntry_Test() =>
		Invalidate_ExistingKey_ShouldRemoveEntry();

	#endregion Invalidate Tests

	#region Clear Tests

	[Fact]
	public void Clear_ShouldRemoveAllEntries_Test() =>
		Clear_ShouldRemoveAllEntries();

	#endregion Clear Tests

	#region Count Tests

	[Fact]
	public void Count_ShouldReflectCacheState_Test() =>
		Count_ShouldReflectCacheState();

	#endregion Count Tests
}
