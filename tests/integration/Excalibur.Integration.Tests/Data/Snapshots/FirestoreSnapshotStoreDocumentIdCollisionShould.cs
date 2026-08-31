// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Snapshots;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-Firestore lock on the document-id collision: two tenants whose (tenant, aggregate type) pairs
/// differ only in where the separator falls must address two different documents, not one.
/// </summary>
/// <remarks>
/// <para>
/// The store joins its id segments with <c>_</c>, so unless the separator is escaped inside a segment the
/// id stops being injective across the segment boundary: tenant <c>a</c> with type <c>b_c</c> and tenant
/// <c>a_b</c> with type <c>c</c> render the same id. The store keeps ONE document per aggregate and
/// addresses it by that id alone, so a shared id is not a cosmetic clash — it is one tenant reading, and
/// then overwriting, another tenant's snapshot.
/// </para>
/// <para>
/// The sibling unit lock asserts injectivity of the id function directly. This asserts the consequence
/// that actually matters, against a live emulator: what a tenant reads back is what that tenant wrote.
/// Both arms are present — SAFETY (neither tenant sees the other's snapshot) and LIVENESS (each tenant
/// does get its own back, so a store that returned nothing to anybody would fail here).
/// </para>
/// <para>
/// Never skipped: the fixture requires Docker, so a missing emulator is a failure rather than a silent
/// pass.
/// </para>
/// </remarks>
[Collection(FirestoreSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
public sealed class FirestoreSnapshotStoreDocumentIdCollisionShould
{
	private readonly FirestoreSnapshotStoreContainerFixture _fixture;
	private readonly string _collectionName = "snapshots_idcollision_" + Guid.NewGuid().ToString("N");

	public FirestoreSnapshotStoreDocumentIdCollisionShould(FirestoreSnapshotStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// SAFETY and LIVENESS together. Tenant <c>a</c> stores an <c>b_c</c> snapshot and tenant <c>a_b</c>
	/// stores a <c>c</c> snapshot for the same aggregate id. Each must read back its own payload.
	/// </summary>
	/// <remarks>
	/// Written as one test because the two arms are two readings of the same pair of writes: separating
	/// them would need the writes performed twice and would let the safety arm pass over a store that
	/// returns nothing at all.
	/// </remarks>
	[Fact]
	public async Task KeepTwoTenantsSnapshotsApartWhenTheSeparatorFallsInsideASegment()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the Firestore emulator must be available — this is a real-infrastructure lock and is never skipped.");

		const string AggregateId = "agg-1";

		var storeForTenantA = CreateStore("a");
		var storeForTenantAb = CreateStore("a_b");

		var snapshotOfTenantA = CreateSnapshot(AggregateId, aggregateType: "b_c", version: 7, payload: 0xAA);
		var snapshotOfTenantAb = CreateSnapshot(AggregateId, aggregateType: "c", version: 7, payload: 0xBB);

		await storeForTenantA.SaveSnapshotAsync(snapshotOfTenantA, CancellationToken.None).ConfigureAwait(false);
		await storeForTenantAb.SaveSnapshotAsync(snapshotOfTenantAb, CancellationToken.None).ConfigureAwait(false);

		var readBackByTenantA = await storeForTenantA
			.GetLatestSnapshotAsync(AggregateId, "b_c", CancellationToken.None).ConfigureAwait(false);
		var readBackByTenantAb = await storeForTenantAb
			.GetLatestSnapshotAsync(AggregateId, "c", CancellationToken.None).ConfigureAwait(false);

		// LIVENESS FIRST. Each tenant must actually get a snapshot back. Without this the safety
		// assertions below are satisfied by a store that stores nothing and returns null to everyone.
		_ = readBackByTenantA.ShouldNotBeNull(
			"tenant 'a' must read back the snapshot it wrote — a store that returns nothing to anybody "
			+ "would satisfy the isolation assertion below while being entirely broken.");
		_ = readBackByTenantAb.ShouldNotBeNull(
			"tenant 'a_b' must read back the snapshot it wrote.");

		// SAFETY. Each tenant's payload must be its OWN. If the two triples share a document, the second
		// write overwrote the first and both tenants now read 0xBB.
		readBackByTenantA.Data.ToArray().ShouldBe(
			[(byte)0xAA],
			"tenant 'a' (type 'b_c') read back a payload it did not write. Its snapshot and tenant 'a_b''s "
			+ "(type 'c') are sharing one document, so the later write destroyed the earlier one — a "
			+ "cross-tenant overwrite, not a formatting clash.");
		readBackByTenantAb.Data.ToArray().ShouldBe(
			[(byte)0xBB],
			"tenant 'a_b' (type 'c') read back a payload it did not write; the two tenants are sharing "
			+ "one document.");

		readBackByTenantA.AggregateType.ShouldBe(
			"b_c",
			"the snapshot tenant 'a' reads back must be the one it wrote, not tenant 'a_b''s.");
		readBackByTenantAb.AggregateType.ShouldBe(
			"c",
			"the snapshot tenant 'a_b' reads back must be the one it wrote, not tenant 'a''s.");
	}

	/// <summary>
	/// SAFETY. A delete issued by one tenant must not remove the other tenant's snapshot. Sharing a
	/// document makes one tenant able to destroy the other's data outright, which the read-side arm above
	/// would not by itself detect.
	/// </summary>
	[Fact]
	public async Task NotLetOneTenantsDeleteRemoveTheOtherTenantsSnapshot()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the Firestore emulator must be available — this is a real-infrastructure lock and is never skipped.");

		const string AggregateId = "agg-2";

		var storeForTenantA = CreateStore("a");
		var storeForTenantAb = CreateStore("a_b");

		await storeForTenantA
			.SaveSnapshotAsync(CreateSnapshot(AggregateId, "b_c", 7, 0xAA), CancellationToken.None)
			.ConfigureAwait(false);
		await storeForTenantAb
			.SaveSnapshotAsync(CreateSnapshot(AggregateId, "c", 7, 0xBB), CancellationToken.None)
			.ConfigureAwait(false);

		await storeForTenantAb
			.DeleteSnapshotsAsync(AggregateId, "c", CancellationToken.None)
			.ConfigureAwait(false);

		var survivingSnapshotOfTenantA = await storeForTenantA
			.GetLatestSnapshotAsync(AggregateId, "b_c", CancellationToken.None).ConfigureAwait(false);

		_ = survivingSnapshotOfTenantA.ShouldNotBeNull(
			"tenant 'a_b' deleted its own snapshot and tenant 'a''s disappeared with it. The two are "
			+ "sharing one document, so one tenant can destroy another tenant's data.");

		// LIVENESS for the delete itself: it must genuinely have removed the caller's own snapshot,
		// or this test would also pass over a store whose delete does nothing at all.
		var deletedSnapshotOfTenantAb = await storeForTenantAb
			.GetLatestSnapshotAsync(AggregateId, "c", CancellationToken.None).ConfigureAwait(false);

		deletedSnapshotOfTenantAb.ShouldBeNull(
			"tenant 'a_b' deleted its own snapshot, so its own read must now find nothing. A delete that "
			+ "does nothing would make the isolation assertion above pass for the wrong reason.");
	}

	private FirestoreSnapshotStore CreateStore(string tenantId) =>
		new(
			_fixture.Db,
			Options.Create(new FirestoreSnapshotStoreOptions
			{
				ProjectId = _fixture.ProjectId,
				CollectionName = _collectionName,
				EmulatorHost = _fixture.EmulatorEndpoint,
			}),
			NullLogger<FirestoreSnapshotStore>.Instance,
			new FixedTenantContext(tenantId));

	private static Snapshot CreateSnapshot(string aggregateId, string aggregateType, long version, byte payload) =>
		new()
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			AggregateType = aggregateType,
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
