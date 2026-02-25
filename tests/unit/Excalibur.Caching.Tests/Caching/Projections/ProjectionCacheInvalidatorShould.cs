// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test

using Excalibur.Caching.Projections;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.Caching.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ProjectionCacheInvalidatorShould
{
	private readonly ICacheInvalidationService _fakeCache = A.Fake<ICacheInvalidationService>();
	private readonly IServiceProvider _fakeServices = A.Fake<IServiceProvider>();

	private ProjectionCacheInvalidator CreateInvalidator(ILogger<ProjectionCacheInvalidator>? logger = null)
	{
		return new ProjectionCacheInvalidator(
			_fakeCache,
			_fakeServices,
			logger ?? NullLogger<ProjectionCacheInvalidator>.Instance);
	}

	// Test message implementing IProjectionInvalidationTags
	private sealed class ExplicitTagMessage : IProjectionInvalidationTags
	{
		public IEnumerable<string> GetProjectionCacheTags() => ["tag1", "tag2"];
	}

	// Convention-based Updated message
	private sealed class OrderUpdated
	{
		public string EntityId { get; set; } = "order-123";
	}

	// Convention-based Deleted message
	private sealed class OrderDeleted
	{
		public string MessageId { get; set; } = "order-456";
	}

	// Non-convention message (no Updated/Deleted suffix)
	private sealed class OrderCreated
	{
		public string EntityId { get; set; } = "order-789";
	}

	// Convention message with no valid property
	private sealed class SomethingUpdated
	{
		public string Name { get; set; } = "test";
	}

	[Fact]
	public async Task InvalidateCacheAsync_ThrowsOnNull()
	{
		var invalidator = CreateInvalidator();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			invalidator.InvalidateCacheAsync(null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvalidateCacheAsync_UsesExplicitTags()
	{
		// Arrange
		var invalidator = CreateInvalidator();
		var message = new ExplicitTagMessage();

		// Act
		await invalidator.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeCache.InvalidateTagsAsync(
			A<IReadOnlyList<string>>.That.Contains("tag1"),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateCacheAsync_UsesConventionForUpdatedSuffix()
	{
		// Arrange
		var invalidator = CreateInvalidator();
		var message = new OrderUpdated { EntityId = "order-123" };

		// Act
		await invalidator.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert — convention should extract tags
		A.CallTo(() => _fakeCache.InvalidateTagsAsync(
			A<IReadOnlyList<string>>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateCacheAsync_UsesConventionForDeletedSuffix()
	{
		// Arrange
		var invalidator = CreateInvalidator();
		var message = new OrderDeleted { MessageId = "order-456" };

		// Act
		await invalidator.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeCache.InvalidateTagsAsync(
			A<IReadOnlyList<string>>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateCacheAsync_DoesNotInvalidate_WhenNoStrategyMatches()
	{
		// Arrange
		var invalidator = CreateInvalidator();
		var message = new OrderCreated { EntityId = "order-789" };

		// Act
		await invalidator.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert — no matching convention or tags
		A.CallTo(() => _fakeCache.InvalidateTagsAsync(
			A<IReadOnlyList<string>>._,
			A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task InvalidateCacheAsync_HandlesConventionMessageWithNoProperty()
	{
		// Arrange
		var invalidator = CreateInvalidator();
		var message = new SomethingUpdated { Name = "test" };

		// Act
		await invalidator.InvalidateCacheAsync(message, CancellationToken.None);

		// Assert — no property found, should not throw
		A.CallTo(() => _fakeCache.InvalidateTagsAsync(
			A<IReadOnlyList<string>>._,
			A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void ImplementIProjectionCacheInvalidator()
	{
		var invalidator = CreateInvalidator();
		invalidator.ShouldBeAssignableTo<IProjectionCacheInvalidator>();
	}
}

#pragma warning restore IL2026, IL3050
