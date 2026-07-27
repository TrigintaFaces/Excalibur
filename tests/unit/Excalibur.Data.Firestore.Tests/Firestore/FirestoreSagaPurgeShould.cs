// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Structural lock for <see cref="Excalibur.Saga.Firestore.FirestoreSagaStore"/>'s
/// <c>PurgeCompletedBeforeAsync</c> running-saga-exclusion guard (bead lq1sv1).
/// </summary>
/// <remarks>
/// <para>
/// Reference: the cross-provider purge contract asserted behaviorally by the shared
/// <c>SagaStoreConformanceTestBase</c> purge test (d0wpug/w8aqq3). Unlike DynamoDB (mockable
/// <c>IAmazonDynamoDB</c>) and Cosmos (mockable <c>CosmosClient</c> chain), Firestore's query path runs
/// through sealed concrete SDK types (<c>FirestoreDb</c>, <c>CollectionReference</c>, <c>Query</c>) that
/// cannot be faked and require a live project/emulator — so the behavioral deletion/count round-trip is
/// EMULATOR-DEFERRED (infra-gated: 63xsiv / 6hapy6), and this unit-level lock instead pins the
/// load-bearing FILTER construction structurally.
/// </para>
/// <para>
/// Contract guard: the running-saga exclusion is STRUCTURAL — a Firestore range filter matches ONLY
/// documents that contain the field, and a running saga never writes <c>completedAt</c>, so a
/// <c>WhereLessThan("completedAt", cutoff)</c> query cannot return a running saga. This lock asserts that
/// exact predicate is what the purge builds.
/// </para>
/// <para>
/// NON-VACUITY: a mutant that broadens the purge to an unfiltered collection query (returning ALL
/// documents, including running sagas), or that filters on the wrong field, removes the
/// <c>WhereLessThan("completedAt"</c> range predicate — turning the assertion below RED.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
public sealed class FirestoreSagaPurgeShould
{
	private static string ReadStoreSource([CallerFilePath] string callerPath = "")
	{
		var path = Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(callerPath)!,
			"..", "..", "..", "..",
			"src", "Excalibur", "Excalibur.Saga.Firestore", "FirestoreSagaStore.cs"));
		File.Exists(path).ShouldBeTrue($"expected Firestore saga store source at {path}");
		return File.ReadAllText(path);
	}

	[Fact]
	public void Purge_QueriesCompletedAtRangeFilter_StructurallyExcludingRunningSagas()
	{
		// Assert against the WHOLE store source, not a sliced method body: the public
		// PurgeCompletedBeforeAsync delegates the query construction to PurgeAllTenantsCompletedBeforeAsync,
		// so a slice of the public method alone misses the filter (the exact split that previously made this
		// lock extract only the signature). WhereLessThan("completedAt", ...) appears ONLY on the purge path
		// in this store, so a whole-source assertion pins the load-bearing predicate robustly.
		var source = ReadStoreSource();

		// The load-bearing guard: a Firestore range filter on completedAt (a running saga never writes the
		// field, so it is structurally unreachable) is what the purge builds, and the deleted set derives
		// from that FILTERED query's snapshot.
		source.ShouldContain("WhereLessThan(\"completedAt\", cutoff)");
		source.ShouldContain("query.GetSnapshotAsync");
	}

	[Fact]
	public void Purge_DoesNotIssueAnUnfilteredCollectionScan()
	{
		// A bare Snapshot of the whole collection (no completedAt filter) would sweep running sagas — the
		// purge must derive its snapshot from the filtered query, never the raw collection.
		var source = ReadStoreSource();

		source.Contains("_collection!.GetSnapshotAsync", StringComparison.Ordinal).ShouldBeFalse(
			"purge must snapshot the completedAt-filtered query, never the whole collection");
	}
}
