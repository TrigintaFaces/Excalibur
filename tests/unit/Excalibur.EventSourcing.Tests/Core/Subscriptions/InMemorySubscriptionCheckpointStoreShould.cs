// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
		await store.AdvanceCheckpointAsync("sub-1", expectedPosition: null, 42L, CancellationToken.None);

		// Act
		var result = await store.GetCheckpointAsync("sub-1", CancellationToken.None);

		// Assert
		result.ShouldBe(42L);
	}

	[Fact]
	public async Task AdvanceAnExistingCheckpointWhenTheExpectedPositionStillMatches()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();
		await store.AdvanceCheckpointAsync("sub-1", expectedPosition: null, 10L, CancellationToken.None);
		await store.AdvanceCheckpointAsync("sub-1", expectedPosition: 10L, 20L, CancellationToken.None);

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
		await store.AdvanceCheckpointAsync("sub-1", expectedPosition: null, 100L, CancellationToken.None);
		await store.AdvanceCheckpointAsync("sub-2", expectedPosition: null, 200L, CancellationToken.None);

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
	public async Task ThrowOnNullOrEmptySubscriptionNameForAdvance()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.AdvanceCheckpointAsync(null!, null, 1L, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => store.AdvanceCheckpointAsync("", null, 1L, CancellationToken.None));
	}

	[Fact]
	public async Task RefuseAnAdvanceWhoseExpectedPositionIsStale()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		_ = await store.AdvanceCheckpointAsync("sub-1", expectedPosition: null, 10L, CancellationToken.None);
		_ = await store.AdvanceCheckpointAsync("sub-1", expectedPosition: 10L, 20L, CancellationToken.None);

		// A second reader still believes the checkpoint is at 10. Under a blind write it would drag the
		// mark back from 20 to 15 and every event in between would be delivered again.
		var outcome = await store.AdvanceCheckpointAsync("sub-1", expectedPosition: 10L, 15L, CancellationToken.None);

		outcome.ShouldBe(
			CheckpointAdvanceOutcome.Superseded,
			"an advance from a stale expected position must be refused, not applied");

		(await store.GetCheckpointAsync("sub-1", CancellationToken.None)).ShouldBe(
			20L,
			"a refused advance must leave the stored position untouched -- reporting the refusal is not " +
			"enough if the write happened anyway");
	}

	[Fact]
	public async Task RefuseACreateWhenACheckpointAlreadyExists()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		_ = await store.AdvanceCheckpointAsync("sub-1", expectedPosition: null, 10L, CancellationToken.None);

		// "No checkpoint yet" is a distinct prior state, so a caller holding that belief must lose to the
		// writer that created one. A store treating null as "overwrite whatever is there" would pass the
		// stale-expected arm above and still clobber here.
		var outcome = await store.AdvanceCheckpointAsync("sub-1", expectedPosition: null, 5L, CancellationToken.None);

		outcome.ShouldBe(CheckpointAdvanceOutcome.Superseded);
		(await store.GetCheckpointAsync("sub-1", CancellationToken.None)).ShouldBe(10L);
	}

	[Fact]
	public async Task ReportAdvancedWhenTheExpectedPositionMatches()
	{
		// LIVENESS. Without this, a store that refused EVERY advance would satisfy both arms above.
		var store = new InMemorySubscriptionCheckpointStore();

		var created = await store.AdvanceCheckpointAsync("sub-1", expectedPosition: null, 10L, CancellationToken.None);
		var moved = await store.AdvanceCheckpointAsync("sub-1", expectedPosition: 10L, 20L, CancellationToken.None);

		created.ShouldBe(CheckpointAdvanceOutcome.Advanced);
		moved.ShouldBe(CheckpointAdvanceOutcome.Advanced);
		(await store.GetCheckpointAsync("sub-1", CancellationToken.None)).ShouldBe(20L);
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
