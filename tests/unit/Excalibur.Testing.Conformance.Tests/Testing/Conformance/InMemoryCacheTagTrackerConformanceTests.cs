// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryCacheTagTracker"/> validating ICacheTagTracker contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryCacheTagTracker provides bi-directional mapping between cache keys and tags for
/// tag-based cache invalidation in scenarios where the underlying cache doesn't natively support tags.
/// </para>
/// <para>
/// <strong>CACHING INFRASTRUCTURE:</strong> ICacheTagTracker implements the tag tracking pattern
/// for cache invalidation support with IMemoryCache and IDistributedCache modes.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>RegisterKeyAsync with tags registers the key correctly</description></item>
/// <item><description>RegisterKeyAsync with empty/null tags is graceful no-op</description></item>
/// <item><description>GetKeysByTagsAsync returns UNION of keys for multiple tags</description></item>
/// <item><description>GetKeysByTagsAsync with empty/null tags returns empty HashSet</description></item>
/// <item><description>UnregisterKeyAsync removes key from ALL associated tags</description></item>
/// <item><description>UnregisterKeyAsync for non-existent key is safe (no exception)</description></item>
/// <item><description>TAG-TRACKING pattern: Register -> Query -> Unregister</description></item>
/// <item><description>NO SYNC methods - all Task-based (FIRST kit without sync method)</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "CACHE")]
public class InMemoryCacheTagTrackerConformanceTests : CacheTagTrackerConformanceTestKit
{
	/// <inheritdoc />
	protected override ICacheTagTracker CreateTracker()
	{
		return new InMemoryCacheTagTracker();
	}

	#region RegisterKeyAsync Tests

	[Fact]
	public Task RegisterKeyAsync_WithTags_ShouldRegister_Test() =>
		RegisterKeyAsync_WithTags_ShouldRegister();

	[Fact]
	public Task RegisterKeyAsync_EmptyTags_ShouldBeNoOp_Test() =>
		RegisterKeyAsync_EmptyTags_ShouldBeNoOp();

	[Fact]
	public Task RegisterKeyAsync_NullTags_ShouldBeNoOp_Test() =>
		RegisterKeyAsync_NullTags_ShouldBeNoOp();

	[Fact]
	public Task RegisterKeyAsync_ReRegister_ShouldReplaceTags_Test() =>
		RegisterKeyAsync_ReRegister_ShouldReplaceTags();

	#endregion RegisterKeyAsync Tests

	#region GetKeysByTagsAsync Tests

	[Fact]
	public Task GetKeysByTagsAsync_SingleTag_ShouldReturnKeys_Test() =>
		GetKeysByTagsAsync_SingleTag_ShouldReturnKeys();

	[Fact]
	public Task GetKeysByTagsAsync_MultipleTags_ShouldReturnUnion_Test() =>
		GetKeysByTagsAsync_MultipleTags_ShouldReturnUnion();

	[Fact]
	public Task GetKeysByTagsAsync_EmptyTags_ShouldReturnEmpty_Test() =>
		GetKeysByTagsAsync_EmptyTags_ShouldReturnEmpty();

	[Fact]
	public Task GetKeysByTagsAsync_NullTags_ShouldReturnEmpty_Test() =>
		GetKeysByTagsAsync_NullTags_ShouldReturnEmpty();

	[Fact]
	public Task GetKeysByTagsAsync_NonExistentTag_ShouldReturnEmpty_Test() =>
		GetKeysByTagsAsync_NonExistentTag_ShouldReturnEmpty();

	#endregion GetKeysByTagsAsync Tests

	#region UnregisterKeyAsync Tests

	[Fact]
	public Task UnregisterKeyAsync_ShouldRemoveFromAllTags_Test() =>
		UnregisterKeyAsync_ShouldRemoveFromAllTags();

	[Fact]
	public Task UnregisterKeyAsync_NonExistentKey_ShouldBeNoOp_Test() =>
		UnregisterKeyAsync_NonExistentKey_ShouldBeNoOp();

	[Fact]
	public Task UnregisterKeyAsync_ShouldCleanupEmptyTagEntries_Test() =>
		UnregisterKeyAsync_ShouldCleanupEmptyTagEntries();

	#endregion UnregisterKeyAsync Tests

	#region Edge Case Tests

	[Fact]
	public Task RegisterKeyAsync_MultipleTags_ShouldBeFoundInAll_Test() =>
		RegisterKeyAsync_MultipleTags_ShouldBeFoundInAll();

	#endregion Edge Case Tests
}
