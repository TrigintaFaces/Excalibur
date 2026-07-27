// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

using Tests.Shared.Fixtures;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Shared fixture for Oracle SagaStore TestContainers (gvenzl/oracle-free).
/// </summary>
/// <remarks>
/// <para>
/// The <c>OracleSagaStore</c> does NOT auto-create its table; its Save/Load Dapper requests issue
/// <c>MERGE</c>/<c>SELECT</c> directly against the qualified table name. This fixture provisions
/// <c>DISPATCH.SAGAS</c> (plus the idempotency-key table) whose columns mirror exactly what the store's
/// requests reference — with Oracle-native types: <c>RAW(16)</c> for the Guid key, <c>CLOB</c> for the
/// JSON state, <c>NUMBER(1)</c> for the completion flag, <c>NUMBER(19)</c> for the version, and
/// <c>TIMESTAMP WITH TIME ZONE</c> for the <c>DateTimeOffset</c> columns.
/// </para>
/// <para>
/// The container app user is created as <c>DISPATCH</c>, so the store's default
/// <c>OracleSagaStoreOptions</c> (<c>SchemaName = "DISPATCH"</c>, <c>TableName = "SAGAS"</c>) resolves
/// to that user's own schema.
/// </para>
/// </remarks>
public sealed class OracleSagaStoreContainerFixture : ContainerFixtureBase
{
	private OracleContainer? _container;
	private bool _initialized;

	/// <summary>Gets the schema name for sagas (the store's default, = the container app user).</summary>
	public string SchemaName { get; } = "DISPATCH";

	/// <summary>Gets the table name for sagas (the store's default).</summary>
	public string TableName { get; } = "SAGAS";

	/// <summary>Gets the connection string for the Oracle container.</summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new OracleBuilder()
			// Pinned to the 23 tag. The floating "slim-faststart" tag resolves to an image whose listener never
			// registers the service Testcontainers connects to, so every test in this fixture failed with
			// ORA-12514 before it reached a database. Pinning the major is the fix; renaming the database is not
			// (that image rejects the rename and the container exits 244).
			.WithImage("gvenzl/oracle-free:23-slim-faststart")
			.WithName($"oracle-sagastore-test-{Guid.NewGuid():N}")
			.WithUsername("DISPATCH")
			.WithPassword("Test_Pass123")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Ensures the saga store table and idempotency-key table are initialized.</summary>
	public async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Mirrors the columns the OracleSagaStore Save/Load requests reference. Oracle-native types; the
		// CompletedAt column is a real indexed TIMESTAMP WITH TIME ZONE backing PurgeCompletedBeforeAsync.
		var createTableSql = $"""
			CREATE TABLE {SchemaName}.{TableName} (
				SagaId RAW(16) NOT NULL,
				SagaType VARCHAR2(500) NOT NULL,
				StateJson CLOB NOT NULL,
				IsCompleted NUMBER(1) DEFAULT 0 NOT NULL,
				-- NOT NULL with the reserved untenanted sentinel, matching the shape the store now writes and
				-- reads. This is load-bearing on Oracle specifically: Oracle stores the empty string AS NULL,
				-- so a NULL-encoded untenanted partition is unaddressable — neither `= :TenantId` with a null
				-- bind nor `= ''` can ever match a row, and the store's predicates are now unconditional.
				TenantId VARCHAR2(200) DEFAULT '__untenanted__' NOT NULL,
				Version NUMBER(19) DEFAULT 0 NOT NULL,
				CreatedUtc TIMESTAMP DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP) NOT NULL,
				UpdatedUtc TIMESTAMP DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP) NOT NULL,
				CompletedAt TIMESTAMP WITH TIME ZONE NULL,
				-- Composite key, matching this package's own tenant-isolation test table
				-- (OracleSagaStoreTenantIsolationShould) which already used (SagaId, TenantId). With SagaId
				-- alone, two tenants cannot hold the same SagaId, so the cross-tenant case is inexpressible.
				CONSTRAINT PK_{TableName} PRIMARY KEY (TenantId, SagaId)
			)
			""";

		await ExecuteAsync(connection, createTableSql).ConfigureAwait(false);
		await ExecuteAsync(connection,
			$"CREATE INDEX IX_{TableName}_CompletedAt ON {SchemaName}.{TableName} (CompletedAt)").ConfigureAwait(false);

		_initialized = true;
	}

	/// <summary>Creates a new OracleConnection to the container.</summary>
	/// <returns>A new connection instance.</returns>
	public OracleConnection CreateConnection() => new(ConnectionString);

	/// <summary>Cleans up all rows from the sagas table between tests.</summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await ExecuteAsync(connection, $"DELETE FROM {SchemaName}.{TableName}").ConfigureAwait(false);
	}

	private static async Task ExecuteAsync(OracleConnection connection, string sql)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}
}
