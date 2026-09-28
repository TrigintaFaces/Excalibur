// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Tests.Shared.Infrastructure;
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
			.WithImage(TestContainerImages.SqlServer2022)
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

		// The snapshot table ships from the same package and a real deployment always has both, so it is
		// created here even though this fixture never reads it.
		foreach (var scriptPath in new[]
		{
			"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/002_CreateSnapshotSchema.sql",
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
	/// Cleans up all rows from the events table between tests, and resets the position counter with them.
	/// </summary>
	/// <remarks>
	/// Resetting the counter is what keeps positions comparable across tests. Positions are allocated
	/// from a counter row rather than an identity column, so truncating the events table alone leaves the
	/// counter where it was and the next test's first append continues from the previous test's highest
	/// position. An arm asserting an absolute position then passes alone and fails in a batch -- which is
	/// exactly how this was found.
	/// </remarks>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var truncateSql =
			$"TRUNCATE TABLE [{SchemaName}].[{TableName}]; "
			+ $"UPDATE [{SchemaName}].[{TableName}Position] SET [Value] = 0 WHERE [Id] = 1;";
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
