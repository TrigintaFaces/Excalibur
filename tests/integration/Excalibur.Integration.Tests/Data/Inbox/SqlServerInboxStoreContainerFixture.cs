// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Shared fixture for SQL Server InboxStore TestContainers.
/// </summary>
/// <remarks>
/// Creates and manages a SQL Server container with the inbox store schema. The
/// <c>SqlServerInboxStore</c> does NOT auto-create its table, so this fixture creates the
/// <c>[dbo].[inbox_messages]</c> table whose columns mirror exactly what the store's Dapper
/// requests reference (MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt,
/// ProcessedAt, Status, LastError, RetryCount, LastAttemptAt, NextAttemptAt, CorrelationId,
/// TenantId, Source). The composite primary key (MessageId, HandlerType, TenantId) — TenantId NOT NULL —
/// backs the tenant-isolated "first writer wins" MERGE; NextAttemptAt backs the backoff-schedulable re-admission claim.
/// </remarks>
public sealed class SqlServerInboxStoreContainerFixture : ContainerFixtureBase
{
	private MsSqlContainer? _container;
	private readonly OneTimeInitializer _initializer = new();

	/// <summary>
	/// Gets the schema name for the inbox table (the store's default).
	/// </summary>
	public string SchemaName { get; } = "dbo";

	/// <summary>
	/// Gets the table name for the inbox (the store's default).
	/// </summary>
	public string TableName { get; } = "inbox_messages";

	/// <summary>
	/// Gets the connection string for the SQL Server container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MsSqlBuilder()
			.WithBoundedMemory()
			.WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
			.WithName($"mssql-inboxstore-test-{Guid.NewGuid():N}")
			.WithPassword("Test@Pass123")
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
		// hand-written copy omitted LeaseExpiresAtUtc entirely; a fixture that holds no schema cannot
		// drift from one.
		foreach (var script in ShippedSchemaScript.ReadSqlCmdBatches(
			"src/Excalibur/Excalibur.Inbox.SqlServer/Scripts/001_CreateInboxSchema.MultiTenant.sql"))
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
	/// Cleans up all rows from the inbox table between tests.
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
