// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

using Testcontainers.PostgreSql;

#pragma warning disable CA2100 // SQL strings are safe - table name is a constant in test fixture

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Shared fixture for Postgres EventStore TestContainers.
/// </summary>
/// <remarks>
/// Creates and manages a Postgres container with the event store schema.
/// Uses postgres:16-alpine for fast container startup.
/// Enables Npgsql legacy timestamp behavior to map TIMESTAMPTZ to DateTimeOffset.
/// </remarks>
public sealed class PostgresEventStoreContainerFixture : IAsyncLifetime, IDisposable
{
	private readonly PostgreSqlContainer _container;
	private bool _initialized;
	private bool _disposed;

	/// <summary>
	/// Static constructor to enable Npgsql legacy timestamp behavior.
	/// This ensures TIMESTAMPTZ columns are mapped to DateTimeOffset instead of DateTime.
	/// Must be set before any Npgsql connection is opened.
	/// </summary>
	static PostgresEventStoreContainerFixture()
	{
		// Enable legacy timestamp behavior so TIMESTAMPTZ maps to DateTimeOffset
		// This is required for Dapper to materialize StoredEvent records correctly
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
	}

	/// <summary>
	/// Gets the connection string for the Postgres container.
	/// </summary>
	public string ConnectionString => _container.GetConnectionString();

	/// <summary>
	/// Gets the table name for events.
	/// </summary>
	public string TableName { get; } = "event_store_events";

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresEventStoreContainerFixture"/> class.
	/// </summary>
	public PostgresEventStoreContainerFixture()
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithName($"postgres-eventstore-test-{Guid.NewGuid():N}")
			.WithDatabase("eventstore_test")
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

		// Create event store table with required schema
		var createTableSql = $"""
			CREATE TABLE IF NOT EXISTS public.{TableName} (
				global_sequence BIGSERIAL PRIMARY KEY,
				event_id VARCHAR(255) NOT NULL UNIQUE,
				aggregate_id VARCHAR(255) NOT NULL,
				aggregate_type VARCHAR(255) NOT NULL,
				event_type VARCHAR(255) NOT NULL,
				event_data BYTEA NOT NULL,
				metadata BYTEA,
				version BIGINT NOT NULL,
				timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
				is_dispatched BOOLEAN NOT NULL DEFAULT FALSE,
				UNIQUE (aggregate_id, aggregate_type, version)
			);

			CREATE INDEX IF NOT EXISTS idx_events_aggregate
				ON public.{TableName}(aggregate_id, aggregate_type, version);

			CREATE INDEX IF NOT EXISTS idx_events_undispatched
				ON public.{TableName}(is_dispatched, global_sequence) WHERE is_dispatched = false;

			CREATE INDEX IF NOT EXISTS idx_events_type
				ON public.{TableName}(event_type);
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
	/// Cleans up all items from the events table.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var truncateSql = $"TRUNCATE TABLE public.{TableName} RESTART IDENTITY";
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
