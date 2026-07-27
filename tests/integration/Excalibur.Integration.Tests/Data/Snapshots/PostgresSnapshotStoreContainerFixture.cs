// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Npgsql;

using Testcontainers.PostgreSql;
using Tests.Shared.Fixtures;

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
public sealed class PostgresSnapshotStoreContainerFixture : ContainerFixtureBase
{
	private PostgreSqlContainer? _container;
	private bool _initialized;

	/// <summary>
	/// Static constructor to configure Dapper.
	/// </summary>
	/// <remarks>
	/// Enables Dapper underscore name matching so snake_case column names map to PascalCase properties.
	/// Must be set before any connections are opened.
	/// </remarks>
	static PostgresSnapshotStoreContainerFixture()
	{
		// Enable Dapper's underscore to PascalCase name matching
		// Required because Postgres uses snake_case column names (e.g., aggregate_id)
		// but DTOs use PascalCase properties (e.g., AggregateId)
		DefaultTypeMap.MatchNamesWithUnderscores = true;
	}

	/// <summary>
	/// Gets the connection string for the Postgres container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets the schema name for snapshots.
	/// </summary>
	public string SchemaName { get; } = "public";

	/// <summary>
	/// Gets the table name for snapshots.
	/// </summary>
	public string TableName { get; } = "snapshots";

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(4);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithName($"postgres-snapshotstore-test-{Guid.NewGuid():N}")
			.WithDatabase("snapshotstore_test")
			.WithUsername("postgres")
			.WithPassword("postgres_password")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
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

		// Matches the canonical Excalibur.EventSourcing.Postgres snapshot store's shipped schema
		// (Scripts/001_CreateSnapshotSchema.sql): snapshot_id is TEXT, metadata is a nullable BYTEA, and
		// there is no snapshot_type column. tenant_id participates in the primary key.
		var createTableSql = $"""
			CREATE TABLE IF NOT EXISTS {SchemaName}.{TableName} (
				snapshot_id TEXT NOT NULL,
				aggregate_id VARCHAR(255) NOT NULL,
				aggregate_type VARCHAR(255) NOT NULL,
				version BIGINT NOT NULL,
				data BYTEA NOT NULL,
				metadata BYTEA,
				created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
				tenant_id VARCHAR(255) NOT NULL DEFAULT '',
				PRIMARY KEY (aggregate_id, aggregate_type, tenant_id)
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
			// Suppress disposal errors and timeouts to prevent test host crash
		}
	}
}
