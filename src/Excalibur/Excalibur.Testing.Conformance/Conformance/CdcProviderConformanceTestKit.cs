// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

	// A per-store ConcurrentSaveMayRefuseAsStale(Exception) hook used to live here, so the kit could
	// recognize one provider's documented refusal EXCEPTION and tell it from a broken store. It is gone
	// because the contract now carries that distinction itself: SavePositionAsync returns a
	// CdcPositionSaveOutcome, so a refusal is a value every store reports the same way and no store has to
	// teach the kit its own exception type. The hook could also only ever say "this exception is tolerable",
	// which could not separate a deliberate monotonic refusal from a transport error that happened to be
	// listed -- the distinction that let a gRPC ABORTED read as conformance-relevant noise for a while.

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
		var outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Every consumer id is distinct, so these writes touch different keys and nothing can legitimately
		// refuse or contend. This is the liveness half of the arm above: that one proves a refusal is
		// reported, this one proves the store still writes when there is nothing to refuse. Without it, a
		// store that refused everything would satisfy the refusal arm and look conforming.
		var notAdvanced = outcomes.Where(o => o != CdcPositionSaveOutcome.Advanced).ToList();
		if (notAdvanced.Count > 0)
		{
			throw new TestFixtureAssertionException(
				"Saves to DISTINCT consumers must all advance — there is no shared key to refuse over. Got: "
				+ string.Join(", ", notAdvanced));
		}

		foreach (var (id, position) in consumers)
		{
			var retrieved = await store.GetPositionAsync(id, CancellationToken.None).ConfigureAwait(false);
			if (retrieved is null || retrieved.ToToken() != position.ToToken())
			{
				throw new TestFixtureAssertionException($"Consumer '{id}' should have its saved position.");
			}
		}
	});

	/// <summary>
	/// Verifies concurrent saves to the same consumer converge to a valid position, and that every save
	/// REPORTS what it did. This does NOT assert which write wins — nothing orders the concurrent tasks, so
	/// last-write-wins was never observable here — only that each save returns an outcome the caller can act
	/// on, that at least one advanced, and that the final stored position is valid.
	/// </summary>
	/// <remarks>
	/// A store that declines to regress its watermark is choosing the safer of two wrong-looking results and
	/// is conforming; so is a store with no guard that writes unconditionally. What is NOT conforming is a
	/// save that neither advances nor says why, which is what <see cref="CdcPositionSaveOutcome.Unknown"/>
	/// means and why it is rejected here.
	/// </remarks>
	public virtual Task ConcurrentSavePosition_SameConsumer_LastWriteWins() => RunAsync(async store =>
	{
		var consumerId = CreateConsumerId();
		const int concurrentWrites = 10;

		// Not Task.WhenAll: it surfaces only the first fault, and this arm needs the outcome of EVERY task —
		// a store that refuses N-1 of N must still be told apart from one that refused all N.
		var tasks = Enumerable.Range(0, concurrentWrites)
			.Select(i => store.SavePositionAsync(consumerId, CreateTestPosition(i), CancellationToken.None))
			.ToList();

		var outcomes = new List<CdcPositionSaveOutcome>(concurrentWrites);
		foreach (var task in tasks)
		{
			outcomes.Add(await task.ConfigureAwait(false));
		}

		if (outcomes.Contains(CdcPositionSaveOutcome.Unknown))
		{
			throw new TestFixtureAssertionException(
				"A concurrent save reported Unknown, which means the store decided nothing and the caller "
				+ "cannot tell whether the checkpoint was written. A store that declines must say WHY — "
				+ "RefusedAsStale when it compared the positions, Contended when it could not.");
		}

		if (!outcomes.Contains(CdcPositionSaveOutcome.Advanced))
		{
			throw new TestFixtureAssertionException(
				"No concurrent write advanced the checkpoint. A store that refuses all of N concurrent writes "
				+ "is indistinguishable from one that accepts none, which is not the property this arm proves — "
				+ "at least one write must land. Outcomes were: "
				+ string.Join(", ", outcomes));
		}

		var retrieved = await store.GetPositionAsync(consumerId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null || !retrieved.IsValid)
		{
			throw new TestFixtureAssertionException("Should have a valid position after concurrent writes.");
		}
	});

}
