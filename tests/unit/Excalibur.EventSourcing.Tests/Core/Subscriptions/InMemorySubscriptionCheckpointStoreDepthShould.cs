// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

/// <summary>
/// Depth coverage tests for <see cref="InMemorySubscriptionCheckpointStore"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemorySubscriptionCheckpointStoreDepthShould
{
	[Fact]
	public async Task GetCheckpointAsync_ReturnsNull_WhenNotStored()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act
		var result = await store.GetCheckpointAsync("sub-1", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task AdvanceCheckpointAsync_ThenGetReturnsStoredValue()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act
		await store.AdvanceCheckpointAsync("sub-1", expectedPosition: null, 42, CancellationToken.None);
		var result = await store.GetCheckpointAsync("sub-1", CancellationToken.None);

		// Assert
		result.ShouldBe(42L);
	}

	[Fact]
	public async Task AdvanceCheckpointAsync_MovesForwardWhenExpectedMatches()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act
		await store.AdvanceCheckpointAsync("sub-1", expectedPosition: null, 10, CancellationToken.None);
		await store.AdvanceCheckpointAsync("sub-1", expectedPosition: 10, 20, CancellationToken.None);
		var result = await store.GetCheckpointAsync("sub-1", CancellationToken.None);

		// Assert
		result.ShouldBe(20L);
	}

	[Fact]
	public async Task MultipleSubscriptions_AreIndependent()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act
		await store.AdvanceCheckpointAsync("sub-1", expectedPosition: null, 10, CancellationToken.None);
		await store.AdvanceCheckpointAsync("sub-2", expectedPosition: null, 20, CancellationToken.None);

		// Assert
		(await store.GetCheckpointAsync("sub-1", CancellationToken.None)).ShouldBe(10L);
		(await store.GetCheckpointAsync("sub-2", CancellationToken.None)).ShouldBe(20L);
	}

	[Fact]
	public async Task GetCheckpointAsync_ThrowsArgumentException_WhenNameIsNull()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await Should.ThrowAsync<ArgumentException>(() =>
			store.GetCheckpointAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetCheckpointAsync_ThrowsArgumentException_WhenNameIsEmpty()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await Should.ThrowAsync<ArgumentException>(() =>
			store.GetCheckpointAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task AdvanceCheckpointAsync_ThrowsArgumentException_WhenNameIsNull()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await Should.ThrowAsync<ArgumentException>(() =>
			store.AdvanceCheckpointAsync(null!, null, 10, CancellationToken.None));
	}

	[Fact]
	public async Task AdvanceCheckpointAsync_ThrowsArgumentException_WhenNameIsEmpty()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await Should.ThrowAsync<ArgumentException>(() =>
			store.AdvanceCheckpointAsync("", null, 10, CancellationToken.None));
	}
}
