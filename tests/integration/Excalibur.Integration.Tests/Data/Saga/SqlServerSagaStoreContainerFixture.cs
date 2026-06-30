// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Shared fixture for SQL Server SagaStore TestContainers.
/// </summary>
/// <remarks>
/// <para>
/// Creates and manages a SQL Server container for the saga store. Despite the task brief, the
/// <c>SqlServerSagaStore</c> does NOT auto-create its table — its Save/Load Dapper requests issue
/// <c>MERGE</c>/<c>SELECT</c> directly against the qualified table name with no DDL bootstrap. This
/// fixture therefore creates the <c>[dispatch].[sagas]</c> table whose columns mirror exactly what the
/// store's requests reference (SagaId, SagaType, StateJson, IsCompleted, Version, CreatedUtc, UpdatedUtc),
/// matching <c>Scripts/01-SagaSchema.sql</c>.
/// </para>
/// <para>
/// The schema/table names match the store's default <c>SqlServerSagaStoreOptions</c>
/// (<c>SchemaName = "dispatch"</c>, <c>TableName = "sagas"</c>), so the simple
/// <c>new SqlServerSagaStore(connectionString, logger, serializer)</c> constructor resolves to
/// <c>[dispatch].[sagas]</c>.
/// </para>
/// </remarks>
public sealed class SqlServerSagaStoreContainerFixture : ContainerFixtureBase
{
	private MsSqlContainer? _container;
	private bool _initialized;

	/// <summary>
	/// Gets the schema name for sagas (the store's default).
	/// </summary>
	public string SchemaName { get; } = "dispatch";

	/// <summary>
	/// Gets the table name for sagas (the store's default).
	/// </summary>
	public string TableName { get; } = "sagas";

	/// <summary>
	/// Gets the connection string for the SQL Server container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.WithName($"mssql-sagastore-test-{Guid.NewGuid():N}")
			.WithPassword("Test@Pass123")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Ensures the saga store schema and table are initialized.
	/// </summary>
	public async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Mirrors Scripts/01-SagaSchema.sql — the columns the SqlServerSagaStore Save/Load requests reference.
		var createSchemaSql = $"""
			IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{SchemaName}')
			BEGIN
				EXEC('CREATE SCHEMA [{SchemaName}]');
			END
			""";

		await using (var schemaCommand = new SqlCommand(createSchemaSql, connection))
		{
			_ = await schemaCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		var createTableSql = $"""
			IF NOT EXISTS (SELECT * FROM sys.objects
				WHERE object_id = OBJECT_ID(N'[{SchemaName}].[{TableName}]') AND type = N'U')
			BEGIN
				CREATE TABLE [{SchemaName}].[{TableName}] (
					SagaId UNIQUEIDENTIFIER NOT NULL,
					SagaType NVARCHAR(500) NOT NULL,
					StateJson NVARCHAR(MAX) NOT NULL,
					IsCompleted BIT NOT NULL DEFAULT 0,
					Version BIGINT NOT NULL DEFAULT 0,
					CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
					UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
					RowVersion ROWVERSION NOT NULL,
					CONSTRAINT PK_{SchemaName}_{TableName} PRIMARY KEY CLUSTERED (SagaId)
				);
			END
			""";

		await using (var tableCommand = new SqlCommand(createTableSql, connection))
		{
			_ = await tableCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		_initialized = true;
	}

	/// <summary>
	/// Creates a new SqlConnection to the container.
	/// </summary>
	/// <returns>A new connection instance.</returns>
	public SqlConnection CreateConnection() => new(ConnectionString);

	/// <summary>
	/// Cleans up all rows from the sagas table between tests.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var truncateSql = $"TRUNCATE TABLE [{SchemaName}].[{TableName}]";
		await using var command = new SqlCommand(truncateSql, connection);
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
