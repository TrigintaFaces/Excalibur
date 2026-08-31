// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using System.Linq;
using System.Text;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ISnapshotStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStoreAsync"/> to verify that
/// your snapshot store implementation conforms to the ISnapshotStore contract.
/// </para>
/// <para>
/// The test kit verifies core snapshot operations including save, load, delete,
/// and versioning behavior.
/// </para>
/// <para>
/// <b>This kit is trim-excluded, not trim-safe, and that is a statement about the snapshot-store contract
/// rather than about the kit.</b> The arms save snapshots and reload them through the store, and a conformant store deserializes each one into the consumer's own snapshot type. No annotation on this kit can reach
/// those types, so a deriving suite must itself carry
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"/> — or suppress the
/// warning deliberately — when it is compiled with the trim analyzer enabled. Overriding an arm
/// rather than wrapping it requires the same annotation on the override. A trimmed test host is not
/// a supported configuration for this kit.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerSnapshotStoreConformanceTests : SnapshotStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override async Task&lt;ISnapshotStore&gt; CreateStoreAsync() =>
///         new SqlServerSnapshotStore(_fixture.ConnectionString, logger, tenantContext);
///
///     protected override async Task CleanupAsync() =>
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
	"Snapshot conformance arms save and reload snapshots through the store, which deserializes each one into the consumer's own snapshot type reflectively. A trimmed test host is not a supported configuration for this kit.")]
public abstract class SnapshotStoreConformanceTestKit : ConformanceTestKit
{
	private const string DefaultAggregateType = "TestAggregate";

	/// <summary>Characters in the large-payload arm's state.</summary>
	private const int LargePayloadCharacters = 100_000;

	/// <summary>Concurrent callers used by the concurrency arms.</summary>
	private const int ConcurrentReaders = 20;

	/// <summary>Separator and quoting characters an identifier must survive.</summary>
	private const string SeparatorRichPrefix = "id:colon/slash|pipe'quote-";

	/// <summary>First tenant used by the isolation arms.</summary>
	private const string TenantA = "conformance-tenant-a";

	/// <summary>Second tenant used by the isolation arms.</summary>
	private const string TenantB = "conformance-tenant-b";

	/// <summary>
	/// Creates a fresh snapshot store instance for testing.
	/// </summary>
	/// <returns>An ISnapshotStore implementation to test.</returns>
	/// <remarks>
	/// Asynchronous because every real provider needs it: a container has to start, a database and
	/// collection have to exist, a client has to connect. The synchronous seam this replaces is the
	/// reason no provider could derive this kit -- a suite that must await its own setup cannot
	/// implement it, so the shipped kit had no derivers while the contract went untested for consumers.
	/// </remarks>
	protected abstract Task<ISnapshotStore> CreateStoreAsync();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Clears residual data before each arm, leaving the store returned by <see cref="CreateStoreAsync"/> usable.
	/// </summary>
	/// <returns>A task that completes when residual data has been cleared.</returns>
	/// <remarks>
	/// <para>
	/// Defaults to <see cref="CleanupAsync"/>, which is correct for any suite whose teardown only deletes
	/// rows, keys or documents. A suite whose <see cref="CleanupAsync"/> <em>also</em> disposes a
	/// connection or client MUST override this with the data-only half - otherwise it disposes the store
	/// the arm is about to use, and every arm fails on a disposed handle rather than on the contract.
	/// </para>
	/// <para>
	/// Resetting <em>before</em> an arm is what makes the arm independent; resetting only afterwards makes
	/// every arm's starting state a function of whether its predecessor finished cleanly.
	/// </para>
	/// </remarks>
	protected virtual Task ResetDataAsync() => CleanupAsync();

	/// <summary>
	/// Creates the store for a single arm and clears residual data before the arm runs.
	/// </summary>
	/// <returns>A store ready for one conformance arm.</returns>
	/// <remarks>
	/// Every arm in this kit obtains its store here rather than from <see cref="CreateStoreAsync"/> directly.
	/// That is the only thing that causes <see cref="CleanupAsync"/> to run: a cleanup a deriver overrides
	/// but the kit never calls is indistinguishable, from the deriver's side, from one that works.
	/// </remarks>
	protected async Task<ISnapshotStore> CreateStoreForArmAsync()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>
	/// Creates a test snapshot with the given parameters.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="version">The snapshot version.</param>
	/// <param name="state">Optional state data.</param>
	/// <returns>A test snapshot.</returns>
	protected virtual ISnapshot CreateTestSnapshot(
		string aggregateId,
		string aggregateType,
		long version,
		string? state = null) =>
		TestSnapshot.Create(aggregateId, aggregateType, version, state);

	/// <summary>
	/// Generates a unique aggregate ID for test isolation.
	/// </summary>
	/// <returns>A unique aggregate identifier.</returns>
	protected virtual string GenerateAggregateId() => Guid.NewGuid().ToString();

	#region Get/Save Tests

	/// <summary>
	/// Verifies that getting a snapshot for a non-existent aggregate returns null.
	/// </summary>
	public virtual async Task GetLatestSnapshotAsync_NoSnapshot_ShouldReturnNull()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		var snapshot = await store.GetLatestSnapshotAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (snapshot is not null)
		{
			throw new TestFixtureAssertionException(
				$"Expected null for non-existent aggregate but got snapshot at version {snapshot.Version}");
		}
	}

	/// <summary>
	/// Verifies that a saved snapshot can be retrieved.
	/// </summary>
	public virtual async Task SaveAndGetLatestSnapshot_ShouldRoundTrip()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var snapshot = CreateTestSnapshot(aggregateId, DefaultAggregateType, 5);

		await store.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected snapshot but got null");
		}

		if (loaded.AggregateId != aggregateId)
		{
			throw new TestFixtureAssertionException(
				$"AggregateId mismatch: expected {aggregateId}, got {loaded.AggregateId}");
		}

		if (loaded.Version != 5)
		{
			throw new TestFixtureAssertionException(
				$"Version mismatch: expected 5, got {loaded.Version}");
		}

		if (loaded.AggregateType != DefaultAggregateType)
		{
			throw new TestFixtureAssertionException(
				$"AggregateType mismatch: expected {DefaultAggregateType}, got {loaded.AggregateType}");
		}
	}

	/// <summary>
	/// Verifies that GetLatestSnapshot returns the highest version snapshot.
	/// </summary>
	public virtual async Task GetLatestSnapshot_MultipleVersions_ShouldReturnLatest()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 5),
			CancellationToken.None).ConfigureAwait(false);

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 10),
			CancellationToken.None).ConfigureAwait(false);

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 15),
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected snapshot but got null");
		}

		if (loaded.Version != 15)
		{
			throw new TestFixtureAssertionException(
				$"Expected latest version 15 but got {loaded.Version}");
		}
	}

	/// <summary>
	/// Verifies that saving a new snapshot replaces the old one (or keeps both, depending on implementation).
	/// </summary>
	public virtual async Task SaveSnapshot_ShouldUpdateLatest()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 5, "state-v5"),
			CancellationToken.None).ConfigureAwait(false);

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 10, "state-v10"),
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected snapshot but got null");
		}

		if (loaded.Version != 10)
		{
			throw new TestFixtureAssertionException(
				$"Expected version 10 after update but got {loaded.Version}");
		}
	}

	/// <summary>
	/// Verifies that saving a version LOWER THAN OR EQUAL to the stored one is a successful no-op:
	/// the call returns normally and the higher stored snapshot is left readable.
	/// </summary>
	/// <returns>A task that represents the asynchronous arm.</returns>
	/// <remarks>
	/// <para>
	/// This is the clause of the contract a store is most likely to get wrong in the DANGEROUS
	/// direction, and until now no arm bound it. Two failures are possible and they are opposites:
	/// a store that lets the stale save WIN goes backwards, serving a version it had already
	/// superseded; a store that TREATS IT AS AN ERROR turns an ordinary interleaving -- two writers,
	/// the newer landing first -- into a fault the caller must handle, when the outcome it asked for
	/// (a version at least this high is readable) already holds.
	/// </para>
	/// <para>
	/// Both halves are asserted, because either alone is satisfiable by a store that is simply wrong
	/// in the other direction: "does not throw" is satisfied by a store that accepts the stale write,
	/// and "keeps the higher version" is satisfied by a store that rejects every save outright.
	/// </para>
	/// </remarks>
	public virtual async Task SaveSnapshot_StaleOrEqualVersion_ShouldBeASuccessfulNoOp()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 7, "state-v7"),
			CancellationToken.None).ConfigureAwait(false);

		// LOWER, then EQUAL. Neither may fault, and neither may disturb what is stored.
		foreach (var staleVersion in new long[] { 3, 7 })
		{
			try
			{
				await store.SaveSnapshotAsync(
					CreateTestSnapshot(aggregateId, DefaultAggregateType, staleVersion, "state-stale"),
					CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not TestFixtureAssertionException)
			{
				throw new TestFixtureAssertionException(
					$"Saving version {staleVersion} behind a stored version 7 threw {ex.GetType().Name}. "
					+ "A stale-or-equal save is a SUCCESSFUL NO-OP, not a fault: the caller asked for a "
					+ "version to be readable and a version at least that high already is. Reserve throwing "
					+ "for an argument, a cancellation, or an infrastructure failure that left the outcome "
					+ $"unknown. Inner: {ex.Message}",
					ex);
			}

			var loaded = await store.GetLatestSnapshotAsync(
				aggregateId,
				DefaultAggregateType,
				CancellationToken.None).ConfigureAwait(false);

			if (loaded is null)
			{
				throw new TestFixtureAssertionException(
					$"After a no-op save of version {staleVersion}, the stored snapshot was GONE. The "
					+ "stale save must leave the higher version alone, not delete it.");
			}

			if (loaded.Version != 7)
			{
				throw new TestFixtureAssertionException(
					$"Saving version {staleVersion} behind a stored version 7 left version {loaded.Version} "
					+ "readable. The store went BACKWARDS: a snapshot it had already superseded is being "
					+ "served, so an aggregate rehydrating from it replays from an older checkpoint than the "
					+ "store previously accepted.");
			}
		}
	}

	#endregion

	#region Delete Tests

	/// <summary>
	/// Verifies that DeleteSnapshots removes all snapshots for an aggregate.
	/// </summary>
	public virtual async Task DeleteSnapshots_ShouldRemoveAll()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 5),
			CancellationToken.None).ConfigureAwait(false);

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 10),
			CancellationToken.None).ConfigureAwait(false);

		await store.DeleteSnapshotsAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded is not null)
		{
			throw new TestFixtureAssertionException(
				$"Expected null after delete but got snapshot at version {loaded.Version}");
		}
	}

	/// <summary>
	/// Verifies that DeleteSnapshotsOlderThan preserves newer snapshots.
	/// </summary>
	public virtual async Task DeleteSnapshotsOlderThan_ShouldPreserveNewer()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 5),
			CancellationToken.None).ConfigureAwait(false);

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 10),
			CancellationToken.None).ConfigureAwait(false);

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 15),
			CancellationToken.None).ConfigureAwait(false);

		await store.DeleteSnapshotsOlderThanAsync(
			aggregateId,
			DefaultAggregateType,
			10, // Delete snapshots older than version 10
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException(
				"Expected at least one snapshot to remain but got null");
		}

		// Should still have version 10 or 15 (or both)
		if (loaded.Version < 10)
		{
			throw new TestFixtureAssertionException(
				$"Expected version >= 10 but got {loaded.Version}");
		}
	}

	/// <summary>
	/// Verifies that delete on non-existent aggregate doesn't throw.
	/// </summary>
	public virtual async Task DeleteSnapshots_NonExistent_ShouldNotThrow()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		// Should not throw
		await store.DeleteSnapshotsAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		// Verify still returns null
		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded is not null)
		{
			throw new TestFixtureAssertionException(
				"Expected null for non-existent aggregate");
		}
	}

	#endregion

	#region Isolation Tests

	/// <summary>
	/// Verifies that snapshots are isolated by aggregate type.
	/// </summary>
	public virtual async Task Snapshots_ShouldIsolateByAggregateType()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, "TypeA", 5),
			CancellationToken.None).ConfigureAwait(false);

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, "TypeB", 10),
			CancellationToken.None).ConfigureAwait(false);

		var loadedA = await store.GetLatestSnapshotAsync(
			aggregateId,
			"TypeA",
			CancellationToken.None).ConfigureAwait(false);

		var loadedB = await store.GetLatestSnapshotAsync(
			aggregateId,
			"TypeB",
			CancellationToken.None).ConfigureAwait(false);

		if (loadedA is null)
		{
			throw new TestFixtureAssertionException("Expected TypeA snapshot but got null");
		}

		if (loadedB is null)
		{
			throw new TestFixtureAssertionException("Expected TypeB snapshot but got null");
		}

		if (loadedA.Version != 5)
		{
			throw new TestFixtureAssertionException(
				$"Expected TypeA version 5 but got {loadedA.Version}");
		}

		if (loadedB.Version != 10)
		{
			throw new TestFixtureAssertionException(
				$"Expected TypeB version 10 but got {loadedB.Version}");
		}
	}

	/// <summary>
	/// Verifies that snapshots are isolated by aggregate ID.
	/// </summary>
	public virtual async Task Snapshots_ShouldIsolateByAggregateId()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId1 = GenerateAggregateId();
		var aggregateId2 = GenerateAggregateId();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId1, DefaultAggregateType, 5),
			CancellationToken.None).ConfigureAwait(false);

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId2, DefaultAggregateType, 10),
			CancellationToken.None).ConfigureAwait(false);

		var loaded1 = await store.GetLatestSnapshotAsync(
			aggregateId1,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		var loaded2 = await store.GetLatestSnapshotAsync(
			aggregateId2,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded1 is null || loaded1.Version != 5)
		{
			throw new TestFixtureAssertionException(
				$"Expected aggregate1 version 5 but got {loaded1?.Version}");
		}

		if (loaded2 is null || loaded2.Version != 10)
		{
			throw new TestFixtureAssertionException(
				$"Expected aggregate2 version 10 but got {loaded2?.Version}");
		}
	}

	/// <summary>
	/// Verifies that deleting one aggregate's snapshots doesn't affect others.
	/// </summary>
	public virtual async Task DeleteSnapshots_ShouldNotAffectOtherAggregates()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId1 = GenerateAggregateId();
		var aggregateId2 = GenerateAggregateId();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId1, DefaultAggregateType, 5),
			CancellationToken.None).ConfigureAwait(false);

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId2, DefaultAggregateType, 10),
			CancellationToken.None).ConfigureAwait(false);

		await store.DeleteSnapshotsAsync(
			aggregateId1,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		var loaded2 = await store.GetLatestSnapshotAsync(
			aggregateId2,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded2 is null)
		{
			throw new TestFixtureAssertionException(
				"Expected aggregate2 snapshot to remain but got null");
		}

		if (loaded2.Version != 10)
		{
			throw new TestFixtureAssertionException(
				$"Expected aggregate2 version 10 but got {loaded2.Version}");
		}
	}

	#endregion

	#region Data Integrity Tests

	/// <summary>
	/// Verifies that snapshot data is preserved through round-trip.
	/// </summary>
	public virtual async Task SaveAndLoad_ShouldPreserveData()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var snapshot = CreateTestSnapshot(aggregateId, DefaultAggregateType, 5, "test-state-data");

		await store.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected snapshot but got null");
		}

		if (loaded.Data.Length == 0)
		{
			throw new TestFixtureAssertionException("Snapshot data was not preserved");
		}

		if (loaded.SnapshotId != snapshot.SnapshotId)
		{
			throw new TestFixtureAssertionException(
				$"SnapshotId mismatch: expected {snapshot.SnapshotId}, got {loaded.SnapshotId}");
		}
	}

	#endregion
	/// <summary>
	/// Creates a test snapshot owned by the supplied tenant.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="version">The snapshot version.</param>
	/// <param name="state">The state payload.</param>
	/// <param name="tenantId">The owning tenant.</param>
	/// <returns>A snapshot carrying the requested tenant.</returns>
	protected virtual ISnapshot CreateTenantedTestSnapshot(
		string aggregateId,
		long version,
		string state,
		string? tenantId)
	{
		var seed = TestSnapshot.Create(aggregateId, DefaultAggregateType, version, state);
		return new TestSnapshot
		{
			SnapshotId = seed.SnapshotId,
			AggregateId = seed.AggregateId,
			AggregateType = seed.AggregateType,
			Version = seed.Version,
			CreatedAt = seed.CreatedAt,
			Data = seed.Data,
			Metadata = seed.Metadata,
			TenantId = tenantId,
		};
	}

	/// <summary>Reads a snapshot's payload as text.</summary>
	/// <param name="snapshot">The snapshot to read.</param>
	/// <returns>The payload decoded as UTF-8.</returns>
	protected static string ReadState(ISnapshot snapshot) =>
		Encoding.UTF8.GetString(snapshot.Data.ToArray());

	#region Tenant Isolation

	// One store, TWO ambient scopes -- the topology production actually runs. A store is registered as a
	// singleton and resolves the tenant PER CALL from the ambient context, so building a store per tenant
	// builds two universes and the isolation arms then pass without exercising anything at all. That shape
	// was tried and removed upstream; it is recorded here so nobody re-adds it.

	/// <summary>
	/// SAFETY: a higher-versioned save by another tenant must not overwrite this tenant's snapshot.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// Two tenants can legitimately hold the SAME aggregate id -- that collision is the property under
	/// test, not an edge case. A store whose upsert keys only on the aggregate matches the other tenant's
	/// row and updates it, and the higher version makes a last-writer-wins store lose the earlier one.
	/// </remarks>
	public virtual async Task Snapshots_HigherVersionFromAnotherTenant_MustNotOverwrite()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sharedAggregateId = GenerateAggregateId();

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateTenantedTestSnapshot(sharedAggregateId, 5, "A-data", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantB))
		{
			await store.SaveSnapshotAsync(
				CreateTenantedTestSnapshot(sharedAggregateId, 7, "B-data", TenantB),
				CancellationToken.None).ConfigureAwait(false);
		}

		ISnapshot? readByA;
		using (TenantContextHolder.BeginScope(TenantA))
		{
			readByA = await store.GetLatestSnapshotAsync(
				sharedAggregateId, DefaultAggregateType, CancellationToken.None).ConfigureAwait(false);
		}

		if (readByA is null)
		{
			throw new TestFixtureAssertionException(
				"Tenant A's snapshot is gone after tenant B saved a higher version for the same aggregate id: "
				+ "the save was keyed only on the aggregate, so it replaced another tenant's row.");
		}

		if (!string.Equals(ReadState(readByA), "A-data", StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"Tenant A read tenant B's data ('{ReadState(readByA)}'). A save keyed only on the aggregate "
				+ "OVERWROTE another tenant's row.");
		}

		if (readByA.Version != 5)
		{
			throw new TestFixtureAssertionException(
				$"Tenant A's version is {readByA.Version}, expected 5 -- the two tenants' rows were merged "
				+ "rather than kept distinct.");
		}
	}

	/// <summary>
	/// SAFETY: a save by one tenant must not be discarded because another tenant holds a higher version.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// The mirror of the arm above, and the one a version-guarded store fails: if the guard compares
	/// against the highest version for the AGGREGATE rather than for this tenant's row, the lower-versioned
	/// tenant's save is silently dropped and the caller is told nothing.
	/// </remarks>
	public virtual async Task Snapshots_SaveBehindAnotherTenantsVersion_MustNotBeDiscarded()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sharedAggregateId = GenerateAggregateId();

		using (TenantContextHolder.BeginScope(TenantB))
		{
			await store.SaveSnapshotAsync(
				CreateTenantedTestSnapshot(sharedAggregateId, 9, "B-data", TenantB),
				CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateTenantedTestSnapshot(sharedAggregateId, 2, "A-data", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		ISnapshot? readByA;
		using (TenantContextHolder.BeginScope(TenantA))
		{
			readByA = await store.GetLatestSnapshotAsync(
				sharedAggregateId, DefaultAggregateType, CancellationToken.None).ConfigureAwait(false);
		}

		if (readByA is null || !string.Equals(ReadState(readByA), "A-data", StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"Tenant A's save was discarded because ANOTHER tenant held a higher version for the same "
				+ "aggregate id. A version guard must compare against this tenant's own row, not the "
				+ "aggregate's highest across the estate -- and the caller received no error.");
		}
	}

	/// <summary>
	/// LIVENESS: each tenant reads its own snapshot for an aggregate id both of them use.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// The liveness half of the pair. A store that answers every scoped read with nothing is perfectly
	/// isolated and completely useless, and it satisfies both safety arms above. This is the arm that
	/// fails for it.
	/// </remarks>
	public virtual async Task Snapshots_EachTenant_MustReadItsOwnForASharedAggregateId()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sharedAggregateId = GenerateAggregateId();

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateTenantedTestSnapshot(sharedAggregateId, 3, "A-data", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantB))
		{
			await store.SaveSnapshotAsync(
				CreateTenantedTestSnapshot(sharedAggregateId, 4, "B-data", TenantB),
				CancellationToken.None).ConfigureAwait(false);
		}

		await AssertTenantReadsItsOwnAsync(store, sharedAggregateId, TenantA, "A-data").ConfigureAwait(false);
		await AssertTenantReadsItsOwnAsync(store, sharedAggregateId, TenantB, "B-data").ConfigureAwait(false);
	}

	private async Task AssertTenantReadsItsOwnAsync(
		ISnapshotStore store,
		string aggregateId,
		string tenantId,
		string expected)
	{
		ISnapshot? read;
		using (TenantContextHolder.BeginScope(tenantId))
		{
			read = await store.GetLatestSnapshotAsync(
				aggregateId, DefaultAggregateType, CancellationToken.None).ConfigureAwait(false);
		}

		if (read is null)
		{
			throw new TestFixtureAssertionException(
				$"Tenant '{tenantId}' received NOTHING for an aggregate it saved. A read that returns the "
				+ "empty set is not isolation -- an isolated caller must still receive its own data.");
		}

		if (!string.Equals(ReadState(read), expected, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"Tenant '{tenantId}' read '{ReadState(read)}' but saved '{expected}' -- the two tenants' "
				+ "snapshots for a shared aggregate id are not distinct.");
		}
	}

	#endregion

	#region Concurrency

	/// <summary>
	/// Concurrent saves at rising versions must leave the highest version readable.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// This arm catches a real class of provider defect: a store that resolves concurrent upserts
	/// last-writer-wins rather than highest-version-wins leaves an EARLIER version readable, and a store
	/// whose optimistic-concurrency guard is not retried surfaces a precondition failure to the caller
	/// instead. Both have shipped here on real engines while every in-memory test passed.
	/// </remarks>
	public virtual async Task SaveSnapshot_ConcurrentRisingVersions_ShouldLeaveTheHighestReadable()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		const int ConcurrentWrites = 10;

		var saves = new List<Task>(ConcurrentWrites);
		for (var i = 1; i <= ConcurrentWrites; i++)
		{
			var snapshot = CreateTestSnapshot(aggregateId, DefaultAggregateType, i * 10, $"state-{i}");
			saves.Add(store.SaveSnapshotAsync(snapshot, CancellationToken.None).AsTask());
		}

		await Task.WhenAll(saves).ConfigureAwait(false);

		var latest = await store.GetLatestSnapshotAsync(
			aggregateId, DefaultAggregateType, CancellationToken.None).ConfigureAwait(false);

		if (latest is null)
		{
			throw new TestFixtureAssertionException(
				"No snapshot readable after concurrent saves -- every write was lost.");
		}

		if (latest.Version != ConcurrentWrites * 10)
		{
			throw new TestFixtureAssertionException(
				$"After {ConcurrentWrites} concurrent saves at rising versions, the latest readable version "
				+ $"is {latest.Version} rather than {ConcurrentWrites * 10}. A store that resolves concurrent "
				+ "upserts last-writer-wins rather than highest-version-wins reports an earlier snapshot as "
				+ "current, and a caller rebuilding from it replays from the wrong point.");
		}
	}

	/// <summary>
	/// Concurrent reads of one snapshot must all observe it.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	public virtual async Task GetLatestSnapshot_ConcurrentReaders_ShouldAllObserveIt()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 42, "concurrent-read"),
			CancellationToken.None).ConfigureAwait(false);

		var reads = new List<Task<ISnapshot?>>(ConcurrentReaders);
		for (var i = 0; i < ConcurrentReaders; i++)
		{
			reads.Add(store.GetLatestSnapshotAsync(
				aggregateId, DefaultAggregateType, CancellationToken.None).AsTask());
		}

		var results = await Task.WhenAll(reads).ConfigureAwait(false);

		var missed = results.Count(static r => r is null);
		if (missed > 0)
		{
			throw new TestFixtureAssertionException(
				$"{missed} of {results.Length} concurrent readers saw no snapshot for an aggregate that has "
				+ "one. A read path that is not safe under concurrency returns nothing intermittently, which "
				+ "a caller cannot distinguish from 'no snapshot yet' and answers by replaying the stream.");
		}
	}

	/// <summary>
	/// The store must not fault when many callers arrive at once.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// Mixed concurrent saves and reads across distinct aggregates. This asserts the absence of a fault
	/// rather than a value: a store that serialises everything passes, and that is deliberate -- the arm
	/// exists to catch a connection, buffer or client shared without synchronisation, which surfaces as an
	/// exception rather than as a wrong answer.
	/// </remarks>
	public virtual async Task Store_ShouldNotFaultWhenManyCallersArriveAtOnce()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var work = new List<Task>(ConcurrentReaders * 2);

		for (var i = 0; i < ConcurrentReaders; i++)
		{
			var aggregateId = GenerateAggregateId();
			var snapshot = CreateTestSnapshot(aggregateId, DefaultAggregateType, 1, $"payload-{i}");
			work.Add(store.SaveSnapshotAsync(snapshot, CancellationToken.None).AsTask());
			work.Add(store.GetLatestSnapshotAsync(
				aggregateId, DefaultAggregateType, CancellationToken.None).AsTask());
		}

		try
		{
			await Task.WhenAll(work).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not TestFixtureAssertionException)
		{
			// The CAUSE must survive. Reporting only the outermost type and message loses the fault that
			// actually happened: a provider wraps an infrastructure error in its own exception whose message
			// is generic ("The operation failed."), so an arm that formats just that says a store faulted
			// without saying how, and a reader cannot tell a pool exhaustion from a genuine shared-state
			// race -- which are an environment problem and a product defect respectively. Chain the
			// original as the inner exception AND spell the chain into the message, because a runner that
			// prints only Message would otherwise still hide it.
			throw new TestFixtureAssertionException(
				"The store faulted when many callers arrived at once, which usually means a connection, "
				+ $"buffer or client is shared without synchronisation: {DescribeChain(ex)}",
				ex);
		}
	}

	/// <summary>
	/// Renders an exception and every inner exception as "Type: message" separated by " -> ", so the
	/// originating fault is visible even when only the outermost message is displayed.
	/// </summary>
	/// <param name="exception">The exception whose chain should be described.</param>
	/// <returns>The full type-and-message chain, outermost first.</returns>
	private static string DescribeChain(Exception exception)
	{
		var parts = new List<string>();
		for (var current = exception; current is not null; current = current.InnerException)
		{
			parts.Add($"{current.GetType().Name}: {current.Message}");
		}

		return string.Join(" -> ", parts);
	}

	#endregion

	#region Payload And Identifier Shapes

	/// <summary>
	/// An empty payload must round-trip as empty rather than as null or absent.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// Engine-determined: several stores coerce an empty byte array to NULL on write and return null on
	/// read, so a legitimately empty snapshot becomes a missing one. An in-memory dictionary never does.
	/// </remarks>
	public virtual async Task SaveAndLoad_EmptyPayload_ShouldRoundTripAsEmpty()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 1, string.Empty),
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId, DefaultAggregateType, CancellationToken.None).ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException(
				"A snapshot saved with an empty payload came back as NO snapshot. An empty payload is a "
				+ "value, not an absence; a store that coerces it to null turns a valid snapshot into a "
				+ "missing one and the caller replays the entire stream.");
		}

		if (loaded.Data.Length != 0)
		{
			throw new TestFixtureAssertionException(
				$"An empty payload round-tripped as {loaded.Data.Length} bytes.");
		}
	}

	/// <summary>
	/// A large payload must round-trip byte-for-byte.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// Engine-determined: column width, document size limits and truncation-on-write are properties of the
	/// storage engine. A store that silently truncates returns a snapshot that deserialises into a corrupt
	/// aggregate rather than failing.
	/// </remarks>
	public virtual async Task SaveAndLoad_LargePayload_ShouldRoundTripByteForByte()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var payload = new string('x', LargePayloadCharacters);

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 1, payload),
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId, DefaultAggregateType, CancellationToken.None).ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException(
				$"A snapshot with a {LargePayloadCharacters}-character payload came back as no snapshot.");
		}

		var round = ReadState(loaded);
		if (!string.Equals(round, payload, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"A large payload did not round-trip: saved {payload.Length} characters, read back "
				+ $"{round.Length}. A store that truncates on write returns a snapshot that deserialises "
				+ "into a corrupt aggregate instead of failing.");
		}
	}

	/// <summary>
	/// An aggregate id containing separator and quoting characters must round-trip intact.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// Engine-determined: a store that composes its key by concatenation, or that interpolates an
	/// identifier into a statement, mis-handles exactly these characters. It is also the cheapest
	/// injection canary in the kit.
	/// </remarks>
	public virtual async Task SaveAndLoad_AggregateIdWithSeparatorCharacters_ShouldRoundTrip()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = SeparatorRichPrefix + GenerateAggregateId();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, DefaultAggregateType, 1, "separator-state"),
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId, DefaultAggregateType, CancellationToken.None).ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException(
				$"An aggregate id containing separator characters was not readable back: '{aggregateId}'. A "
				+ "store that composes its key by concatenation loses or collides on these.");
		}

		if (!string.Equals(loaded.AggregateId, aggregateId, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"An aggregate id round-tripped as '{loaded.AggregateId}', expected '{aggregateId}'.");
		}
	}

	#endregion

}
