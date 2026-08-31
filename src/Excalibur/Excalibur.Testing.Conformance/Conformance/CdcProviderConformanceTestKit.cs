// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IDE0007 // Use implicit type (var)
#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for <see cref="ICdcStateStore"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this kit and implement <see cref="CreateStateStoreAsync"/> and
/// <see cref="CreateTestPosition"/> to verify that your CDC state store conforms to the contract:
/// checkpoint save/get/delete round-trips, resume from a saved position, multi-consumer isolation, and
/// concurrent access. Override <see cref="CleanupAsync"/> to release resources between tests.
/// </para>
/// <para>
/// The kit exposes plain <c>public virtual</c> methods with no test-framework attributes; add the
/// attributes your test framework requires (for example <c>[Fact]</c>) on thin overrides in your derived
/// class. Each test creates a fresh store via <see cref="CreateStateStoreAsync"/> and disposes it (and
/// calls <see cref="CleanupAsync"/>) afterwards.
/// </para>
/// <para>
/// <b>This kit is trim-excluded, not trim-safe, and that is a statement about the CDC state-store contract
/// rather than about the kit.</b> The arms save checkpoints and reload them through the store, and a conformant store rehydrates the provider's own ChangePosition subclass, whose concrete type it does not know statically. No annotation on this kit can reach
/// those types, so a deriving suite must itself carry
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"/> — or suppress the
/// warning deliberately — when it is compiled with the trim analyzer enabled. Overriding an arm
/// rather than wrapping it requires the same annotation on the override. A trimmed test host is not
/// a supported configuration for this kit.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
	"CDC state-store conformance arms save and reload checkpoints through the store, which rehydrates the provider's own ChangePosition subclass reflectively. A trimmed test host is not a supported configuration for this kit.")]
public abstract class CdcProviderConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a new instance of the <see cref="ICdcStateStore"/> implementation under test.
	/// </summary>
	/// <returns>A configured <see cref="ICdcStateStore"/> instance.</returns>
	protected abstract Task<ICdcStateStore> CreateStateStoreAsync();

	/// <summary>
	/// Creates a test <see cref="ChangePosition"/> instance for the specific provider.
	/// </summary>
	/// <param name="index">An index to distinguish positions. A higher index means a later position.</param>
	/// <returns>A valid <see cref="ChangePosition"/> for testing.</returns>
	protected abstract ChangePosition CreateTestPosition(int index);

	/// <summary>
	/// Cleans up resources after each test. The default implementation does nothing.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Creates a unique consumer identifier for testing.
	/// </summary>
	/// <returns>A unique consumer identifier.</returns>
	protected static string CreateConsumerId() => $"test-consumer-{Guid.NewGuid():N}";

	private async Task RunAsync(Func<ICdcStateStore, Task> body)
	{
		var store = await CreateStateStoreAsync().ConfigureAwait(false);
		try
		{
			await body(store).ConfigureAwait(false);
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			if (store is IAsyncDisposable asyncDisposable)
			{
				await asyncDisposable.DisposeAsync().ConfigureAwait(false);
			}
			else if (store is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}
	}

	/// <summary>Verifies a saved position round-trips through <c>SavePositionAsync</c>/<c>GetPositionAsync</c>.</summary>
	public virtual Task SaveAndGetPosition_RoundTrips() => RunAsync(async store =>
	{
		var consumerId = CreateConsumerId();
		var position = CreateTestPosition(1);

		await store.SavePositionAsync(consumerId, position, CancellationToken.None).ConfigureAwait(false);
		var retrieved = await store.GetPositionAsync(consumerId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException("Saved position should be retrievable.");
		}

		if (retrieved.ToToken() != position.ToToken())
		{
			throw new TestFixtureAssertionException("Retrieved position token should match the saved token.");
		}
	});

	/// <summary>Verifies a new consumer with no checkpoint returns null.</summary>
	public virtual Task GetPosition_NoCheckpoint_ReturnsNull() => RunAsync(async store =>
	{
		var consumerId = CreateConsumerId();

		var position = await store.GetPositionAsync(consumerId, CancellationToken.None).ConfigureAwait(false);

		if (position is not null)
		{
			throw new TestFixtureAssertionException("A new consumer should have no checkpoint (expected null).");
		}
	});

	/// <summary>Verifies checkpoints for different consumers are stored independently.</summary>
	public virtual Task SavePosition_MultipleConsumers_Independent() => RunAsync(async store =>
	{
		var consumer1 = CreateConsumerId();
		var consumer2 = CreateConsumerId();
		var position1 = CreateTestPosition(1);
		var position2 = CreateTestPosition(2);

		await store.SavePositionAsync(consumer1, position1, CancellationToken.None).ConfigureAwait(false);
		await store.SavePositionAsync(consumer2, position2, CancellationToken.None).ConfigureAwait(false);

		var retrieved1 = await store.GetPositionAsync(consumer1, CancellationToken.None).ConfigureAwait(false);
		var retrieved2 = await store.GetPositionAsync(consumer2, CancellationToken.None).ConfigureAwait(false);

		if (retrieved1 is null || retrieved2 is null)
		{
			throw new TestFixtureAssertionException("Both consumers should have retrievable positions.");
		}

		if (retrieved1.ToToken() != position1.ToToken() || retrieved2.ToToken() != position2.ToToken())
		{
			throw new TestFixtureAssertionException("Each consumer should retrieve its own position.");
		}
	});

	/// <summary>Verifies saving a position overwrites the previous checkpoint.</summary>
	public virtual Task SavePosition_Overwrites_PreviousCheckpoint() => RunAsync(async store =>
	{
		var consumerId = CreateConsumerId();
		var position1 = CreateTestPosition(1);
		var position2 = CreateTestPosition(2);

		await store.SavePositionAsync(consumerId, position1, CancellationToken.None).ConfigureAwait(false);
		await store.SavePositionAsync(consumerId, position2, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetPositionAsync(consumerId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null || retrieved.ToToken() != position2.ToToken())
		{
			throw new TestFixtureAssertionException("Should return the latest saved position.");
		}
	});

	/// <summary>Verifies a retrieved position reports itself valid.</summary>
	public virtual Task SavePosition_PreservesPositionValidity() => RunAsync(async store =>
	{
		var consumerId = CreateConsumerId();
		var position = CreateTestPosition(1);

		await store.SavePositionAsync(consumerId, position, CancellationToken.None).ConfigureAwait(false);
		var retrieved = await store.GetPositionAsync(consumerId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null || !retrieved.IsValid)
		{
			throw new TestFixtureAssertionException("Retrieved position should be valid.");
		}
	});

	/// <summary>Verifies resume returns the last-saved checkpoint after progressive checkpointing.</summary>
	public virtual Task Resume_FromSavedCheckpoint_ReturnsCorrectPosition() => RunAsync(async store =>
	{
		var consumerId = CreateConsumerId();

		for (int i = 1; i <= 5; i++)
		{
			await store.SavePositionAsync(consumerId, CreateTestPosition(i), CancellationToken.None).ConfigureAwait(false);
		}

		var resumePosition = await store.GetPositionAsync(consumerId, CancellationToken.None).ConfigureAwait(false);

		if (resumePosition is null || resumePosition.ToToken() != CreateTestPosition(5).ToToken())
		{
			throw new TestFixtureAssertionException("Resume should return the last saved position.");
		}
	});

	/// <summary>Verifies resume returns null after the checkpoint is deleted.</summary>
	public virtual Task Resume_AfterDelete_ReturnsNull() => RunAsync(async store =>
	{
		var consumerId = CreateConsumerId();
		var position = CreateTestPosition(1);

		await store.SavePositionAsync(consumerId, position, CancellationToken.None).ConfigureAwait(false);
		await store.DeletePositionAsync(consumerId, CancellationToken.None).ConfigureAwait(false);

		var resumePosition = await store.GetPositionAsync(consumerId, CancellationToken.None).ConfigureAwait(false);

		if (resumePosition is not null)
		{
			throw new TestFixtureAssertionException("A deleted checkpoint should not be resumable (expected null).");
		}
	});

	/// <summary>Verifies deleting an existing checkpoint returns true.</summary>
	public virtual Task DeletePosition_ExistingCheckpoint_ReturnsTrue() => RunAsync(async store =>
	{
		var consumerId = CreateConsumerId();
		var position = CreateTestPosition(1);

		await store.SavePositionAsync(consumerId, position, CancellationToken.None).ConfigureAwait(false);
		var deleted = await store.DeletePositionAsync(consumerId, CancellationToken.None).ConfigureAwait(false);

		if (!deleted)
		{
			throw new TestFixtureAssertionException("Deleting an existing checkpoint should return true.");
		}
	});

	/// <summary>Verifies deleting a non-existent checkpoint returns false.</summary>
	public virtual Task DeletePosition_NonExistentCheckpoint_ReturnsFalse() => RunAsync(async store =>
	{
		var consumerId = CreateConsumerId();

		var deleted = await store.DeletePositionAsync(consumerId, CancellationToken.None).ConfigureAwait(false);

		if (deleted)
		{
			throw new TestFixtureAssertionException("Deleting a non-existent checkpoint should return false.");
		}
	});

	/// <summary>Verifies deleting one consumer's checkpoint does not affect another's.</summary>
	public virtual Task DeletePosition_DoesNotAffectOtherConsumers() => RunAsync(async store =>
	{
		var consumer1 = CreateConsumerId();
		var consumer2 = CreateConsumerId();
		var position1 = CreateTestPosition(1);
		var position2 = CreateTestPosition(2);

		await store.SavePositionAsync(consumer1, position1, CancellationToken.None).ConfigureAwait(false);
		await store.SavePositionAsync(consumer2, position2, CancellationToken.None).ConfigureAwait(false);

		await store.DeletePositionAsync(consumer1, CancellationToken.None).ConfigureAwait(false);

		var retrieved2 = await store.GetPositionAsync(consumer2, CancellationToken.None).ConfigureAwait(false);

		if (retrieved2 is null || retrieved2.ToToken() != position2.ToToken())
		{
			throw new TestFixtureAssertionException("Consumer 2's checkpoint should not be affected.");
		}
	});

	/// <summary>Verifies <c>GetAllPositionsAsync</c> returns every consumer's checkpoint.</summary>
	public virtual Task GetAllPositions_ReturnsAllConsumerCheckpoints() => RunAsync(async store =>
	{
		var consumers = new List<(string Id, ChangePosition Position)>();
		for (int i = 0; i < 3; i++)
		{
			var consumerId = CreateConsumerId();
			var position = CreateTestPosition(i + 1);
			consumers.Add((consumerId, position));
			await store.SavePositionAsync(consumerId, position, CancellationToken.None).ConfigureAwait(false);
		}

		var allPositions = new List<(string ConsumerId, ChangePosition Position)>();
		await foreach (var entry in store.GetAllPositionsAsync(CancellationToken.None).ConfigureAwait(false))
		{
			allPositions.Add(entry);
		}

		if (allPositions.Count < 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 3 positions but found {allPositions.Count}.");
		}

		foreach (var (consumerId, _) in consumers)
		{
			if (!allPositions.Exists(p => p.ConsumerId == consumerId))
			{
				throw new TestFixtureAssertionException($"Expected consumer '{consumerId}' to be present.");
			}
		}
	});

	/// <summary>Verifies <c>GetAllPositionsAsync</c> on an empty store yields nothing.</summary>
	public virtual Task GetAllPositions_EmptyStore_ReturnsEmpty() => RunAsync(async store =>
	{
		var allPositions = new List<(string ConsumerId, ChangePosition Position)>();
		await foreach (var entry in store.GetAllPositionsAsync(CancellationToken.None).ConfigureAwait(false))
		{
			allPositions.Add(entry);
		}

		if (allPositions.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected an empty store to yield 0 positions but found {allPositions.Count}.");
		}
	});

	/// <summary>Verifies concurrent saves across many consumers all succeed.</summary>
	public virtual Task ConcurrentSavePosition_AllSucceed() => RunAsync(async store =>
	{
		const int concurrentConsumers = 10;
		var consumers = Enumerable.Range(0, concurrentConsumers)
			.Select(i => (Id: CreateConsumerId(), Position: CreateTestPosition(i)))
			.ToList();

		var tasks = consumers.Select(c => store.SavePositionAsync(c.Id, c.Position, CancellationToken.None));
		await Task.WhenAll(tasks).ConfigureAwait(false);

		foreach (var (id, position) in consumers)
		{
			var retrieved = await store.GetPositionAsync(id, CancellationToken.None).ConfigureAwait(false);
			if (retrieved is null || retrieved.ToToken() != position.ToToken())
			{
				throw new TestFixtureAssertionException($"Consumer '{id}' should have its saved position.");
			}
		}
	});

	/// <summary>Verifies concurrent saves to the same consumer converge to a valid last-write-wins position.</summary>
	public virtual Task ConcurrentSavePosition_SameConsumer_LastWriteWins() => RunAsync(async store =>
	{
		var consumerId = CreateConsumerId();
		const int concurrentWrites = 10;

		var tasks = Enumerable.Range(0, concurrentWrites)
			.Select(i => store.SavePositionAsync(consumerId, CreateTestPosition(i), CancellationToken.None));
		await Task.WhenAll(tasks).ConfigureAwait(false);

		var retrieved = await store.GetPositionAsync(consumerId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null || !retrieved.IsValid)
		{
			throw new TestFixtureAssertionException("Should have a valid position after concurrent writes.");
		}
	});

}
