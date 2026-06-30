// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;

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
	private bool _initialized;

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
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.WithName($"mssql-outboxstore-test-{Guid.NewGuid():N}")
			.WithPassword("Test@Pass123")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Ensures the outbox store schema is initialized.
	/// </summary>
	public async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		const string createOutboxTableSql = """
			IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OutboxMessages' AND xtype='U')
			CREATE TABLE [dbo].[OutboxMessages] (
				Id NVARCHAR(255) NOT NULL PRIMARY KEY,
				MessageType NVARCHAR(500) NOT NULL,
				Payload VARBINARY(MAX) NOT NULL,
				Headers NVARCHAR(MAX) NULL,
				Destination NVARCHAR(255) NOT NULL,
				CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
				ScheduledAt DATETIMEOFFSET NULL,
				SentAt DATETIMEOFFSET NULL,
				Status INT NOT NULL DEFAULT 0,
				RetryCount INT NOT NULL DEFAULT 0,
				LastError NVARCHAR(MAX) NULL,
				LastAttemptAt DATETIMEOFFSET NULL,
				CorrelationId NVARCHAR(255) NULL,
				CausationId NVARCHAR(255) NULL,
				TenantId NVARCHAR(255) NULL,
				Priority INT NOT NULL DEFAULT 0,
				TargetTransports NVARCHAR(MAX) NULL,
				IsMultiTransport BIT NOT NULL DEFAULT 0,
				LeasedAt DATETIMEOFFSET NULL,
				LeasedBy NVARCHAR(255) NULL,
				PartitionKey NVARCHAR(256) NULL,
				GroupKey NVARCHAR(256) NULL,
				SequenceNumber BIGINT NOT NULL DEFAULT 0,
				NextAttemptAt DATETIMEOFFSET NULL,
				INDEX IX_OutboxMessages_Status_CreatedAt (Status, CreatedAt),
				INDEX IX_OutboxMessages_Claim (Status, NextAttemptAt, PartitionKey, SequenceNumber)
			)
			""";

		const string createTransportsTableSql = """
			IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OutboxMessageTransports' AND xtype='U')
			CREATE TABLE [dbo].[OutboxMessageTransports] (
				Id NVARCHAR(255) NOT NULL PRIMARY KEY,
				MessageId NVARCHAR(255) NOT NULL,
				TransportName NVARCHAR(255) NOT NULL,
				Destination NVARCHAR(255) NULL,
				Status INT NOT NULL DEFAULT 0,
				CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
				AttemptedAt DATETIMEOFFSET NULL,
				SentAt DATETIMEOFFSET NULL,
				RetryCount INT NOT NULL DEFAULT 0,
				LastError NVARCHAR(MAX) NULL,
				TransportMetadata NVARCHAR(MAX) NULL,
				FOREIGN KEY (MessageId) REFERENCES [dbo].[OutboxMessages](Id)
			)
			""";

		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		await using (var createOutbox = new SqlCommand(createOutboxTableSql, connection))
		{
			_ = await createOutbox.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		await using (var createTransports = new SqlCommand(createTransportsTableSql, connection))
		{
			_ = await createTransports.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		_initialized = true;
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
