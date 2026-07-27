// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.Saga;

#pragma warning disable CA2100 // SQL strings use a compile-time const table name in a test fixture.

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// bd-cd5m5c — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-SQL-Server tenant-isolation lock
/// for the row-discriminator saga store. This sprint added an ambient-<see cref="ITenantContext"/> predicate
/// (<c>AND target.TenantId = @TenantId</c> on the Save MERGE, <c>AND TenantId = @TenantId</c> on Load) so a
/// saga operation under one tenant can never match, load, or overwrite another tenant's saga with the same id.
/// </summary>
/// <remarks>
/// Proven against real SQL Server (a mock cannot reproduce the MERGE match / predicate) and never skipped.
/// Uses an isolated table whose PK is the full composite <c>(SagaId, TenantId)</c> so two tenants can hold the
/// same <c>SagaId</c> as distinct rows — otherwise the canonical <c>SagaId</c>-only PK would collide before the
/// tenant predicate is even exercised.
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
	private const string SchemaName = "dbo";
	private const string TableName = "saga_tenant_isolation_test";
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
		await EnsureTableAsync().ConfigureAwait(false);
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
		await EnsureTableAsync().ConfigureAwait(false);
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
			SchemaName = SchemaName,
			TableName = TableName,
		});

		return new SqlServerSagaStore(
			_fixture.ConnectionString,
			options,
			NullLogger<SqlServerSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(tenantId));
	}

	private async Task EnsureTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Isolated table: PK is the full composite (SagaId, TenantId) with TenantId NOT NULL, so two tenants'
		// identical SagaIds are distinct rows and the tenant dimension of the Save-MERGE / Load predicate is what
		// is under test (not a bare SagaId PK collision). That PK is the one thing this table may not inherit
		// from the shipped Scripts/01-SagaSchema.sql, which is why the columns are restated here rather than
		// provisioned from it as SqlServerSagaStoreContainerFixture now does.
		//
		// Restating them makes this a copy, and a copy drifts: CompletedAt stood at DATETIME2 here after the
		// shipped column became DATETIMEOFFSET(7), because nothing in this class reads it and no test could go
		// red. Column types below must track the script. Where a column is not exercised by these tests, that
		// is a reason to keep it honest, not a licence to let it rot.
		var sql = $"""
			IF OBJECT_ID('[{SchemaName}].[{TableName}]', 'U') IS NOT NULL DROP TABLE [{SchemaName}].[{TableName}];
			CREATE TABLE [{SchemaName}].[{TableName}] (
				SagaId      UNIQUEIDENTIFIER NOT NULL,
				SagaType    NVARCHAR(500)    NOT NULL,
				StateJson   NVARCHAR(MAX)    NOT NULL,
				IsCompleted BIT              NOT NULL DEFAULT 0,
				TenantId    NVARCHAR(200)    NOT NULL,
				Version     BIGINT           NOT NULL DEFAULT 0,
				CreatedUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
				UpdatedUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
				CompletedAt DATETIMEOFFSET(7) NULL,
				RowVersion  ROWVERSION       NOT NULL,
				CONSTRAINT [PK_{TableName}] PRIMARY KEY CLUSTERED (SagaId, TenantId)
			);
			""";

		await using var command = new SqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
