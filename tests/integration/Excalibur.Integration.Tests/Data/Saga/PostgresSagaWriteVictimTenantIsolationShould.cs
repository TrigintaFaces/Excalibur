// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Real-Postgres lock for the cross-tenant WRITE VICTIM: a scoped save must never overwrite another
/// tenant's saga row that happens to carry the same saga id.
/// </summary>
/// <remarks>
/// <para>
/// Saga ids are chosen by the consumer, so two tenants colliding on one is ordinary rather than
/// exotic. If a save's predicate omits the tenant term, the second tenant's write does not insert a new
/// row — it <em>updates</em> the first tenant's, and the first tenant's saga state is destroyed in place.
/// Nothing errors, both callers see success, and the loss is discovered only when the first tenant's
/// saga fails to resume.
/// </para>
/// <para>
/// Both directions are asserted because either alone is satisfiable by a broken store. That the victim
/// still reads its own state is the safety half; that the writer's own state actually persisted is the
/// liveness half — a store whose save silently did nothing would leave the victim intact and pass a
/// safety-only arm while being useless.
/// </para>
/// <para>
/// One store, ambient tenant switched between operations — the same reasoning as every tenant arm here.
/// Two stores would let an implementation pass by instance separation with no tenant predicate at all.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Postgres")]
[Collection("PostgresSagaStore")]
public sealed class PostgresSagaWriteVictimTenantIsolationShould(PostgresSagaStoreContainerFixture fixture)
{
	private const string VictimTenant = "tenant-write-victim-a";
	private const string WriterTenant = "tenant-write-victim-b";

	[Fact]
	public async Task NotOverwriteAnotherTenantsSagaSharingTheSameSagaId()
	{
		fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — a cross-tenant overwrite destroys saga state in place, "
			+ "so this lock is never skipped.");

		await fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var ambient = new MutableTenantContext();
		var store = CreateStore(ambient);

		// The collision the defect needs: one saga id, two tenants.
		var sagaId = Guid.NewGuid();

		// Both tenants save TWICE. The first save inserts (version 0 takes the INSERT branch, whose
		// ON CONFLICT target is the composite primary key and therefore cannot address another tenant's
		// row at all). The second save takes the version-gated UPDATE branch, whose predicate keys on
		// saga_id + version rather than on the key — that is the only branch where a missing tenant term
		// can reach another tenant's row, so it is the branch this lock has to exercise.
		ambient.TenantId = VictimTenant;
		var victimState = await SaveTwiceAsync(store, sagaId, "victim-state").ConfigureAwait(false);

		ambient.TenantId = WriterTenant;
		_ = await SaveTwiceAsync(store, sagaId, "writer-state-first").ConfigureAwait(false);

		// Control: the victim's row is on disk, at the same version the writer's next UPDATE will name,
		// so a later mismatch is attributable to the overwrite rather than to a save that never landed.
		ambient.TenantId = VictimTenant;
		var beforeOverwrite = await store.LoadAsync<WriteVictimSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		beforeOverwrite.ShouldNotBeNull("the victim's saga must persist before the other tenant writes");
		beforeOverwrite.Payload.ShouldBe("victim-state");
		beforeOverwrite.Version.ShouldBe(
			victimState.Version,
			"the victim must be at a non-zero version, or the writer's next save takes the INSERT branch "
			+ "and never reaches the predicate under test");

		// The overwrite attempt: an UPDATE naming saga_id and a version the victim's row also carries.
		ambient.TenantId = WriterTenant;
		await SaveAsync(store, sagaId, "writer-state", beforeOverwrite.Version).ConfigureAwait(false);

		// SAFETY — the victim still reads its own state. A save whose predicate omits the tenant term
		// updated this row in place, and this reads "writer-state".
		ambient.TenantId = VictimTenant;
		var victim = await store.LoadAsync<WriteVictimSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		victim.ShouldNotBeNull(
			"the victim's saga must still exist after another tenant saved the same saga id; if it is gone "
			+ "the second write replaced the row rather than inserting its own");
		victim.Payload.ShouldBe(
			"victim-state",
			"the victim's saga state was overwritten by another tenant's save — the save predicate is "
			+ "missing its tenant term, and one tenant's saga has silently destroyed another's");

		// LIVENESS — the writer's own state persisted. Without this, a store whose save did nothing at all
		// would satisfy the safety assertion above.
		ambient.TenantId = WriterTenant;
		var writer = await store.LoadAsync<WriteVictimSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		writer.ShouldNotBeNull("the writing tenant's own saga must persist");
		writer.Payload.ShouldBe(
			"writer-state",
			"the writing tenant must read back what it wrote; if it reads the victim's state the save was "
			+ "swallowed and the safety assertion above passed for the wrong reason");
	}

	private PostgresSagaStore CreateStore(ITenantContext ambientTenant) =>
		new(
			Options.Create(new PostgresSagaOptions
			{
				ConnectionString = fixture.ConnectionString,
				Schema = fixture.Schema,
				TableName = fixture.TableName,
				CommandTimeoutSeconds = 30
			}),
			NullLogger<PostgresSagaStore>.Instance,
			new DispatchJsonSerializer(),
			ambientTenant);

	private static Task SaveAsync(PostgresSagaStore store, Guid sagaId, string payload, long version = 0) =>
		store.SaveAsync(
			new WriteVictimSagaState { SagaId = sagaId, Payload = payload, Version = version },
			CancellationToken.None);

	/// <summary>
	/// Saves twice so the row leaves version 0 and subsequent saves take the version-gated UPDATE branch.
	/// </summary>
	private static async Task<WriteVictimSagaState> SaveTwiceAsync(
		PostgresSagaStore store,
		Guid sagaId,
		string payload)
	{
		await SaveAsync(store, sagaId, payload).ConfigureAwait(false);

		var inserted = await store.LoadAsync<WriteVictimSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		inserted.ShouldNotBeNull("the first save must insert the row");

		await SaveAsync(store, sagaId, payload, inserted.Version).ConfigureAwait(false);

		var updated = await store.LoadAsync<WriteVictimSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		updated.ShouldNotBeNull("the second save must update the row");
		return updated;
	}

	private sealed class WriteVictimSagaState : SagaState
	{
		public string Payload { get; set; } = string.Empty;
	}

	private sealed class MutableTenantContext : ITenantContext
	{
		public string? TenantId { get; set; }

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
