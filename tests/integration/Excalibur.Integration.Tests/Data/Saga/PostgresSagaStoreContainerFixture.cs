// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

using Testcontainers.PostgreSql;

#pragma warning disable CA2100 // SQL strings are safe - table name is a constant in test fixture

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Shared fixture for Postgres Saga Store TestContainers.
/// </summary>
/// <remarks>
/// Creates and manages a Postgres container with the saga store schema.
/// Uses postgres:16-alpine for fast container startup.
/// Enables Npgsql legacy timestamp behavior to map TIMESTAMPTZ to DateTimeOffset.
/// </remarks>
public sealed class PostgresSagaStoreContainerFixture : IAsyncLifetime, IDisposable
{
	private readonly PostgreSqlContainer _container;
	private bool _initialized;
	private bool _disposed;

	/// <summary>
	/// Static constructor to enable Npgsql legacy timestamp behavior.
	/// This ensures TIMESTAMPTZ columns are mapped to DateTimeOffset instead of DateTime.
	/// Must be set before any Npgsql connection is opened.
	/// </summary>
	static PostgresSagaStoreContainerFixture()
	{
		// Enable legacy timestamp behavior so TIMESTAMPTZ maps to DateTimeOffset
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
	}

	/// <summary>
	/// Gets the connection string for the Postgres container.
	/// </summary>
	public string ConnectionString => _container.GetConnectionString();

	/// <summary>
	/// Gets the schema name for the saga store.
	/// </summary>
	public string Schema { get; } = "dispatch";

	/// <summary>
	/// Gets the table name for sagas.
	/// </summary>
	public string TableName { get; } = "sagas";

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresSagaStoreContainerFixture"/> class.
	/// </summary>
	public PostgresSagaStoreContainerFixture()
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithName($"postgres-sagastore-test-{Guid.NewGuid():N}")
			.WithDatabase("sagastore_test")
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
	/// Ensures the saga store schema is initialized.
	/// </summary>
	public async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Create schema and saga store table
		var createSchemaSql = $"""
			CREATE SCHEMA IF NOT EXISTS {Schema};

			CREATE TABLE IF NOT EXISTS "{Schema}"."{TableName}" (
				saga_id UUID PRIMARY KEY,
				saga_type VARCHAR(256) NOT NULL,
				state_json JSONB NOT NULL,
				is_completed BOOLEAN NOT NULL DEFAULT FALSE,
				created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
				updated_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
			);

			CREATE INDEX IF NOT EXISTS idx_sagas_saga_type
				ON "{Schema}"."{TableName}"(saga_type);

			CREATE INDEX IF NOT EXISTS idx_sagas_is_completed
				ON "{Schema}"."{TableName}"(is_completed) WHERE is_completed = FALSE;
			""";

		await using var command = new NpgsqlCommand(createSchemaSql, connection);
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
	/// Cleans up all items from the sagas table.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var truncateSql = $"TRUNCATE TABLE \"{Schema}\".\"{TableName}\" RESTART IDENTITY";
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

/// <summary>
/// xUnit collection definition for Postgres Saga Store integration tests.
/// </summary>
[CollectionDefinition("PostgresSagaStore")]
public class PostgresSagaStoreTestCollection : ICollectionFixture<PostgresSagaStoreContainerFixture>
{
}
