// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

using Testcontainers.PostgreSql;
using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

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
public sealed class PostgresEventStoreContainerFixture : ContainerFixtureBase
{
	private readonly OneTimeInitializer _initializer = new();
	private PostgreSqlContainer? _container;

	/// <summary>
	/// Gets the connection string for the Postgres container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets the table name for events.
	/// </summary>
	public string TableName { get; } = "events";

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(4);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithName($"postgres-eventstore-test-{Guid.NewGuid():N}")
			.WithDatabase("eventstore_test")
			.WithUsername("postgres")
			.WithPassword("postgres_password")
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
		// fixture that restated it could drift permissively -- a NOT NULL column made nullable, a
		// narrower key -- and every arm running against it would then be structurally unable to detect
		// the violation it exists to catch, while still reporting green. A fixture that holds no schema
		// cannot drift from one.
		var scripts = ShippedSchemaScript.ReadAll(
			"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/004_CreateEventStoreSchema.sql",
			"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/005_MakeEventStreamIdentityTenantScoped.sql",
			"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/006_ConvergeUntenantedToDefaultTenant.sql");

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
