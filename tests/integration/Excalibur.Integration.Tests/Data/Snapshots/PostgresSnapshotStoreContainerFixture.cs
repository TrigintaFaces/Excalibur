// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Npgsql;

using Testcontainers.PostgreSql;
using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

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
	private readonly OneTimeInitializer _initializer = new();

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
	/// Gets the table name for snapshots (the store's default -- matches the shipped schema).
	/// </summary>
	public string TableName { get; } = "event_store_snapshots";

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

		// The schema is the one the package SHIPS, applied in the order a consumer applies it. A
		// hand-written copy named the table "snapshots" -- the shipped script creates
		// "event_store_snapshots" -- and defaulted tenant_id to '', the sentinel the store retired in
		// favour of the reserved '__untenanted__' value. A fixture that holds no schema cannot drift.
		var scripts = ShippedSchemaScript.ReadAll(
			"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/001_CreateSnapshotSchema.sql",
			"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/002_MigrateSnapshotsToKeyedSentinel.sql");

		foreach (var script in scripts)
		{
			await using var command = new NpgsqlCommand(script, connection);
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
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
