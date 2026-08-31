// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings are safe - table/schema names are constants in test fixture

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Shared fixture for SQL Server SnapshotStore TestContainers.
/// </summary>
/// <remarks>
/// Creates and manages a SQL Server container with the snapshot store schema.
/// The table layout mirrors the columns the SqlServerSnapshotStore Dapper requests expect
/// (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, Metadata) with the
/// latest-snapshot-per-aggregate primary key on AggregateId (matching the store's MERGE semantics).
/// </remarks>
public sealed class SqlServerSnapshotStoreContainerFixture : ContainerFixtureBase
{
	private MsSqlContainer? _container;
	private readonly OneTimeInitializer _initializer = new();

	/// <summary>
	/// Gets the schema name for snapshots.
	/// </summary>
	public string SchemaName { get; } = "dbo";

	/// <summary>
	/// Gets the table name for snapshots.
	/// </summary>
	public string TableName { get; } = "EventStoreSnapshots";

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
			.WithName($"mssql-snapshotstore-test-{Guid.NewGuid():N}")
			.WithPassword("Test@Pass123")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Ensures the snapshot store schema is initialized.
	/// </summary>
	public Task EnsureInitializedAsync() => _initializer.RunAsync(InitializeSchemaAsync);

	/// <summary>
	/// Provisions the schema. Runs once, through <see cref="OneTimeInitializer"/>, so a failure
	/// here is rethrown to every later caller instead of being retried against a database this
	/// call already half-provisioned.
	/// </summary>
	private async Task InitializeSchemaAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// The schema is the one the package SHIPS (002_CreateSnapshotSchema.sql), executed rather than
		// restated. A restated copy previously drifted on four columns at once (a PRIMARY KEY missing
		// AggregateType, an un-collated TenantId, DATETIME2 instead of DATETIMEOFFSET, a narrower
		// AggregateType) -- each one silently able to hide the exact defect the arm running against it
		// was written to catch. A fixture that holds no schema cannot drift from one.
		foreach (var script in ShippedSchemaScript.ReadSqlCmdBatches(
			"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/002_CreateSnapshotSchema.sql"))
		{
			await using var command = new SqlCommand(script, connection);
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates a new SqlConnection to the container.
	/// </summary>
	/// <returns>A new connection instance.</returns>
	public SqlConnection CreateConnection() => new(ConnectionString);

	/// <summary>
	/// Cleans up all rows from the snapshots table between tests.
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
