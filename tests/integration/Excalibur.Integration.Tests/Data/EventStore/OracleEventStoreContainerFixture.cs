// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

using Tests.Shared.Fixtures;

#pragma warning disable CA2100 // SQL strings are safe - identifiers are constants in test fixture

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Shared fixture for Oracle EventStore TestContainers. Creates and manages a
/// <c>gvenzl/oracle-free</c> container with the event store schema.
/// </summary>
public sealed class OracleEventStoreContainerFixture : ContainerFixtureBase
{
	private OracleContainer? _container;
	private bool _initialized;

	/// <summary>
	/// Gets the connection string for the Oracle container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets the schema (owning user) that the store objects live in. Resolved from the container's
	/// connecting user at initialization time.
	/// </summary>
	public string Schema { get; private set; } = "SYSTEM";

	/// <summary>
	/// Gets the table name for events.
	/// </summary>
	public string TableName { get; } = "EVENTSTOREEVENTS";

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new OracleBuilder()
			.WithImage("gvenzl/oracle-free:23-slim-faststart")
			.WithName($"oracle-eventstore-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
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

		using (var userCommand = connection.CreateCommand())
		{
			userCommand.CommandText = "SELECT USER FROM DUAL";
			Schema = (await userCommand.ExecuteScalarAsync().ConfigureAwait(false))?.ToString() ?? "SYSTEM";
		}

		// Create the event store table in the connecting user's own schema. POSITION is an identity
		// column (the monotonic global sequence); (AggregateId, AggregateType, Version) is unique so an
		// optimistic-concurrency double-append violates it. Column names are unquoted (upper-cased by
		// Oracle) and bind 1:1 to the store's quoted upper-case identifiers.
		var createTableSql = $"""
			CREATE TABLE {TableName} (
				POSITION        NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
				EVENTID         VARCHAR2(255) NOT NULL,
				AGGREGATEID     VARCHAR2(255) NOT NULL,
				AGGREGATETYPE   VARCHAR2(255) NOT NULL,
				EVENTTYPE       VARCHAR2(255) NOT NULL,
				EVENTDATA       BLOB,
				METADATA        BLOB,
				VERSION         NUMBER(19) NOT NULL,
				EVENTTIMESTAMP  TIMESTAMP(7) WITH TIME ZONE NOT NULL,
				TENANTID        VARCHAR2(255),
				CONSTRAINT UQ_EVENTSTORE_STREAM_VERSION UNIQUE (AGGREGATEID, AGGREGATETYPE, VERSION)
			)
			""";

		await using (var command = connection.CreateCommand())
		{
			command.CommandText = createTableSql;
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		// NOTE: no separate CREATE INDEX on (AGGREGATEID, AGGREGATETYPE, VERSION) — the
		// UQ_EVENTSTORE_STREAM_VERSION unique constraint above already creates a usable index on exactly that
		// column list; adding another triggers ORA-01408 ("such column list already indexed") on a fresh DB.
		_initialized = true;
	}

	/// <summary>
	/// Creates a new <see cref="OracleConnection"/> to the container.
	/// </summary>
	public OracleConnection CreateConnection() => new(ConnectionString);

	/// <summary>
	/// Cleans up all rows from the events table between tests.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		if (!_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = $"DELETE FROM {TableName}";
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}
}
