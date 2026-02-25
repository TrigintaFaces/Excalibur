// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class HybridCacheInvalidationServiceShould
{
	private readonly HybridCache _hybridCache = A.Fake<HybridCache>();
	private readonly HybridCacheInvalidationService _sut;

	public HybridCacheInvalidationServiceShould()
	{
		_sut = new HybridCacheInvalidationService(_hybridCache);
	}

	[Fact]
	public async Task InvalidateTags_CallsRemoveByTag()
	{
		// Arrange
		var tags = new[] { "tag1", "tag2" };

		// Act
		await _sut.InvalidateTagsAsync(tags, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(tags),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateTags_DoesNotCallRemove_WhenTagsEmpty()
	{
		// Arrange
		var tags = Array.Empty<string>();

		// Act
		await _sut.InvalidateTagsAsync(tags, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task InvalidateKeys_CallsRemove()
	{
		// Arrange
		var keys = new[] { "key1", "key2" };

		// Act
		await _sut.InvalidateKeysAsync(keys, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _hybridCache.RemoveAsync(
			A<IEnumerable<string>>.That.IsSameSequenceAs(keys),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateKeys_DoesNotCallRemove_WhenKeysEmpty()
	{
		// Arrange
		var keys = Array.Empty<string>();

		// Act
		await _sut.InvalidateKeysAsync(keys, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _hybridCache.RemoveAsync(
			A<IEnumerable<string>>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}
}
