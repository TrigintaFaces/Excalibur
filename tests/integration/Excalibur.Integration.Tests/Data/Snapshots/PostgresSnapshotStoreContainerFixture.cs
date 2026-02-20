// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Npgsql;

using Testcontainers.PostgreSql;

#pragma warning disable CA2100 // SQL strings are safe - table name is a constant in test fixture

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Shared fixture for Postgres SnapshotStore TestContainers.
/// </summary>
/// <remarks>
/// Creates and manages a Postgres container with the snapshot store schema.
/// Uses postgres:16-alpine for fast container startup.
/// Enables Npgsql legacy timestamp behavior to map TIMESTAMPTZ to DateTimeOffset.
/// </remarks>
public sealed class PostgresSnapshotStoreContainerFixture : IAsyncLifetime, IDisposable
{
	private readonly PostgreSqlContainer _container;
	private bool _initialized;
	private bool _disposed;

	/// <summary>
	/// Static constructor to configure Dapper and Npgsql.
	/// </summary>
	/// <remarks>
	/// Enables:
	/// - Npgsql legacy timestamp behavior: TIMESTAMPTZ columns map to DateTimeOffset instead of DateTime
	/// - Dapper underscore name matching: snake_case column names map to PascalCase properties
	/// These must be set before any connections are opened.
	/// </remarks>
	static PostgresSnapshotStoreContainerFixture()
	{
		// Enable legacy timestamp behavior so TIMESTAMPTZ maps to DateTimeOffset
		// This is required for Dapper to materialize Snapshot records correctly
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

		// Enable Dapper's underscore to PascalCase name matching
		// Required because Postgres uses snake_case column names (e.g., aggregate_id)
		// but DTOs use PascalCase properties (e.g., AggregateId)
		DefaultTypeMap.MatchNamesWithUnderscores = true;
	}

	/// <summary>
	/// Gets the connection string for the Postgres container.
	/// </summary>
	public string ConnectionString => _container.GetConnectionString();

	/// <summary>
	/// Gets the schema name for snapshots.
	/// </summary>
	public string SchemaName { get; } = "public";

	/// <summary>
	/// Gets the table name for snapshots.
	/// </summary>
	public string TableName { get; } = "snapshots";

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresSnapshotStoreContainerFixture"/> class.
	/// </summary>
	public PostgresSnapshotStoreContainerFixture()
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithName($"postgres-snapshotstore-test-{Guid.NewGuid():N}")
			.WithDatabase("snapshotstore_test")
			.WithUsername("postgres")
			.WithPassword("postgres_password")
			.WithCleanUp(true)
			.Build();
	}

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		await _container.StartAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Ensures the snapshot store schema is initialized.
	/// </summary>
	public async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Create snapshot store table with required schema
		// Uses composite primary key (aggregate_id, aggregate_type) for single-snapshot-per-aggregate pattern
		var createTableSql = $"""
			CREATE TABLE IF NOT EXISTS {SchemaName}.{TableName} (
				snapshot_id UUID NOT NULL,
				aggregate_id VARCHAR(255) NOT NULL,
				aggregate_type VARCHAR(255) NOT NULL,
				version BIGINT NOT NULL,
				snapshot_type VARCHAR(500) NOT NULL,
				data BYTEA NOT NULL,
				metadata BYTEA,
				created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
				PRIMARY KEY (aggregate_id, aggregate_type)
			);

			CREATE INDEX IF NOT EXISTS idx_snapshots_version
				ON {SchemaName}.{TableName}(aggregate_id, aggregate_type, version);
			""";

		await using var command = new NpgsqlCommand(createTableSql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

		_initialized = true;
	}

	/// <summary>
	/// Creates a new NpgsqlConnection to the container.
	/// </summary>
	/// <returns>A new connection instance.</returns>
	public NpgsqlConnection CreateConnection()
	{
		return new NpgsqlConnection(ConnectionString);
	}

	/// <summary>
	/// Cleans up all items from the snapshots table.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var truncateSql = $"TRUNCATE TABLE {SchemaName}.{TableName}";
		await using var command = new NpgsqlCommand(truncateSql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		await _container.DisposeAsync().ConfigureAwait(false);
	}
}
