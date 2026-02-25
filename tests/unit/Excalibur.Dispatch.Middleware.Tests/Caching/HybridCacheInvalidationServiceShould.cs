// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;
using FakeItEasy;
using Microsoft.Extensions.Caching.Hybrid;
using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for HybridCacheInvalidationService functionality.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HybridCacheInvalidationServiceShould : UnitTestBase
{
	[Fact]
	public async Task InvalidateTagsAsync_WithEmptyTags_DoesNotCallRemoveByTag()
	{
		// Arrange
		var cache = A.Fake<HybridCache>();
		var service = new HybridCacheInvalidationService(cache);
		var emptyTags = Array.Empty<string>();

		// Act
		await service.InvalidateTagsAsync(emptyTags, CancellationToken.None);

		// Assert
		A.CallTo(() => cache.RemoveByTagAsync(A<IEnumerable<string>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task InvalidateTagsAsync_WithNonEmptyTags_CallsRemoveByTag()
	{
		// Arrange
		var cache = A.Fake<HybridCache>();
		var service = new HybridCacheInvalidationService(cache);
		var tags = new[] { "tag1", "tag2" };

		// Act
		await service.InvalidateTagsAsync(tags, CancellationToken.None);

		// Assert
		A.CallTo(() => cache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(tags),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateTagsAsync_WithSingleTag_CallsRemoveByTag()
	{
		// Arrange
		var cache = A.Fake<HybridCache>();
		var service = new HybridCacheInvalidationService(cache);
		var tags = new[] { "user:123" };

		// Act
		await service.InvalidateTagsAsync(tags, CancellationToken.None);

		// Assert
		A.CallTo(() => cache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(tags),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateTagsAsync_WithCancellationToken_PassesTokenToCache()
	{
		// Arrange
		var cache = A.Fake<HybridCache>();
		var service = new HybridCacheInvalidationService(cache);
		var tags = new[] { "tag1" };
		using var cts = new CancellationTokenSource();

		// Act
		await service.InvalidateTagsAsync(tags, cts.Token);

		// Assert
		A.CallTo(() => cache.RemoveByTagAsync(
			A<IEnumerable<string>>._,
			cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateKeysAsync_WithEmptyKeys_DoesNotCallRemove()
	{
		// Arrange
		var cache = A.Fake<HybridCache>();
		var service = new HybridCacheInvalidationService(cache);
		var emptyKeys = Array.Empty<string>();

		// Act
		await service.InvalidateKeysAsync(emptyKeys, CancellationToken.None);

		// Assert
		A.CallTo(() => cache.RemoveAsync(A<IEnumerable<string>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task InvalidateKeysAsync_WithNonEmptyKeys_CallsRemove()
	{
		// Arrange
		var cache = A.Fake<HybridCache>();
		var service = new HybridCacheInvalidationService(cache);
		var keys = new[] { "key1", "key2" };

		// Act
		await service.InvalidateKeysAsync(keys, CancellationToken.None);

		// Assert
		A.CallTo(() => cache.RemoveAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(keys),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateKeysAsync_WithSingleKey_CallsRemove()
	{
		// Arrange
		var cache = A.Fake<HybridCache>();
		var service = new HybridCacheInvalidationService(cache);
		var keys = new[] { "cache:user:123" };

		// Act
		await service.InvalidateKeysAsync(keys, CancellationToken.None);

		// Assert
		A.CallTo(() => cache.RemoveAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(keys),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateKeysAsync_WithCancellationToken_PassesTokenToCache()
	{
		// Arrange
		var cache = A.Fake<HybridCache>();
		var service = new HybridCacheInvalidationService(cache);
		var keys = new[] { "key1" };
		using var cts = new CancellationTokenSource();

		// Act
		await service.InvalidateKeysAsync(keys, cts.Token);

		// Assert
		A.CallTo(() => cache.RemoveAsync(
			A<IEnumerable<string>>._,
			cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateTagsAsync_WithMultipleTags_PassesAllTags()
	{
		// Arrange
		var cache = A.Fake<HybridCache>();
		var service = new HybridCacheInvalidationService(cache);
		var tags = new[] { "user", "product", "order" };

		// Act
		await service.InvalidateTagsAsync(tags, CancellationToken.None);

		// Assert
		A.CallTo(() => cache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(tags),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateKeysAsync_WithMultipleKeys_PassesAllKeys()
	{
		// Arrange
		var cache = A.Fake<HybridCache>();
		var service = new HybridCacheInvalidationService(cache);
		var keys = new[] { "key1", "key2", "key3" };

		// Act
		await service.InvalidateKeysAsync(keys, CancellationToken.None);

		// Assert
		A.CallTo(() => cache.RemoveAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(keys),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
