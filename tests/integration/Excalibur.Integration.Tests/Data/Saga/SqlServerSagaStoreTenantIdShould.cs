// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Real-infrastructure lock (bead 952rbe) for saga tenant binding on the SQL Server store: a
/// <c>SagaState.TenantId</c> must round-trip through both the serialized state blob AND the dedicated
/// queryable <c>TenantId</c> row-column (defense-in-depth multi-tenant scoping).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two-axis assertion:</b> (1) <c>SaveAsync</c>→<c>LoadAsync</c> reloads <c>TenantId</c> via the
/// <c>StateJson</c> blob (the whole <see cref="Excalibur.Dispatch.Abstractions.Messaging.Delivery.SagaState"/>
/// is serialized), and (2) a raw <c>SELECT TenantId</c> on the saga row returns the same value (the
/// <c>SaveSagaRequest</c> MERGE writes the dedicated column).
/// </para>
/// <para>
/// <b>Non-vacuity:</b> RED on the pre-fix surface — without <c>SagaState.TenantId</c> the blob carries no
/// tenant, and without the schema/MERGE column the row <c>SELECT</c> throws <c>Invalid column name 'TenantId'</c>.
/// Asserted against a real SQL Server container (a mock can't reproduce the MERGE write / column read);
/// never skipped.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Collection("SqlServer SagaStore Integration Tests")]
public sealed class SqlServerSagaStoreTenantIdShould : IClassFixture<SqlServerSagaStoreContainerFixture>
{
	private const string TenantId = "tenant-952rbe";
	private readonly SqlServerSagaStoreContainerFixture _fixture;

	public SqlServerSagaStoreTenantIdShould(SqlServerSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Persist_and_reload_TenantId_via_both_the_blob_and_the_queryable_row_column()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — this real-infra saga TenantId lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		try
		{
			var store = new SqlServerSagaStore(
				_fixture.ConnectionString,
				NullLogger<SqlServerSagaStore>.Instance,
				new DispatchJsonSerializer());

			var sagaId = Guid.NewGuid();
			var state = new TestSagaState { SagaId = sagaId, TenantId = TenantId };

			await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

			// Axis 1 — TenantId rides the serialized state blob.
			var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
			_ = loaded.ShouldNotBeNull();
			loaded.TenantId.ShouldBe(TenantId, "TenantId must round-trip through the serialized SagaState blob");

			// Axis 2 — TenantId is also written to the dedicated queryable row-column (defense-in-depth).
			await using var connection = new SqlConnection(_fixture.ConnectionString);
			await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

			var rowTenantId = await connection.QuerySingleAsync<string?>(
				$"SELECT TenantId FROM [{_fixture.SchemaName}].[{_fixture.TableName}] WHERE SagaId = @id",
				new { id = sagaId }).ConfigureAwait(false);

			rowTenantId.ShouldBe(TenantId, "the SaveSagaRequest MERGE must populate the queryable TenantId column");
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}
}
