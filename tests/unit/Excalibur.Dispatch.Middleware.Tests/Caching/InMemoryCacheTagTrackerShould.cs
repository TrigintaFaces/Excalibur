// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

[Trait("Category", "Unit")]
public sealed class InMemoryCacheTagTrackerShould : UnitTestBase
{
	[Fact]
	public async Task RegisterKeyAsync_WithTags_AssociatesKeyWithTags()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act
		await tracker.RegisterKeyAsync("key1", ["tag-a", "tag-b"], CancellationToken.None);

		// Assert
		var keys = await tracker.GetKeysByTagsAsync(["tag-a"], CancellationToken.None);
		keys.ShouldContain("key1");
	}

	[Fact]
	public async Task RegisterKeyAsync_WithNullTags_DoesNotThrow()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act & Assert — should not throw
		await tracker.RegisterKeyAsync("key1", null!, CancellationToken.None);
	}

	[Fact]
	public async Task RegisterKeyAsync_WithEmptyTags_DoesNotThrow()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act & Assert
		await tracker.RegisterKeyAsync("key1", [], CancellationToken.None);
	}

	[Fact]
	public async Task GetKeysByTagsAsync_WithRegisteredTag_ReturnsMatchingKeys()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		await tracker.RegisterKeyAsync("key1", ["tag-a"], CancellationToken.None);
		await tracker.RegisterKeyAsync("key2", ["tag-a", "tag-b"], CancellationToken.None);
		await tracker.RegisterKeyAsync("key3", ["tag-b"], CancellationToken.None);

		// Act
		var result = await tracker.GetKeysByTagsAsync(["tag-a"], CancellationToken.None);

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContain("key1");
		result.ShouldContain("key2");
	}

	[Fact]
	public async Task GetKeysByTagsAsync_WithMultipleTags_ReturnsUnion()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		await tracker.RegisterKeyAsync("key1", ["tag-a"], CancellationToken.None);
		await tracker.RegisterKeyAsync("key2", ["tag-b"], CancellationToken.None);

		// Act
		var result = await tracker.GetKeysByTagsAsync(["tag-a", "tag-b"], CancellationToken.None);

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContain("key1");
		result.ShouldContain("key2");
	}

	[Fact]
	public async Task GetKeysByTagsAsync_WithUnknownTag_ReturnsEmpty()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		await tracker.RegisterKeyAsync("key1", ["tag-a"], CancellationToken.None);

		// Act
		var result = await tracker.GetKeysByTagsAsync(["unknown-tag"], CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetKeysByTagsAsync_WithNullTags_ReturnsEmpty()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act
		var result = await tracker.GetKeysByTagsAsync(null!, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetKeysByTagsAsync_WithEmptyTags_ReturnsEmpty()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act
		var result = await tracker.GetKeysByTagsAsync([], CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task UnregisterKeyAsync_RemovesKeyFromAllTags()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		await tracker.RegisterKeyAsync("key1", ["tag-a", "tag-b"], CancellationToken.None);
		await tracker.RegisterKeyAsync("key2", ["tag-a"], CancellationToken.None);

		// Act
		await tracker.UnregisterKeyAsync("key1", CancellationToken.None);

		// Assert
		var tagAKeys = await tracker.GetKeysByTagsAsync(["tag-a"], CancellationToken.None);
		tagAKeys.ShouldContain("key2");
		tagAKeys.ShouldNotContain("key1");

		var tagBKeys = await tracker.GetKeysByTagsAsync(["tag-b"], CancellationToken.None);
		tagBKeys.ShouldBeEmpty();
	}

	[Fact]
	public async Task UnregisterKeyAsync_WithUnknownKey_DoesNotThrow()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act & Assert
		await tracker.UnregisterKeyAsync("nonexistent", CancellationToken.None);
	}

	[Fact]
	public async Task UnregisterKeyAsync_CleansUpEmptyTagEntries()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		await tracker.RegisterKeyAsync("key1", ["tag-a"], CancellationToken.None);

		// Act — remove the only key for tag-a
		await tracker.UnregisterKeyAsync("key1", CancellationToken.None);

		// Assert — tag-a should return empty
		var result = await tracker.GetKeysByTagsAsync(["tag-a"], CancellationToken.None);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task RegisterKeyAsync_OverwritesExistingKeyTags()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		await tracker.RegisterKeyAsync("key1", ["tag-a"], CancellationToken.None);

		// Act — re-register with different tags
		await tracker.RegisterKeyAsync("key1", ["tag-b"], CancellationToken.None);

		// Assert — key1 should now be associated with tag-b
		var tagBKeys = await tracker.GetKeysByTagsAsync(["tag-b"], CancellationToken.None);
		tagBKeys.ShouldContain("key1");
	}
}
