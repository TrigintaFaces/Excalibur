// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Shared fixture for SQL Server EventStore TestContainers.
/// </summary>
/// <remarks>
/// Creates and manages a SQL Server container with the event store schema. The store does NOT
/// auto-create its table, so this fixture creates the [dbo].[EventStoreEvents] table whose columns
/// mirror exactly what the SqlServerEventStore Dapper requests expect
/// (Position, EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp).
/// Position is an IDENTITY column matching the store's OUTPUT INSERTED.Position append, and a unique
/// constraint on (AggregateId, AggregateType, Version) backs optimistic concurrency.
/// </remarks>
public sealed class SqlServerEventStoreContainerFixture : ContainerFixtureBase
{
	private readonly OneTimeInitializer _initializer = new();
	private MsSqlContainer? _container;

	/// <summary>
	/// Gets the schema name for events (the store's default).
	/// </summary>
	public string SchemaName { get; } = "dbo";

	/// <summary>
	/// Gets the table name for events (the store's default).
	/// </summary>
	public string TableName { get; } = "EventStoreEvents";

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
			.WithBoundedMemory()
			.WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
			.WithName($"mssql-eventstore-test-{Guid.NewGuid():N}")
			.WithPassword("Test@Pass123")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Ensures the event store schema is initialized.
	/// </summary>
	public Task EnsureInitializedAsync() => _initializer.RunAsync(InitializeSchemaAsync);

	private async Task InitializeSchemaAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// The schema is the one the package SHIPS, applied in the order a consumer applies it. A
		// fixture that restated it had TenantId nullable and outside the stream key -- the opposite of
		// 001_CreateEventStoreSchema.sql, which pins it to a binary collation and includes it in
		// UNIQUE (AggregateId, AggregateType, Version, TenantId). A fixture that holds no schema cannot
		// drift from one.
		foreach (var script in ShippedSchemaScript.ReadSqlCmdBatches(
			"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/001_CreateEventStoreSchema.sql"))
		{
			await using var command = new SqlCommand(script, connection);
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		// 003's pre-flight refuses outright unless BOTH EventStoreEvents and EventStoreSnapshots already
		// exist under their default names -- this package's migrations assume a consumer applied 001 AND
		// 002 together, because a real deployment always has both tables from the same package. 002 has
		// to run here even though this fixture never reads the snapshot table itself.
		foreach (var scriptPath in new[]
		{
			"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/002_CreateSnapshotSchema.sql",
			"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/003_MigrateToMultiTenant.sql",
			"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/004_MakeEventTenantTotal.sql",
			"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/006_ConvergeUntenantedToDefaultTenant.sql",
			// 007 is a no-op against this fixture -- 001 already ships EventData nullable, so the script
			// takes its "already nullable" branch. It is applied anyway so that every run parses and
			// executes the shipped migration on a non-sqlcmd runner, which is the failure mode that
			// silently kills a migration on its first line, and so that its re-run guard is exercised
			// rather than asserted. The migration's actual ALTER is covered by
			// SqlServerEventDataNullableMigrationShould, which builds the pre-migration NOT NULL shape.
			"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/007_MakeEventDataNullableForErasure.sql",
		})
		{
			foreach (var script in ShippedSchemaScript.ReadSqlCmdBatches(scriptPath))
			{
				await using var command = new SqlCommand(script, connection);
				_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Creates a new SqlConnection to the container.
	/// </summary>
	/// <returns>A new connection instance.</returns>
	public SqlConnection CreateConnection() => new(ConnectionString);

	/// <summary>
	/// Cleans up all rows from the events table between tests.
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
