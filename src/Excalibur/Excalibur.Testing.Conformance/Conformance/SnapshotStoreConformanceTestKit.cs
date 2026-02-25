// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ISnapshotStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your snapshot store implementation conforms to the ISnapshotStore contract.
/// </para>
/// <para>
/// The test kit verifies core snapshot operations including save, load, delete,
/// and versioning behavior.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerSnapshotStoreConformanceTests : SnapshotStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override ISnapshotStore CreateStore() =>
///         new SqlServerSnapshotStore(_fixture.ConnectionString);
///
///     protected override async Task CleanupAsync() =>
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class SnapshotStoreConformanceTestKit
{
	private const string DefaultAggregateType = "TestAggregate";

	/// <summary>
	/// Creates a fresh snapshot store instance for testing.
	/// </summary>
	/// <returns>An ISnapshotStore implementation to test.</returns>
	protected abstract ISnapshotStore CreateStore();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();
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

	#endregion

	#region Delete Tests

	/// <summary>
	/// Verifies that DeleteSnapshots removes all snapshots for an aggregate.
	/// </summary>
	public virtual async Task DeleteSnapshots_ShouldRemoveAll()
	{
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();
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

		if (loaded.Data is null || loaded.Data.Length == 0)
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
}
