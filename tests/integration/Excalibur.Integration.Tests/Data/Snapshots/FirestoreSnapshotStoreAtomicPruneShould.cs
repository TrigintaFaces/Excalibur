// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Data.Firestore.Snapshots;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-Firestore locks on <c>DeleteSnapshotsOlderThanAsync</c> being one atomic step rather than a
/// splittable check-then-act.
/// </summary>
/// <remarks>
/// <para>
/// The store keeps ONE document per aggregate, so "delete if older" and "overwrite with newer" address
/// the same document. If the version test and the delete are separate operations, a newer snapshot
/// landing between them is destroyed: the read observes the stale version, decides to delete, and the
/// delete removes the snapshot that arrived in the meantime. The condition was true when it was
/// evaluated and false when it was acted on.
/// </para>
/// <para>
/// <b>What each test can and cannot establish.</b> The two version-guard tests below are deterministic
/// and prove the prune's decision is correct in isolation — they pass with or without atomicity, and are
/// here as the liveness and safety arms that stop a prune from regressing into a no-op or into an
/// unconditional delete. Only <see cref="NotDestroyASnapshotThatArrivesWhileThePruneIsDeciding"/>
/// distinguishes the atomic implementation from the splittable one, and it does so by racing a real
/// concurrent write against a real prune.
/// </para>
/// <para>
/// <b>Why the race is sound even though it is a race.</b> Its assertion is one-sided: a correct store can
/// never lose the newer snapshot on any interleaving, so this test cannot fail against correct code — it
/// has no flaky-red mode. What a race costs is detection power, not soundness, and detection power is
/// what the attempt count buys. The losing interleaving is narrow (the newer write must land between the
/// read and the delete), so a single attempt would frequently miss it.
/// </para>
/// </remarks>
[Collection(FirestoreSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
public sealed class FirestoreSnapshotStoreAtomicPruneShould
{
	private const string AggregateType = "PrunedAggregate";
	private const string TenantId = "tenant-prune";

	/// <summary>
	/// Attempts made to land a concurrent write inside the prune's decide-then-act window.
	/// </summary>
	/// <remarks>
	/// A budget for detection, not a correctness parameter: raising it only makes the splittable
	/// implementation more likely to be caught, and lowering it can never make a correct store fail.
	/// </remarks>
	private const int RaceAttempts = 40;

	private readonly FirestoreSnapshotStoreContainerFixture _fixture;
	private readonly string _collectionPrefix = "snapshots_prune_" + Guid.NewGuid().ToString("N");

	public FirestoreSnapshotStoreAtomicPruneShould(FirestoreSnapshotStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// The atomicity lock. A newer snapshot written concurrently with a prune must survive, on every
	/// interleaving. A prune that reads the version outside a transaction and then deletes will, on the
	/// interleaving where the write lands between the two, delete the newer snapshot it never saw.
	/// </summary>
	[Fact]
	public async Task NotDestroyASnapshotThatArrivesWhileThePruneIsDeciding()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the Firestore emulator must be available — this is a real-infrastructure lock and is never skipped.");

		var attemptsWhereThePruneCompleted = 0;

		for (var attempt = 1; attempt <= RaceAttempts; attempt++)
		{
			// A collection of its own per attempt, so the single document in it is unambiguous and no
			// residue from an earlier attempt can influence this one.
			var collectionName = $"{_collectionPrefix}_{attempt}";
			var collection = _fixture.Db.Collection(collectionName);
			var store = CreateStore(collectionName);
			var aggregateId = $"agg-{attempt}";

			// Seed the stale snapshot the prune is entitled to delete: version 1 is older than 100.
			await store
				.SaveSnapshotAsync(CreateSnapshot(aggregateId, version: 1, payload: 0x01), CancellationToken.None)
				.ConfigureAwait(false);

			var documentReference = await SingleDocumentOfAsync(collection).ConfigureAwait(false);

			// The newer snapshot, written directly rather than through the store. A plain write, not a
			// transaction: contending two transactions on one document runs into a known emulator defect
			// (parallel transactions on the same document time out and the writes are not persisted),
			// which would make this test measure the emulator rather than the store.
			var newerSnapshotDocument = BuildDocument(aggregateId, version: 200, payload: 0x02);

			var startingGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			var pruneThrew = false;

			var prune = Task.Run(async () =>
			{
				await startingGate.Task.ConfigureAwait(false);
				try
				{
					await store.DeleteSnapshotsOlderThanAsync(
						aggregateId,
						AggregateType,
						olderThanVersion: 100,
						CancellationToken.None).ConfigureAwait(false);
				}
				catch (RpcException)
				{
					// The emulator aborting a contended transaction is not the subject of this test. It is
					// recorded rather than swallowed: the assertion after the loop fails if EVERY attempt
					// ended this way, because a prune that never runs proves nothing.
					pruneThrew = true;
				}
			});

			var writeNewer = Task.Run(async () =>
			{
				await startingGate.Task.ConfigureAwait(false);
				_ = await documentReference.SetAsync(newerSnapshotDocument).ConfigureAwait(false);
			});

			startingGate.SetResult();
			await Task.WhenAll(prune, writeNewer).ConfigureAwait(false);

			if (!pruneThrew)
			{
				attemptsWhereThePruneCompleted++;
			}

			var survivor = await store
				.GetLatestSnapshotAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

			_ = survivor.ShouldNotBeNull(
				$"attempt {attempt}: version 200 was written concurrently with a prune of everything older "
				+ "than version 100, and the snapshot is now gone. The prune read version 1, decided to "
				+ "delete, and the delete then removed the version-200 snapshot that arrived in between — "
				+ "the version test and the delete were not one atomic step, so the condition was true when "
				+ "it was evaluated and false when it was acted on.");

			survivor.Version.ShouldBe(
				200,
				$"attempt {attempt}: the surviving snapshot must be the newer one. Anything else means the "
				+ "prune acted on a version it had already stopped being true.");
		}

		// LIVENESS. Every assertion above is satisfied by a prune that always fails before touching
		// anything. At least one attempt must have run the prune to completion.
		attemptsWhereThePruneCompleted.ShouldBeGreaterThan(
			0,
			$"all {RaceAttempts} attempts ended with the prune throwing, so the assertions above passed "
			+ "over a store that never pruned at all. That is not evidence of atomicity.");
	}

	/// <summary>
	/// LIVENESS for the prune's decision. A prune that never deletes anything would satisfy the atomicity
	/// lock above trivially — nothing can be destroyed by an operation that does nothing.
	/// </summary>
	[Fact]
	public async Task DeleteASnapshotThatIsGenuinelyOlder()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the Firestore emulator must be available — this is a real-infrastructure lock and is never skipped.");

		var collectionName = $"{_collectionPrefix}_liveness";
		var store = CreateStore(collectionName);
		const string AggregateId = "agg-liveness";

		await store
			.SaveSnapshotAsync(CreateSnapshot(AggregateId, version: 1, payload: 0x01), CancellationToken.None)
			.ConfigureAwait(false);

		await store.DeleteSnapshotsOlderThanAsync(
			AggregateId,
			AggregateType,
			olderThanVersion: 100,
			CancellationToken.None).ConfigureAwait(false);

		var afterPrune = await store
			.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

		afterPrune.ShouldBeNull(
			"version 1 is older than 100, so the prune must remove it. A prune that deletes nothing would "
			+ "make the atomicity lock pass for the wrong reason.");
	}

	/// <summary>
	/// SAFETY for the prune's decision, evaluated with no concurrency at all: a snapshot that is not older
	/// than the threshold must survive. Guards against the opposite regression to the one above — an
	/// unconditional delete.
	/// </summary>
	[Fact]
	public async Task LeaveASnapshotThatIsNotOlderThanTheThreshold()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the Firestore emulator must be available — this is a real-infrastructure lock and is never skipped.");

		var collectionName = $"{_collectionPrefix}_safety";
		var store = CreateStore(collectionName);
		const string AggregateId = "agg-safety";

		await store
			.SaveSnapshotAsync(CreateSnapshot(AggregateId, version: 200, payload: 0x02), CancellationToken.None)
			.ConfigureAwait(false);

		await store.DeleteSnapshotsOlderThanAsync(
			AggregateId,
			AggregateType,
			olderThanVersion: 100,
			CancellationToken.None).ConfigureAwait(false);

		var afterPrune = await store
			.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

		_ = afterPrune.ShouldNotBeNull(
			"version 200 is not older than 100, so the prune must leave it alone. Deleting it would be an "
			+ "unconditional delete wearing a version guard.");
		afterPrune.Version.ShouldBe(200);
	}

	private static async Task<DocumentReference> SingleDocumentOfAsync(CollectionReference collection)
	{
		var documents = new List<DocumentReference>();

		await foreach (var document in collection.ListDocumentsAsync().ConfigureAwait(false))
		{
			documents.Add(document);
		}

		documents.Count.ShouldBe(
			1,
			"the seeded snapshot must be the only document in this attempt's collection; the concurrent "
			+ "write below targets it directly, so an ambiguous collection would target the wrong one.");

		return documents[0];
	}

	/// <summary>
	/// The stored shape of a snapshot, matching what the store itself writes, so the store can read the
	/// concurrently-written document back.
	/// </summary>
	private static Dictionary<string, object> BuildDocument(string aggregateId, long version, byte payload) =>
		new()
		{
			["snapshotId"] = Guid.NewGuid().ToString(),
			["aggregateId"] = aggregateId,
			["aggregateType"] = AggregateType,
			["version"] = version,
			["createdAt"] = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
			["data"] = Blob.CopyFrom([payload]),
		};

	private FirestoreSnapshotStore CreateStore(string collectionName) =>
		new(
			_fixture.Db,
			Options.Create(new FirestoreSnapshotStoreOptions
			{
				ProjectId = _fixture.ProjectId,
				CollectionName = collectionName,
				EmulatorHost = _fixture.EmulatorEndpoint,
			}),
			NullLogger<FirestoreSnapshotStore>.Instance,
			new FixedTenantContext(TenantId));

	private static Snapshot CreateSnapshot(string aggregateId, long version, byte payload) =>
		new()
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			AggregateType = AggregateType,
			Version = version,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = new ReadOnlyMemory<byte>([payload]),
		};

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
}
