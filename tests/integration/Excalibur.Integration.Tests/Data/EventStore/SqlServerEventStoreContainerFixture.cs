// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;

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
	private MsSqlContainer? _container;
	private bool _initialized;

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
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.WithName($"mssql-eventstore-test-{Guid.NewGuid():N}")
			.WithPassword("Test@Pass123")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Ensures the event store schema is initialized.
	/// </summary>
	public async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Mirrors the columns the SqlServerEventStore Insert/Load/Version/Erase requests reference.
		// EventData is nullable so the GDPR erasure path (sets EventData = NULL) is exercisable.
		var createTableSql = $"""
			IF NOT EXISTS (SELECT * FROM sys.tables t
				JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE s.name = '{SchemaName}' AND t.name = '{TableName}')
			BEGIN
				CREATE TABLE [{SchemaName}].[{TableName}] (
					Position BIGINT IDENTITY(1,1) NOT NULL,
					EventId NVARCHAR(255) NOT NULL,
					AggregateId NVARCHAR(255) NOT NULL,
					AggregateType NVARCHAR(500) NOT NULL,
					EventType NVARCHAR(500) NOT NULL,
					EventData VARBINARY(MAX) NULL,
					Metadata VARBINARY(MAX) NULL,
					Version BIGINT NOT NULL,
					Timestamp DATETIMEOFFSET NOT NULL,
					CONSTRAINT PK_{TableName} PRIMARY KEY (Position),
					CONSTRAINT UQ_{TableName}_Stream UNIQUE (AggregateId, AggregateType, Version)
				);
			END
			""";

		await using var command = new SqlCommand(createTableSql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

		_initialized = true;
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
