// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.Oracle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// b52l9e (4vlmds) — real-Oracle lock for the cross-tenant WRITE VICTIM: a scoped save must never
/// overwrite another tenant's saga row that happens to carry the same saga id. Ports the pattern already
/// proven on Postgres (<see cref="PostgresSagaWriteVictimTenantIsolationShould"/>) and SQL Server
/// (<see cref="SqlServerSagaWriteVictimTenantIsolationShould"/>) to Oracle.
/// </summary>
/// <remarks>
/// <para>
/// Oracle's MERGE binds its tenant term unconditionally in the ON clause (<c>SaveSagaRequest{TSagaState}</c>:
/// <c>const string onTenant = " AND target.TenantId = :TenantId";</c>), so the match — and therefore both
/// the UPDATE and INSERT branches — can never address another tenant's row. This lock proves that property
/// against the real MERGE, and pins it against regression: RED on the mutant that drops the term from the
/// match. Uses the SHIPPED schema (<c>Scripts/01-SagaSchema.sql</c>, composite <c>PK (TenantId, SagaId)</c>)
/// through the fixture, not a private table — a green here is a statement about the schema a consumer
/// actually receives.
/// </para>
/// <para>
/// Both directions are asserted because either alone is satisfiable by a broken store: that the victim
/// still reads its own state is the safety half; that the writer's own state actually persisted is the
/// liveness half — a store whose save silently did nothing would leave the victim intact and pass a
/// safety-only arm while being useless.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Oracle")]
[Collection("Oracle SagaStore Integration Tests")]
public sealed class OracleSagaWriteVictimTenantIsolationShould(OracleSagaStoreContainerFixture fixture)
	: IClassFixture<OracleSagaStoreContainerFixture>
{
	private const string VictimTenant = "tenant-write-victim-a";
	private const string WriterTenant = "tenant-write-victim-b";

	[Fact]
	public async Task NotOverwriteAnotherTenantsSagaSharingTheSameSagaId()
	{
		fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available — a cross-tenant overwrite destroys saga state in place, "
			+ "so this lock is never skipped.");

		await fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await fixture.CleanupTableAsync().ConfigureAwait(false);

		var ambient = new MutableTenantContext();
		var store = CreateStore(ambient);

		// The collision the defect needs: one saga id, two tenants.
		var sagaId = Guid.NewGuid();

		// Both tenants save TWICE. The first save inserts (:ExpectedVersion = 0 takes the INSERT branch,
		// whose ON is target.SagaId = source.SagaId AND target.TenantId = :TenantId — a WRITE match, so it
		// cannot address another tenant's row). The second save takes the version-gated UPDATE branch,
		// which is the branch a dropped tenant term would let cross tenants.
		ambient.TenantId = VictimTenant;
		var victimState = await SaveTwiceAsync(store, sagaId, "victim-state").ConfigureAwait(false);

		ambient.TenantId = WriterTenant;
		_ = await SaveTwiceAsync(store, sagaId, "writer-state-first").ConfigureAwait(false);

		// Control: the victim's row is on disk, at the same version the writer's next UPDATE will name, so
		// a later mismatch is attributable to the overwrite rather than to a save that never landed.
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

	private OracleSagaStore CreateStore(ITenantContext ambientTenant) =>
		new(
			fixture.ConnectionString,
			Options.Create(new OracleSagaStoreOptions
			{
				SchemaName = fixture.SchemaName,
				TableName = fixture.TableName,
			}),
			NullLogger<OracleSagaStore>.Instance,
			new DispatchJsonSerializer(),
			ambientTenant);

	private static Task SaveAsync(OracleSagaStore store, Guid sagaId, string payload, long version = 0) =>
		store.SaveAsync(
			new WriteVictimSagaState { SagaId = sagaId, Payload = payload, Version = version },
			CancellationToken.None);

	/// <summary>
	/// Saves twice so the row leaves version 0 and subsequent saves take the version-gated UPDATE branch.
	/// </summary>
	private static async Task<WriteVictimSagaState> SaveTwiceAsync(
		OracleSagaStore store,
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
