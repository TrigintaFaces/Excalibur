// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.Saga;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// bd-cd5m5c — independent (author≠impl) NON-SKIPPED real-Postgres tenant-isolation lock for the
/// row-discriminator saga store (Postgres sibling of <see cref="SqlServerSagaStoreTenantIsolationShould"/>).
/// This sprint wove an ambient-<see cref="ITenantContext"/> predicate into the store: the version-gated UPDATE
/// adds <c>AND tenant_id = @TenantId</c> and the load adds <c>AND tenant_id = @TenantId</c>, so a saga operation
/// under one tenant can never load or overwrite another tenant's saga with the same id.
/// </summary>
/// <remarks>
/// Proven against real Postgres (a mock cannot reproduce the server-side predicate / row match) and never
/// skipped. The canonical <c>sagas</c> table keys on <c>saga_id</c> alone and the store upserts with
/// <c>ON CONFLICT (saga_id)</c>, so — unlike the SqlServer sibling's composite-PK isolated table — two tenants
/// cannot hold the same <c>saga_id</c> as distinct rows here. The tenant dimension is therefore exercised on the
/// LOAD path: only tenant A persists the saga; tenant B's scoped load of the same id must return nothing.
/// <para>
/// <b>RED on the mutant</b> that drops the tenant predicate from Load: tenant B's <c>LoadAsync</c> would then
/// return tenant A's saga — the cross-tenant read this lock forbids.
/// </para>
/// </remarks>
[Collection("PostgresSagaStore")]
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Postgres")]
public sealed class PostgresSagaStoreTenantIsolationShould : IAsyncLifetime
{
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	private readonly PostgresSagaStoreContainerFixture _fixture;

	public PostgresSagaStoreTenantIsolationShould(PostgresSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — real-infra saga tenant-isolation lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task Not_let_one_tenant_load_another_tenants_saga()
	{
		var storeA = CreateStore(TenantA);
		var storeB = CreateStore(TenantB);

		var sagaId = Guid.NewGuid();

		// Only tenant A persists a saga with this id (version 0 → INSERT), stamping tenant_id = tenant-A.
		await storeA.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		// Tenant B loading the SAME id must get NOTHING — the load's AND tenant_id = @TenantId scopes A's row away.
		// RED on the drop-tenant-predicate-from-Load mutant (tenant B would receive tenant A's saga).
		var crossTenantLoad = await storeB.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		crossTenantLoad.ShouldBeNull("tenant B must not be able to load tenant A's saga by id");
	}

	[Fact]
	public async Task Load_a_saga_within_the_owning_tenant()
	{
		var storeA = CreateStore(TenantA);

		var sagaId = Guid.NewGuid();

		await storeA.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		// The owning tenant loads its own saga — the predicate matches, so isolation must not disable the
		// legitimate same-tenant read.
		var loaded = await storeA.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull();
		loaded!.TenantId.ShouldBe(TenantA, "tenant A must load its own saga within the tenant");
	}

	private PostgresSagaStore CreateStore(string tenantId)
	{
		var options = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Schema = _fixture.Schema,
			TableName = _fixture.TableName,
			CommandTimeoutSeconds = 30,
		});

		return new PostgresSagaStore(
			options,
			NullLogger<PostgresSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(tenantId));
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
