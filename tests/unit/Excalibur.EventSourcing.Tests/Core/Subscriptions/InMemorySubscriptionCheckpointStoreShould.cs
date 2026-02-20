// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemorySubscriptionCheckpointStoreShould
{
	[Fact]
	public async Task ReturnNullForUnknownSubscription()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act
		var result = await store.GetCheckpointAsync("unknown", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task StoreAndRetrieveCheckpoint()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();
		await store.StoreCheckpointAsync("sub-1", 42L, CancellationToken.None);

		// Act
		var result = await store.GetCheckpointAsync("sub-1", CancellationToken.None);

		// Assert
		result.ShouldBe(42L);
	}

	[Fact]
	public async Task OverwriteExistingCheckpoint()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();
		await store.StoreCheckpointAsync("sub-1", 10L, CancellationToken.None);
		await store.StoreCheckpointAsync("sub-1", 20L, CancellationToken.None);

		// Act
		var result = await store.GetCheckpointAsync("sub-1", CancellationToken.None);

		// Assert
		result.ShouldBe(20L);
	}

	[Fact]
	public async Task TrackMultipleSubscriptionsIndependently()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();
		await store.StoreCheckpointAsync("sub-1", 100L, CancellationToken.None);
		await store.StoreCheckpointAsync("sub-2", 200L, CancellationToken.None);

		// Act & Assert
		(await store.GetCheckpointAsync("sub-1", CancellationToken.None)).ShouldBe(100L);
		(await store.GetCheckpointAsync("sub-2", CancellationToken.None)).ShouldBe(200L);
	}

	[Fact]
	public async Task ThrowOnNullOrEmptySubscriptionNameForGet()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetCheckpointAsync(null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetCheckpointAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullOrEmptySubscriptionNameForStore()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.StoreCheckpointAsync(null!, 1L, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => store.StoreCheckpointAsync("", 1L, CancellationToken.None));
	}

	[Fact]
	public void ImplementISubscriptionCheckpointStore()
	{
		// Arrange & Act
		var store = new InMemorySubscriptionCheckpointStore();

		// Assert
		store.ShouldBeAssignableTo<ISubscriptionCheckpointStore>();
	}
}
