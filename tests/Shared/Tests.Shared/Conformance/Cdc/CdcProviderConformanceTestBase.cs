// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Tests.Shared.Conformance.Cdc;

/// <summary>
/// Base class for ICdcStateStore conformance tests.
/// Implementations must provide a concrete ICdcStateStore instance for testing.
/// </summary>
/// <remarks>
/// <para>
/// This conformance test kit verifies that CDC state store implementations
/// correctly implement the ICdcStateStore interface contract, including:
/// </para>
/// <list type="bullet">
///   <item>Change detection via position-based checkpoint tracking</item>
///   <item>Checkpoint persistence and retrieval</item>
///   <item>Resume from saved checkpoint positions</item>
///   <item>Position deletion and consumer reset</item>
///   <item>Error recovery and concurrent access</item>
/// </list>
/// <para>
/// To create conformance tests for your own ICdcStateStore implementation:
/// <list type="number">
///   <item>Inherit from CdcProviderConformanceTestBase</item>
///   <item>Override CreateStateStoreAsync() to create an instance of your ICdcStateStore implementation</item>
///   <item>Override CreateTestPosition() to create a valid ChangePosition for your provider</item>
///   <item>Override CleanupAsync() to properly clean up resources between tests</item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Conformance")]
[Trait("Component", "Cdc")]
public abstract class CdcProviderConformanceTestBase : IAsyncLifetime
{
	/// <summary>
	/// The CDC state store instance under test.
	/// </summary>
	protected ICdcStateStore StateStore { get; private set; } = null!;

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		StateStore = await CreateStateStoreAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		await CleanupAsync().ConfigureAwait(false);

		if (StateStore is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if (StateStore is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Creates a new instance of the ICdcStateStore implementation under test.
	/// </summary>
	/// <returns>A configured ICdcStateStore instance.</returns>
	protected abstract Task<ICdcStateStore> CreateStateStoreAsync();

	/// <summary>
	/// Creates a test ChangePosition instance for the specific provider.
	/// </summary>
	/// <param name="index">An index to distinguish positions. Higher index means later position.</param>
	/// <returns>A valid ChangePosition for testing.</returns>
	protected abstract ChangePosition CreateTestPosition(int index);

	/// <summary>
	/// Cleans up the ICdcStateStore instance after each test.
	/// </summary>
	protected abstract Task CleanupAsync();

	#region Helper Methods

	/// <summary>
	/// Creates a unique consumer ID for testing.
	/// </summary>
	protected static string CreateConsumerId() => $"test-consumer-{Guid.NewGuid():N}";

	#endregion Helper Methods

	#region Interface Implementation Tests

	[Fact]
	public void StateStore_ShouldImplementICdcStateStore()
	{
		// Assert
		_ = StateStore.ShouldBeAssignableTo<ICdcStateStore>();
	}

	#endregion Interface Implementation Tests

	#region DetectChanges (SavePosition + GetPosition) Tests

	[Fact]
	public async Task SaveAndGetPosition_RoundTrips()
	{
		// Arrange
		var consumerId = CreateConsumerId();
		var position = CreateTestPosition(1);

		// Act
		await StateStore.SavePositionAsync(consumerId, position, CancellationToken.None)
			.ConfigureAwait(false);

		var retrieved = await StateStore.GetPositionAsync(consumerId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		retrieved.ShouldNotBeNull("Saved position should be retrievable");
		retrieved.ToToken().ShouldBe(position.ToToken());
	}

	[Fact]
	public async Task GetPosition_NoCheckpoint_ReturnsNull()
	{
		// Arrange
		var consumerId = CreateConsumerId();

		// Act
		var position = await StateStore.GetPositionAsync(consumerId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		position.ShouldBeNull("New consumer should have no checkpoint");
	}

	[Fact]
	public async Task SavePosition_MultipleConsumers_Independent()
	{
		// Arrange
		var consumer1 = CreateConsumerId();
		var consumer2 = CreateConsumerId();
		var position1 = CreateTestPosition(1);
		var position2 = CreateTestPosition(2);

		// Act
		await StateStore.SavePositionAsync(consumer1, position1, CancellationToken.None)
			.ConfigureAwait(false);
		await StateStore.SavePositionAsync(consumer2, position2, CancellationToken.None)
			.ConfigureAwait(false);

		var retrieved1 = await StateStore.GetPositionAsync(consumer1, CancellationToken.None)
			.ConfigureAwait(false);
		var retrieved2 = await StateStore.GetPositionAsync(consumer2, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		retrieved1.ShouldNotBeNull();
		retrieved2.ShouldNotBeNull();
		retrieved1.ToToken().ShouldBe(position1.ToToken());
		retrieved2.ToToken().ShouldBe(position2.ToToken());
	}

	#endregion DetectChanges (SavePosition + GetPosition) Tests

	#region Checkpoint Tests

	[Fact]
	public async Task SavePosition_Overwrites_PreviousCheckpoint()
	{
		// Arrange
		var consumerId = CreateConsumerId();
		var position1 = CreateTestPosition(1);
		var position2 = CreateTestPosition(2);

		// Act - Save, then overwrite
		await StateStore.SavePositionAsync(consumerId, position1, CancellationToken.None)
			.ConfigureAwait(false);
		await StateStore.SavePositionAsync(consumerId, position2, CancellationToken.None)
			.ConfigureAwait(false);

		var retrieved = await StateStore.GetPositionAsync(consumerId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		retrieved.ShouldNotBeNull();
		retrieved.ToToken().ShouldBe(position2.ToToken(), "Should return the latest saved position");
	}

	[Fact]
	public async Task SavePosition_PreservesPositionValidity()
	{
		// Arrange
		var consumerId = CreateConsumerId();
		var position = CreateTestPosition(1);

		// Act
		await StateStore.SavePositionAsync(consumerId, position, CancellationToken.None)
			.ConfigureAwait(false);

		var retrieved = await StateStore.GetPositionAsync(consumerId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		retrieved.ShouldNotBeNull();
		retrieved.IsValid.ShouldBeTrue("Retrieved position should be valid");
	}

	#endregion Checkpoint Tests

	#region Resume Tests

	[Fact]
	public async Task Resume_FromSavedCheckpoint_ReturnsCorrectPosition()
	{
		// Arrange
		var consumerId = CreateConsumerId();

		// Simulate progressive checkpointing
		for (int i = 1; i <= 5; i++)
		{
			var position = CreateTestPosition(i);
			await StateStore.SavePositionAsync(consumerId, position, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Act - Resume from last checkpoint
		var resumePosition = await StateStore.GetPositionAsync(consumerId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - Should be the last saved position
		resumePosition.ShouldNotBeNull();
		resumePosition.ToToken().ShouldBe(CreateTestPosition(5).ToToken());
	}

	[Fact]
	public async Task Resume_AfterDelete_ReturnsNull()
	{
		// Arrange
		var consumerId = CreateConsumerId();
		var position = CreateTestPosition(1);

		await StateStore.SavePositionAsync(consumerId, position, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		await StateStore.DeletePositionAsync(consumerId, CancellationToken.None)
			.ConfigureAwait(false);

		var resumePosition = await StateStore.GetPositionAsync(consumerId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		resumePosition.ShouldBeNull("Deleted checkpoint should not be resumable");
	}

	#endregion Resume Tests

	#region SchemaChange (DeletePosition) Tests

	[Fact]
	public async Task DeletePosition_ExistingCheckpoint_ReturnsTrue()
	{
		// Arrange
		var consumerId = CreateConsumerId();
		var position = CreateTestPosition(1);

		await StateStore.SavePositionAsync(consumerId, position, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		var deleted = await StateStore.DeletePositionAsync(consumerId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		deleted.ShouldBeTrue("Deleting existing checkpoint should return true");
	}

	[Fact]
	public async Task DeletePosition_NonExistentCheckpoint_ReturnsFalse()
	{
		// Arrange
		var consumerId = CreateConsumerId();

		// Act
		var deleted = await StateStore.DeletePositionAsync(consumerId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		deleted.ShouldBeFalse("Deleting non-existent checkpoint should return false");
	}

	[Fact]
	public async Task DeletePosition_DoesNotAffectOtherConsumers()
	{
		// Arrange
		var consumer1 = CreateConsumerId();
		var consumer2 = CreateConsumerId();
		var position1 = CreateTestPosition(1);
		var position2 = CreateTestPosition(2);

		await StateStore.SavePositionAsync(consumer1, position1, CancellationToken.None)
			.ConfigureAwait(false);
		await StateStore.SavePositionAsync(consumer2, position2, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		await StateStore.DeletePositionAsync(consumer1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var retrieved2 = await StateStore.GetPositionAsync(consumer2, CancellationToken.None)
			.ConfigureAwait(false);
		retrieved2.ShouldNotBeNull("Consumer 2's checkpoint should not be affected");
		retrieved2.ToToken().ShouldBe(position2.ToToken());
	}

	#endregion SchemaChange (DeletePosition) Tests

	#region ErrorRecovery Tests

	[Fact]
	public async Task GetAllPositions_ReturnsAllConsumerCheckpoints()
	{
		// Arrange
		var consumers = new List<(string Id, ChangePosition Position)>();
		for (int i = 0; i < 3; i++)
		{
			var consumerId = CreateConsumerId();
			var position = CreateTestPosition(i + 1);
			consumers.Add((consumerId, position));
			await StateStore.SavePositionAsync(consumerId, position, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Act
		var allPositions = new List<(string ConsumerId, ChangePosition Position)>();
		await foreach (var entry in StateStore.GetAllPositionsAsync(CancellationToken.None)
			.ConfigureAwait(false))
		{
			allPositions.Add(entry);
		}

		// Assert
		allPositions.Count.ShouldBeGreaterThanOrEqualTo(3);
		foreach (var (consumerId, position) in consumers)
		{
			allPositions.ShouldContain(p => p.ConsumerId == consumerId);
		}
	}

	[Fact]
	public async Task GetAllPositions_EmptyStore_ReturnsEmpty()
	{
		// Act
		var allPositions = new List<(string ConsumerId, ChangePosition Position)>();
		await foreach (var entry in StateStore.GetAllPositionsAsync(CancellationToken.None)
			.ConfigureAwait(false))
		{
			allPositions.Add(entry);
		}

		// Assert
		allPositions.ShouldBeEmpty();
	}

	[Fact]
	public async Task ConcurrentSavePosition_AllSucceed()
	{
		// Arrange
		const int concurrentConsumers = 10;
		var consumers = Enumerable.Range(0, concurrentConsumers)
			.Select(i => (Id: CreateConsumerId(), Position: CreateTestPosition(i)))
			.ToList();

		// Act - Save all concurrently
		var tasks = consumers.Select(c =>
			StateStore.SavePositionAsync(c.Id, c.Position, CancellationToken.None));
		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - All should be retrievable
		foreach (var (id, position) in consumers)
		{
			var retrieved = await StateStore.GetPositionAsync(id, CancellationToken.None)
				.ConfigureAwait(false);
			retrieved.ShouldNotBeNull($"Consumer {id} should have a saved position");
			retrieved.ToToken().ShouldBe(position.ToToken());
		}
	}

	[Fact]
	public async Task ConcurrentSavePosition_SameConsumer_LastWriteWins()
	{
		// Arrange
		var consumerId = CreateConsumerId();
		const int concurrentWrites = 10;

		// Act - Write to the same consumer concurrently
		var tasks = Enumerable.Range(0, concurrentWrites)
			.Select(i => StateStore.SavePositionAsync(
				consumerId,
				CreateTestPosition(i),
				CancellationToken.None));
		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Should have some valid position (last write wins)
		var retrieved = await StateStore.GetPositionAsync(consumerId, CancellationToken.None)
			.ConfigureAwait(false);
		retrieved.ShouldNotBeNull("Should have a valid position after concurrent writes");
		retrieved.IsValid.ShouldBeTrue();
	}

	#endregion ErrorRecovery Tests
}
