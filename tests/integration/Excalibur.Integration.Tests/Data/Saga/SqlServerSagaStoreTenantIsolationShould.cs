// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.Saga;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// bd-cd5m5c — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-SQL-Server tenant-isolation lock
/// for the row-discriminator saga store. This sprint added an ambient-<see cref="ITenantContext"/> predicate
/// (<c>AND target.TenantId = @TenantId</c> on the Save MERGE, <c>AND TenantId = @TenantId</c> on Load) so a
/// saga operation under one tenant can never match, load, or overwrite another tenant's saga with the same id.
/// </summary>
/// <remarks>
/// Proven against real SQL Server (a mock cannot reproduce the MERGE match / predicate) and never skipped.
/// Runs against the table the fixture provisions from the SHIPPED <c>Scripts/01-SagaSchema.sql</c>, so a green
/// here is a statement about the schema a consumer actually receives. This class previously restated the table
/// inline because the shipped primary key was <c>SagaId</c> alone and could not express two tenants holding the
/// same id; the shipped key is now the composite <c>(TenantId, SagaId)</c>, so that reason is gone — and a
/// restatement is worse than useless, because it drifts. It already had: the local copy still declared
/// <c>CompletedAt</c> as <c>DATETIME2</c> after the shipped column became <c>DATETIMEOFFSET(7)</c>, and nothing
/// here read it, so no test could go red.
/// <para>
/// <b>RED on the mutant</b> that drops the tenant predicate: from Load, tenant B's <c>LoadAsync</c> would return
/// tenant A's saga (or the Save MERGE would match/overwrite it across tenants) — the cross-tenant access this
/// lock forbids.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Collection("SqlServer SagaStore Integration Tests")]
public sealed class SqlServerSagaStoreTenantIsolationShould : IClassFixture<SqlServerSagaStoreContainerFixture>
{
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	private readonly SqlServerSagaStoreContainerFixture _fixture;

	public SqlServerSagaStoreTenantIsolationShould(SqlServerSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Admit_the_same_saga_id_once_per_tenant_and_isolate_loads()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		var storeA = CreateStore(TenantA);
		var storeB = CreateStore(TenantB);

		var sagaId = Guid.NewGuid();

		// Both tenants persist a saga under the SAME id — each is a distinct row (isolation on save; the MERGE's
		// ON includes AND target.TenantId = @TenantId, so tenant B does not match/overwrite tenant A's row).
		await storeA.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);
		await storeB.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantB }, CancellationToken.None)
			.ConfigureAwait(false);

		// Each tenant loads ONLY its own saga (Load's WHERE includes AND TenantId = @TenantId).
		var loadedA = await storeA.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		var loadedB = await storeB.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);

		_ = loadedA.ShouldNotBeNull();
		_ = loadedB.ShouldNotBeNull();
		loadedA!.TenantId.ShouldBe(TenantA, "tenant A must load its own saga, never tenant B's");
		loadedB!.TenantId.ShouldBe(TenantB, "tenant B must load its own saga, never tenant A's");
	}

	[Fact]
	public async Task Not_let_one_tenant_load_another_tenants_saga()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		var storeA = CreateStore(TenantA);
		var storeB = CreateStore(TenantB);

		var sagaId = Guid.NewGuid();

		// Only tenant A has a saga with this id.
		await storeA.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		// Tenant B loading the SAME id must get NOTHING — the tenant predicate scopes A's row away.
		// RED on the drop-TenantId-from-Load mutant (tenant B would receive tenant A's saga).
		var crossTenantLoad = await storeB.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		crossTenantLoad.ShouldBeNull("tenant B must not be able to load tenant A's saga by id");
	}

	private SqlServerSagaStore CreateStore(string tenantId)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — real-infra saga tenant-isolation lock is never skipped.");

		var options = Options.Create(new SqlServerSagaStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			TableName = _fixture.TableName,
		});

		return new SqlServerSagaStore(
			_fixture.ConnectionString,
			options,
			NullLogger<SqlServerSagaStore>.Instance,
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
