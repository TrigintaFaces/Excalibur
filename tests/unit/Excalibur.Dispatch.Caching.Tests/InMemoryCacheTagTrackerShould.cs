// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class InMemoryCacheTagTrackerShould
{
	[Fact]
	public async Task RegisterKey_WithTags()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act
		await tracker.RegisterKeyAsync("key1", ["tag-a", "tag-b"], CancellationToken.None).ConfigureAwait(false);
		var keys = await tracker.GetKeysByTagsAsync(["tag-a"], CancellationToken.None).ConfigureAwait(false);

		// Assert
		keys.ShouldContain("key1");
	}

	[Fact]
	public async Task RegisterKey_WithEmptyTags_ShouldNotThrow()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act & Assert - should not throw
		await tracker.RegisterKeyAsync("key1", [], CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task RegisterKey_WithNullTags_ShouldNotThrow()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act & Assert - should not throw
		await tracker.RegisterKeyAsync("key1", null!, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task GetKeysByTags_ReturnEmpty_WhenNoKeysRegistered()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act
		var keys = await tracker.GetKeysByTagsAsync(["tag-a"], CancellationToken.None).ConfigureAwait(false);

		// Assert
		keys.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetKeysByTags_ReturnEmpty_WhenNullTags()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act
		var keys = await tracker.GetKeysByTagsAsync(null!, CancellationToken.None).ConfigureAwait(false);

		// Assert
		keys.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetKeysByTags_ReturnEmpty_WhenEmptyTags()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act
		var keys = await tracker.GetKeysByTagsAsync([], CancellationToken.None).ConfigureAwait(false);

		// Assert
		keys.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetKeysByTags_ReturnMultipleKeys_ForSameTag()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		await tracker.RegisterKeyAsync("key1", ["shared-tag"], CancellationToken.None).ConfigureAwait(false);
		await tracker.RegisterKeyAsync("key2", ["shared-tag"], CancellationToken.None).ConfigureAwait(false);
		await tracker.RegisterKeyAsync("key3", ["other-tag"], CancellationToken.None).ConfigureAwait(false);

		// Act
		var keys = await tracker.GetKeysByTagsAsync(["shared-tag"], CancellationToken.None).ConfigureAwait(false);

		// Assert
		keys.Count.ShouldBe(2);
		keys.ShouldContain("key1");
		keys.ShouldContain("key2");
	}

	[Fact]
	public async Task GetKeysByTags_ReturnUnion_ForMultipleTags()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		await tracker.RegisterKeyAsync("key1", ["tag-a"], CancellationToken.None).ConfigureAwait(false);
		await tracker.RegisterKeyAsync("key2", ["tag-b"], CancellationToken.None).ConfigureAwait(false);

		// Act
		var keys = await tracker.GetKeysByTagsAsync(["tag-a", "tag-b"], CancellationToken.None).ConfigureAwait(false);

		// Assert
		keys.Count.ShouldBe(2);
		keys.ShouldContain("key1");
		keys.ShouldContain("key2");
	}

	[Fact]
	public async Task UnregisterKey_RemovesKeyFromAllTags()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		await tracker.RegisterKeyAsync("key1", ["tag-a", "tag-b"], CancellationToken.None).ConfigureAwait(false);

		// Act
		await tracker.UnregisterKeyAsync("key1", CancellationToken.None).ConfigureAwait(false);
		var keysA = await tracker.GetKeysByTagsAsync(["tag-a"], CancellationToken.None).ConfigureAwait(false);
		var keysB = await tracker.GetKeysByTagsAsync(["tag-b"], CancellationToken.None).ConfigureAwait(false);

		// Assert
		keysA.ShouldBeEmpty();
		keysB.ShouldBeEmpty();
	}

	[Fact]
	public async Task UnregisterKey_DoesNotAffectOtherKeys()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		await tracker.RegisterKeyAsync("key1", ["tag-a"], CancellationToken.None).ConfigureAwait(false);
		await tracker.RegisterKeyAsync("key2", ["tag-a"], CancellationToken.None).ConfigureAwait(false);

		// Act
		await tracker.UnregisterKeyAsync("key1", CancellationToken.None).ConfigureAwait(false);
		var keys = await tracker.GetKeysByTagsAsync(["tag-a"], CancellationToken.None).ConfigureAwait(false);

		// Assert
		keys.Count.ShouldBe(1);
		keys.ShouldContain("key2");
	}

	[Fact]
	public async Task UnregisterKey_CleansUpEmptyTagEntries()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		await tracker.RegisterKeyAsync("key1", ["unique-tag"], CancellationToken.None).ConfigureAwait(false);

		// Act
		await tracker.UnregisterKeyAsync("key1", CancellationToken.None).ConfigureAwait(false);
		var keys = await tracker.GetKeysByTagsAsync(["unique-tag"], CancellationToken.None).ConfigureAwait(false);

		// Assert
		keys.ShouldBeEmpty();
	}

	[Fact]
	public async Task UnregisterKey_HandlesNonExistentKey()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();

		// Act & Assert - should not throw
		await tracker.UnregisterKeyAsync("nonexistent", CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public void ConstructWithMeterFactory()
	{
		// Arrange
		var meterFactory = A.Fake<IMeterFactory>();
		var meter = new Meter("test");
		A.CallTo(() => meterFactory.Create(A<MeterOptions>._)).Returns(meter);

		// Act
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		// Assert
		tracker.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenMeterFactoryIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new InMemoryCacheTagTracker(null!));
	}

	[Fact]
	public async Task SupportConcurrentAccess()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker();
		var tasks = new List<Task>();

		// Act
		for (var i = 0; i < 10; i++)
		{
			var idx = i;
			tasks.Add(Task.Run(async () =>
			{
				for (var j = 0; j < 50; j++)
				{
					await tracker.RegisterKeyAsync($"key-{idx}-{j}", [$"tag-{idx}"], CancellationToken.None).ConfigureAwait(false);
				}
			}));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		for (var i = 0; i < 10; i++)
		{
			var keys = await tracker.GetKeysByTagsAsync([$"tag-{i}"], CancellationToken.None).ConfigureAwait(false);
			keys.Count.ShouldBe(50);
		}
	}
}
