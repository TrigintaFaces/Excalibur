// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Caching.Projections;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Caching.Tests.Projections;

/// <summary>
/// Depth coverage tests for <see cref="ProjectionCacheInvalidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ProjectionCacheInvalidatorDepthShould
{
	private readonly ICacheInvalidationService _cache;
	private readonly IServiceProvider _services;
	private readonly ProjectionCacheInvalidator _sut;

	public ProjectionCacheInvalidatorDepthShould()
	{
		_cache = A.Fake<ICacheInvalidationService>();
		_services = A.Fake<IServiceProvider>();

		_sut = new ProjectionCacheInvalidator(
			_cache,
			_services,
			NullLogger<ProjectionCacheInvalidator>.Instance);
	}

	[Fact]
	public async Task ThrowWhenMessageIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.InvalidateCacheAsync(null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task UseExplicitTagsWhenMessageImplementsInterface()
	{
		// Arrange
		var message = new MessageWithExplicitTags(["tag1", "tag2"]);

		// Act
		await _sut.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.InvalidateTagsAsync(
			A<IReadOnlyCollection<string>>.That.Matches(tags =>
				tags.Contains("tag1") && tags.Contains("tag2")),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipEmptyTagsFromExplicitInterface()
	{
		// Arrange
		var message = new MessageWithExplicitTags(["tag1", "", " ", "tag2"]);

		// Act
		await _sut.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.InvalidateTagsAsync(
			A<IReadOnlyCollection<string>>.That.Matches(tags =>
				tags.Count == 2 && tags.Contains("tag1") && tags.Contains("tag2")),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseConventionForUpdatedSuffix()
	{
		// Arrange
		var message = new OrderUpdated { EntityId = "order-123" };

		// Act
		await _sut.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.InvalidateTagsAsync(
			A<IReadOnlyCollection<string>>.That.Matches(tags => tags.Count == 1),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseConventionForDeletedSuffix()
	{
		// Arrange
		var message = new OrderDeleted { EntityId = "order-456" };

		// Act
		await _sut.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.InvalidateTagsAsync(
			A<IReadOnlyCollection<string>>.That.Matches(tags => tags.Count == 1),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotInvalidateWhenNoStrategyMatches()
	{
		// Arrange
		var message = new UnknownMessage();

		// Act
		await _sut.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.InvalidateTagsAsync(A<IReadOnlyCollection<string>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task NotInvalidateWhenConventionPropertyValueIsEmpty()
	{
		// Arrange
		var message = new OrderUpdated { EntityId = "" };

		// Act
		await _sut.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.InvalidateTagsAsync(A<IReadOnlyCollection<string>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task NotInvalidateWhenConventionPropertyValueIsNull()
	{
		// Arrange
		var message = new OrderUpdated { EntityId = null! };

		// Act
		await _sut.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.InvalidateTagsAsync(A<IReadOnlyCollection<string>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task UseMessageIdPropertyAsConventionFallback()
	{
		// Arrange
		var message = new ItemUpdated { MessageId = "msg-789" };

		// Act
		await _sut.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.InvalidateTagsAsync(
			A<IReadOnlyCollection<string>>.That.Matches(tags => tags.Count == 1),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void HaveCorrectSuffixConstants()
	{
		// Assert
		ProjectionCacheInvalidator.UpdatedSuffix.ShouldBe("Updated");
		ProjectionCacheInvalidator.DeletedSuffix.ShouldBe("Deleted");
		ProjectionCacheInvalidator.MessageIdPropertyName.ShouldBe("MessageId");
		ProjectionCacheInvalidator.EntityIdPropertyName.ShouldBe("EntityId");
	}

	// Test helper types
	private sealed class MessageWithExplicitTags(IEnumerable<string> tags) : IProjectionInvalidationTags
	{
		public IEnumerable<string> GetProjectionCacheTags() => tags;
	}

	private sealed class OrderUpdated
	{
		public string EntityId { get; init; } = string.Empty;
	}

	private sealed class OrderDeleted
	{
		public string EntityId { get; init; } = string.Empty;
	}

	private sealed class ItemUpdated
	{
		public string MessageId { get; init; } = string.Empty;
	}

	private sealed class UnknownMessage;
}
