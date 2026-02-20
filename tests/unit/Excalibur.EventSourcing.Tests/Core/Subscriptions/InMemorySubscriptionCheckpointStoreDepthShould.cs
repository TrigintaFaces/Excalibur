// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	public async Task StoreCheckpointAsync_ThenGetReturnsStoredValue()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act
		await store.StoreCheckpointAsync("sub-1", 42, CancellationToken.None);
		var result = await store.GetCheckpointAsync("sub-1", CancellationToken.None);

		// Assert
		result.ShouldBe(42L);
	}

	[Fact]
	public async Task StoreCheckpointAsync_OverwritesPrevious()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act
		await store.StoreCheckpointAsync("sub-1", 10, CancellationToken.None);
		await store.StoreCheckpointAsync("sub-1", 20, CancellationToken.None);
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
		await store.StoreCheckpointAsync("sub-1", 10, CancellationToken.None);
		await store.StoreCheckpointAsync("sub-2", 20, CancellationToken.None);

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
	public async Task StoreCheckpointAsync_ThrowsArgumentException_WhenNameIsNull()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await Should.ThrowAsync<ArgumentException>(() =>
			store.StoreCheckpointAsync(null!, 10, CancellationToken.None));
	}

	[Fact]
	public async Task StoreCheckpointAsync_ThrowsArgumentException_WhenNameIsEmpty()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await Should.ThrowAsync<ArgumentException>(() =>
			store.StoreCheckpointAsync("", 10, CancellationToken.None));
	}
}
