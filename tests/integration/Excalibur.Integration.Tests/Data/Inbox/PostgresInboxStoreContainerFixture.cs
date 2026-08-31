// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Npgsql;

using Testcontainers.PostgreSql;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Shared fixture for Postgres InboxStore TestContainers.
/// </summary>
/// <remarks>
/// Creates and manages a Postgres container with the inbox store schema. The
/// <c>PostgresInboxStore</c> does NOT auto-create its table, so this fixture creates the
/// <c>public.inbox_messages</c> table whose snake_case columns mirror exactly what the store's
/// Dapper requests reference (message_id, handler_type, message_type, payload, metadata,
/// received_at, processed_at, status, last_error, retry_count, last_attempt_at, correlation_id,
/// tenant_id, source). Entries are keyed by the composite primary key (message_id, handler_type).
/// Enables Npgsql legacy timestamp behavior so TIMESTAMPTZ maps to DateTimeOffset, and Dapper
/// underscore name matching so snake_case columns bind to PascalCase properties.
/// </remarks>
public sealed class PostgresInboxStoreContainerFixture : ContainerFixtureBase
{
	private PostgreSqlContainer? _container;
	private readonly OneTimeInitializer _initializer = new();

	/// <summary>
	/// Static constructor to configure Npgsql and Dapper before any connection opens.
	/// </summary>
	static PostgresInboxStoreContainerFixture()
	{
		// snake_case columns (e.g. message_id) bind to PascalCase properties (e.g. MessageId).
		DefaultTypeMap.MatchNamesWithUnderscores = true;
	}

	/// <summary>
	/// Gets the connection string for the Postgres container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets the schema name for the inbox table (the store's default).
	/// </summary>
	public string SchemaName { get; } = "public";

	/// <summary>
	/// Gets the table name for the inbox (the store's default).
	/// </summary>
	public string TableName { get; } = "inbox_messages";

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(4);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithName($"postgres-inboxstore-test-{Guid.NewGuid():N}")
			.WithDatabase("inboxstore_test")
			.WithUsername("postgres")
			.WithPassword("postgres_password")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Ensures the inbox store schema is initialized.
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

		// The MULTI-TENANT schema the package SHIPS -- these tests exercise tenant isolation, and the
		// store fails fast at startup if the physical schema does not match the registered mode. A
		// hand-written copy omitted lease_expires_at entirely; a fixture that holds no schema cannot
		// drift from one.
		var script = ShippedSchemaScript.Read(
			"src/Excalibur/Excalibur.Inbox.Postgres/Scripts/001_CreateInboxSchema.MultiTenant.sql");

		await using var command = new NpgsqlCommand(script, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Creates a new NpgsqlConnection to the container.
	/// </summary>
	/// <returns>A new connection instance.</returns>
	public NpgsqlConnection CreateConnection() => new(ConnectionString);

	/// <summary>
	/// Cleans up all rows from the inbox table between tests.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var truncateSql = $"TRUNCATE TABLE \"{SchemaName}\".\"{TableName}\"";
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
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}
}
