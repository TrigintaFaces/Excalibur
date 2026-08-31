// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Shared fixture for SQL Server OutboxStore TestContainers.
/// </summary>
/// <remarks>
/// <para>
/// Creates and manages a SQL Server container for the outbox store conformance suite. The SqlServer
/// outbox store does NOT self-create its schema: it issues Dapper requests against pre-existing
/// <c>[dbo].[OutboxMessages]</c> and <c>[dbo].[OutboxMessageTransports]</c> tables, so this fixture
/// creates them. The columns mirror exactly what the store's claim/insert/statistics SQL references
/// (Status, RetryCount, LeasedAt/LeasedBy lease columns, NextAttemptAt, PartitionKey/SequenceNumber
/// ordering keys, TenantId, …). Failed and dead-lettered messages are tracked in the OutboxMessages
/// table via the Status column, so no separate dead-letter table is required for the conformance suite.
/// </para>
/// <para>
/// Cleanup deletes the transport rows before the message rows to respect the foreign key, keeping the
/// shared container isolated between tests.
/// </para>
/// </remarks>
public sealed class SqlServerOutboxStoreContainerFixture : ContainerFixtureBase
{
	private MsSqlContainer? _container;
	private readonly OneTimeInitializer _initializer = new();

	/// <summary>
	/// Gets the schema name for the outbox tables (the store's default).
	/// </summary>
	public string SchemaName { get; } = "dbo";

	/// <summary>
	/// Gets the outbox message table name (the store's default).
	/// </summary>
	public string OutboxTableName { get; } = "OutboxMessages";

	/// <summary>
	/// Gets the transport delivery table name (the store's default).
	/// </summary>
	public string TransportsTableName { get; } = "OutboxMessageTransports";

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
			.WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
			.WithName($"mssql-outboxstore-test-{Guid.NewGuid():N}")
			.WithPassword("Test@Pass123")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Ensures the outbox store schema is initialized.
	/// </summary>
	public Task EnsureInitializedAsync() => _initializer.RunAsync(InitializeSchemaAsync);

	/// <summary>
	/// Provisions the schema. Runs once, through <see cref="OneTimeInitializer"/>, so a failure
	/// here is rethrown to every later caller instead of being retried against a database this
	/// call already half-provisioned.
	/// </summary>
	private async Task InitializeSchemaAsync()
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// The schema is the one the package SHIPS, applied in the order a consumer applies it. A
		// hand-written copy left OutboxMessages.TenantId nullable and dropped the DeadLetterQueue table
		// entirely; a fixture that holds no schema cannot drift from one.
		foreach (var script in ShippedSchemaScript.ReadSqlCmdBatches(
			"src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql"))
		{
			await using var command = new SqlCommand(script, connection);
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates a new SqlConnection to the container.
	/// </summary>
	/// <returns>A new connection instance.</returns>
	public SqlConnection CreateConnection() => new(ConnectionString);

	/// <summary>
	/// Cleans up all rows from the outbox and transport tables between tests.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// Delete transports first to satisfy the foreign key.
		await using (var deleteTransports = new SqlCommand("DELETE FROM [dbo].[OutboxMessageTransports]", connection))
		{
			_ = await deleteTransports.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		await using (var deleteMessages = new SqlCommand("DELETE FROM [dbo].[OutboxMessages]", connection))
		{
			_ = await deleteMessages.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		// Reset the durable fence high-water between tests. In production this table intentionally outlives
		// message cleanup; between conformance tests it MUST be cleared so one test's advanced high-water
		// does not leak into the next (which would fence off the next test's valid tokens).
		await using (var deleteFence = new SqlCommand("DELETE FROM [dbo].[OutboxFence]", connection))
		{
			_ = await deleteFence.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
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
