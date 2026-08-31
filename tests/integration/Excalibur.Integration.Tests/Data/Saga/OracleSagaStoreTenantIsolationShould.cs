// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.Oracle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

#pragma warning disable CA2100 // SQL strings use a compile-time const table name in a test fixture.

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Independent (author≠impl) NON-SKIPPED real-Oracle tenant-isolation lock for the row-discriminator saga
/// store. The Oracle saga store threads the ambient <see cref="ITenantContext"/> through
/// <see cref="TenantScope.FromContext(ITenantContext)"/> so its Save MERGE gains
/// <c>AND target.TenantId = :TenantId</c> on the match and its Load gains <c>AND TenantId = :TenantId</c> —
/// a saga operation under one tenant can never match, load, or overwrite another tenant's saga with the same id.
/// </summary>
/// <remarks>
/// Proven against a real Oracle (a mock cannot reproduce the MERGE match / WHERE predicate) and never skipped
/// (<c>DockerAvailable.ShouldBeTrue</c>). Uses an isolated table whose PK is the full composite
/// <c>(SagaId, TenantId)</c> so two tenants can hold the same <c>SagaId</c> as distinct rows — otherwise the
/// canonical <c>SagaId</c>-only PK would collide before the tenant predicate is even exercised.
/// <para>
/// <b>Safety:</b> tenant B cannot load tenant A's saga by id. <b>Liveness:</b> tenant A still loads its own
/// saga (the predicate scopes to — never scopes away — the owning tenant). RED on the mutant that drops the
/// tenant predicate from Load/Save: tenant B's <c>LoadAsync</c> would return tenant A's saga.
/// </para>
/// </remarks>
[Collection("Oracle SagaStore Integration Tests")]
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Oracle")]
public sealed class OracleSagaStoreTenantIsolationShould : IClassFixture<OracleSagaStoreContainerFixture>
{
	private const string SchemaName = "DISPATCH";
	private const string TableName = "SAGA_TENANT_ISOLATION_TEST";
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	private readonly OracleSagaStoreContainerFixture _fixture;

	public OracleSagaStoreTenantIsolationShould(OracleSagaStoreContainerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task Admit_the_same_saga_id_once_per_tenant_and_isolate_loads()
	{
		await EnsureTableAsync().ConfigureAwait(false);
		var storeA = CreateStore(TenantA);
		var storeB = CreateStore(TenantB);

		var sagaId = Guid.NewGuid();

		// Both tenants persist a saga under the SAME id — each is a distinct row (isolation on save; the MERGE's
		// ON includes AND target.TenantId = :TenantId, so tenant B does not match/overwrite tenant A's row).
		await storeA.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);
		await storeB.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantB }, CancellationToken.None)
			.ConfigureAwait(false);

		// Liveness — each tenant loads ONLY its own saga (Load's WHERE includes AND TenantId = :TenantId).
		var loadedA = await storeA.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		var loadedB = await storeB.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);

		_ = loadedA.ShouldNotBeNull("tenant A must load its own saga (liveness)");
		_ = loadedB.ShouldNotBeNull("tenant B must load its own saga (liveness)");
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

		// Safety — tenant B loading the SAME id must get NOTHING; the tenant predicate scopes A's row away.
		// RED on the drop-TenantId-from-Load mutant (tenant B would receive tenant A's saga).
		var crossTenantLoad = await storeB.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		crossTenantLoad.ShouldBeNull("tenant B must not be able to load tenant A's saga by id");

		// Liveness — tenant A still loads its own saga (the predicate scopes to, never away from, the owner).
		var ownLoad = await storeA.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		_ = ownLoad.ShouldNotBeNull("tenant A must still load its own saga after the cross-tenant miss");
	}

	private OracleSagaStore CreateStore(string tenantId)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available — real-infra saga tenant-isolation lock is never skipped.");

		var options = Options.Create(new OracleSagaStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = SchemaName,
			TableName = TableName,
		});

		return new OracleSagaStore(
			_fixture.ConnectionString,
			options,
			NullLogger<OracleSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(tenantId));
	}

	private async Task EnsureTableAsync()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Isolated table: PK is the full composite (SagaId, TenantId) with TenantId NOT NULL, so two tenants'
		// identical SagaIds are distinct rows and the tenant dimension of the Save-MERGE / Load predicate is what
		// is under test (not a bare SagaId PK collision). Oracle-native types mirror the shipped saga table:
		// RAW(16) Guid key, CLOB state, NUMBER(1)/NUMBER(19) flags.
		await DropIfExistsAsync(connection).ConfigureAwait(false);
		var createSql = $"""
			CREATE TABLE {SchemaName}.{TableName} (
				SagaId RAW(16) NOT NULL,
				SagaType VARCHAR2(500) NOT NULL,
				StateJson CLOB NOT NULL,
				IsCompleted NUMBER(1) DEFAULT 0 NOT NULL,
				TenantId VARCHAR2(200) NOT NULL,
				Version NUMBER(19) DEFAULT 0 NOT NULL,
				CreatedUtc TIMESTAMP DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP) NOT NULL,
				UpdatedUtc TIMESTAMP DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP) NOT NULL,
				CompletedAt TIMESTAMP WITH TIME ZONE NULL,
				CONSTRAINT PK_{TableName} PRIMARY KEY (SagaId, TenantId)
			)
			""";

		await using var command = connection.CreateCommand();
		command.CommandText = createSql;
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task DropIfExistsAsync(OracleConnection connection)
	{
		try
		{
			await using var drop = connection.CreateCommand();
			drop.CommandText = $"DROP TABLE {SchemaName}.{TableName}";
			_ = await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
		catch (OracleException ex) when (ex.Number == 942)
		{
			// ORA-00942: table or view does not exist — nothing to drop.
		}
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
