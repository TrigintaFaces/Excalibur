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
		var result = await store.GetCheckpointAsync("sub-1", "Proj.A", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task AdvanceCheckpointAsync_ThenGetReturnsStoredValue()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act
		await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: null, 42, CancellationToken.None);
		var result = await store.GetCheckpointAsync("sub-1", "Proj.A", CancellationToken.None);

		// Assert
		result.ShouldBe(42L);
	}

	[Fact]
	public async Task AdvanceCheckpointAsync_MovesForwardWhenExpectedMatches()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act
		await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: null, 10, CancellationToken.None);
		await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: 10, 20, CancellationToken.None);
		var result = await store.GetCheckpointAsync("sub-1", "Proj.A", CancellationToken.None);

		// Assert
		result.ShouldBe(20L);
	}

	[Fact]
	public async Task MultipleSubscriptions_AreIndependent()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act
		await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: null, 10, CancellationToken.None);
		await store.AdvanceBoundCheckpointAsync("sub-2", "Proj.A", expectedPosition: null, 20, CancellationToken.None);

		// Assert
		(await store.GetCheckpointAsync("sub-1", "Proj.A", CancellationToken.None)).ShouldBe(10L);
		(await store.GetCheckpointAsync("sub-2", "Proj.A", CancellationToken.None)).ShouldBe(20L);
	}

	[Fact]
	public async Task GetCheckpointAsync_ThrowsArgumentException_WhenNameIsNull()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await Should.ThrowAsync<ArgumentException>(() =>
			store.GetCheckpointAsync(null!, "Proj.A", CancellationToken.None));
	}

	[Fact]
	public async Task GetCheckpointAsync_ThrowsArgumentException_WhenNameIsEmpty()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await Should.ThrowAsync<ArgumentException>(() =>
			store.GetCheckpointAsync("", "Proj.A", CancellationToken.None));
	}

	[Fact]
	public async Task GetCheckpointAsync_ThrowsArgumentException_WhenProjectionKeyIsBlank()
	{
		var store = new InMemorySubscriptionCheckpointStore();

		// A cursor is addressed by the PAIR, so a blank projection key is as unaddressable as a blank
		// subscription name. Accepting one would collapse every projection under a subscription onto a
		// single cursor, and each would skip whatever the others folded.
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			store.GetCheckpointAsync("sub-1", " ", CancellationToken.None));
	}

	[Fact]
	public async Task AdvanceCheckpointAsync_ThrowsArgumentException_WhenProjectionKeyIsBlank()
	{
		var store = new InMemorySubscriptionCheckpointStore();

		_ = await Should.ThrowAsync<ArgumentException>(() =>
			store.AdvanceBoundCheckpointAsync("sub-1", "", null, 10, CancellationToken.None));
	}

	[Fact]
	public async Task AdvanceCheckpointAsync_ThrowsArgumentException_WhenNameIsNull()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await Should.ThrowAsync<ArgumentException>(() =>
			store.AdvanceBoundCheckpointAsync(null!, "Proj.A", null, 10, CancellationToken.None));
	}

	[Fact]
	public async Task AdvanceCheckpointAsync_ThrowsArgumentException_WhenNameIsEmpty()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await Should.ThrowAsync<ArgumentException>(() =>
			store.AdvanceBoundCheckpointAsync("", "Proj.A", null, 10, CancellationToken.None));
	}
}
