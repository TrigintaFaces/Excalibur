// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class HybridCacheInvalidationServiceDepthShould
{
	private readonly HybridCache _hybridCache = A.Fake<HybridCache>();
	private readonly HybridCacheInvalidationService _sut;

	public HybridCacheInvalidationServiceDepthShould()
	{
		_sut = new HybridCacheInvalidationService(_hybridCache);
	}

	[Fact]
	public async Task ForwardCancellationToken_WhenInvalidatingTags()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var tags = new[] { "tag1" };

		// Act
		await _sut.InvalidateTagsAsync(tags, cts.Token).ConfigureAwait(false);

		// Assert -- verify exact CancellationToken was forwarded
		A.CallTo(() => _hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>._,
			cts.Token)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ForwardCancellationToken_WhenInvalidatingKeys()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var keys = new[] { "key1" };

		// Act
		await _sut.InvalidateKeysAsync(keys, cts.Token).ConfigureAwait(false);

		// Assert -- verify exact CancellationToken was forwarded
		A.CallTo(() => _hybridCache.RemoveAsync(
			A<IEnumerable<string>>._,
			cts.Token)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleSingleTag()
	{
		// Arrange
		var tags = new[] { "single-tag" };

		// Act
		await _sut.InvalidateTagsAsync(tags, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(tags),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleSingleKey()
	{
		// Arrange
		var keys = new[] { "single-key" };

		// Act
		await _sut.InvalidateKeysAsync(keys, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _hybridCache.RemoveAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(keys),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleMultipleTags()
	{
		// Arrange
		var tags = new[] { "tag1", "tag2", "tag3" };

		// Act
		await _sut.InvalidateTagsAsync(tags, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(tags),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleMultipleKeys()
	{
		// Arrange
		var keys = new[] { "key1", "key2", "key3" };

		// Act
		await _sut.InvalidateKeysAsync(keys, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _hybridCache.RemoveAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(keys),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ImplementICacheInvalidationService()
	{
		// Assert
		_sut.ShouldBeAssignableTo<ICacheInvalidationService>();
	}
}
