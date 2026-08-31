// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Npgsql;

using Testcontainers.PostgreSql;

using Tests.Shared.Fixtures;

#pragma warning disable CA2100 // SQL strings are safe - table/index names are constants in test fixture

using Tests.Shared.Helpers;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Shared fixture for Postgres OutboxStore TestContainers.
/// </summary>
/// <remarks>
/// <para>
/// Creates and manages a Postgres container for the outbox store conformance suite. Unlike the
/// document-store providers, the Postgres outbox store does NOT self-create its schema: it issues
/// Dapper requests against pre-existing <c>outbox</c> and <c>outbox_dead_letters</c> tables, so this
/// fixture creates them. The DDL mirrors exactly the columns the store's reserve/insert/schedule
/// requests reference (message_id, message_type, message_metadata, message_body, tenant_id,
/// destination, correlation_id, causation_id, priority, partition_key, group_key, target_transports,
/// is_multi_transport, occurred_on, attempts, dispatcher_id, dispatcher_timeout, next_attempt_at,
/// scheduled_at).
/// </para>
/// <para>
/// The reserve request aliases every column to its PascalCase property name, so Dapper underscore
/// matching is not required; the Npgsql legacy timestamp switch is enabled so TIMESTAMPTZ columns
/// materialize as DateTimeOffset. Cleanup truncates the tables between tests to keep the shared
/// container isolated.
/// </para>
/// </remarks>
public sealed class PostgresOutboxStoreContainerFixture : ContainerFixtureBase
{
	private PostgreSqlContainer? _container;
	private readonly OneTimeInitializer _initializer = new();

	/// <summary>
	/// Static constructor to configure Npgsql timestamp behavior.
	/// </summary>
	/// <remarks>
	/// Enables Npgsql legacy timestamp behavior so TIMESTAMPTZ columns (e.g. occurred_on) map to
	/// DateTimeOffset rather than DateTime. Must be set before any connection is opened.
	/// </remarks>
	static PostgresOutboxStoreContainerFixture()
	{
		DefaultTypeMap.MatchNamesWithUnderscores = true;
	}

	/// <summary>
	/// Gets the schema name for the outbox tables (the store's default).
	/// </summary>
	public string SchemaName { get; } = "public";

	/// <summary>
	/// Gets the outbox table name (the store's default).
	/// </summary>
	public string OutboxTableName { get; } = "outbox";

	/// <summary>
	/// Gets the dead-letter table name (the store's default).
	/// </summary>
	public string DeadLetterTableName { get; } = "outbox_dead_letters";

	/// <summary>
	/// Gets the connection string for the Postgres container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(4);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithName($"postgres-outboxstore-test-{Guid.NewGuid():N}")
			.WithDatabase("outboxstore_test")
			.WithUsername("postgres")
			.WithPassword("postgres_password")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Ensures the outbox store schema is initialized.
	/// </summary>
	/// <remarks>
	/// The columns mirror exactly what the PostgresOutboxStore Dapper requests
	/// (InsertOutboxMessage, ReserveOutboxMessages, ScheduleOutboxMessage, GetOutboxStatistics, …)
	/// reference, including the tenant_id and next_attempt_at columns used by the tenant-persistence
	/// and exponential-backoff paths.
	/// </remarks>
	public Task EnsureInitializedAsync() => _initializer.RunAsync(InitializeSchemaAsync);

	/// <summary>
	/// Provisions the schema. Runs once, through <see cref="OneTimeInitializer"/>, so a failure
	/// here is rethrown to every later caller instead of being retried against a database this
	/// call already half-provisioned.
	/// </summary>
	private async Task InitializeSchemaAsync()
	{
		// The schema is the one the package SHIPS, applied in the order a consumer applies it. A
		// fixture that restated it could drift permissively -- a nullable column where the script says NOT
		// NULL, a narrower key -- and every arm running against it would then be structurally unable to
		// detect the violation it exists to catch, while still reporting green. A fixture that holds no
		// schema cannot drift from one.
		var scripts = ShippedSchemaScript.ReadAll(
			"src/Excalibur/Excalibur.Outbox.Postgres/Scripts/001_CreateOutboxSchema.sql",
			"src/Excalibur/Excalibur.Outbox.Postgres/Scripts/002_MakeOutboxTenantTotal.sql",
			"src/Excalibur/Excalibur.Outbox.Postgres/Scripts/003_CarryTenantOnDeadLetters.sql");

		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

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
	public NpgsqlConnection CreateConnection() => new(ConnectionString);

	/// <summary>
	/// Cleans up all rows from the outbox and dead-letter tables between tests.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new NpgsqlCommand(
			"TRUNCATE TABLE outbox CASCADE; TRUNCATE TABLE outbox_dead_letters CASCADE; TRUNCATE TABLE outbox_fence CASCADE;",
			connection);
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
