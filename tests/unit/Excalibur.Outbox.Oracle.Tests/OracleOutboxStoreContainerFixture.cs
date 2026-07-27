// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

using Tests.Shared.Fixtures;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Shared fixture for the Oracle OutboxStore real-infra conformance tests.
/// </summary>
/// <remarks>
/// Starts a <c>gvenzl/oracle-free</c> container and creates the <c>OUTBOX</c> and
/// <c>OUTBOX_DEAD_LETTERS</c> tables whose columns mirror exactly what <see cref="OracleOutboxStore"/>'s
/// Dapper requests reference. NOT-NULL is applied only to the identity column (<c>message_id</c>) and the
/// numeric columns; the string/BLOB columns are nullable so Oracle's <c>'' → NULL</c> fold cannot violate
/// a NOT-NULL constraint (A0 ruling #5). Registers a tolerant <see cref="DateTimeOffset"/> Dapper handler
/// so TIMESTAMP WITH TIME ZONE columns round-trip through the store's DateTimeOffset properties.
/// </remarks>
public sealed class OracleOutboxStoreContainerFixture : ContainerFixtureBase
{
	private OracleContainer? _container;
	private bool _initialized;

	static OracleOutboxStoreContainerFixture()
	{
		SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
	}

	/// <summary>Gets the connection string for the Oracle container.</summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>Gets the schema name (empty => connection default schema).</summary>
	public string SchemaName { get; } = string.Empty;

	/// <summary>Gets the outbox table name.</summary>
	public string OutboxTableName { get; } = "OUTBOX";

	/// <summary>Gets the dead-letter table name.</summary>
	public string DeadLetterTableName { get; } = "OUTBOX_DEAD_LETTERS";

	/// <summary>Gets the leadership-fencing high-water control table name.</summary>
	public string FenceTableName { get; } = "OUTBOX_FENCE";

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new OracleBuilder()
			.WithImage("gvenzl/oracle-free:23-slim-faststart")
			.WithName($"oracle-outboxstore-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Ensures the outbox store schema is initialized.</summary>
	public async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Oracle has no "CREATE TABLE IF NOT EXISTS"; swallow ORA-00955 (name already used) for idempotence.
		var outboxDdl = $"""
			CREATE TABLE {OutboxTableName} (
				message_id          VARCHAR2(100)                   NOT NULL,
				message_type        VARCHAR2(500),
				message_metadata    CLOB,
				message_body        BLOB,
				tenant_id           VARCHAR2(255),
				destination         VARCHAR2(500),
				correlation_id      VARCHAR2(255),
				causation_id        VARCHAR2(255),
				occurred_on         TIMESTAMP(7) WITH TIME ZONE     DEFAULT SYSTIMESTAMP NOT NULL,
				attempts            NUMBER(10)     DEFAULT 0        NOT NULL,
				error_message       CLOB,
				priority            NUMBER(10)     DEFAULT 0        NOT NULL,
				dispatcher_id       VARCHAR2(100),
				dispatcher_timeout  TIMESTAMP(7) WITH TIME ZONE,
				next_attempt_at     TIMESTAMP(7) WITH TIME ZONE,
				scheduled_at        TIMESTAMP(7) WITH TIME ZONE,
				partition_key       VARCHAR2(255),
				group_key           VARCHAR2(255),
				sequence_number     NUMBER(19)     DEFAULT 0        NOT NULL,
				target_transports   VARCHAR2(1000),
				is_multi_transport  NUMBER(1)      DEFAULT 0        NOT NULL,
				CONSTRAINT UQ_OUTBOX_MESSAGE_ID UNIQUE (message_id)
			)
			""";

		var deadLetterDdl = $"""
			CREATE TABLE {DeadLetterTableName} (
				message_id          VARCHAR2(100)                   NOT NULL,
				message_type        VARCHAR2(500),
				message_metadata    CLOB,
				message_body        BLOB,
				occurred_on         TIMESTAMP(7) WITH TIME ZONE     DEFAULT SYSTIMESTAMP NOT NULL,
				attempts            NUMBER(10)     DEFAULT 0        NOT NULL,
				error_message       CLOB,
				moved_on            TIMESTAMP(7) WITH TIME ZONE     DEFAULT SYSTIMESTAMP NOT NULL,
				CONSTRAINT UQ_OUTBOX_DLQ_MESSAGE_ID UNIQUE (message_id)
			)
			""";

		// Leadership-fencing high-water control table (one durable row per scope). The delete-on-sent
		// outbox deletes the winning rows, so the fence high-water cannot live in the outbox rows; this
		// dedicated table survives the drain. Matches the fenced-claim high-water enforcement in OracleOutboxStore.
		var fenceDdl = $"""
			CREATE TABLE {FenceTableName} (
				scope_key           VARCHAR2(600)                   NOT NULL,
				high_water_token    NUMBER(19)                      NOT NULL,
				CONSTRAINT PK_OUTBOX_FENCE PRIMARY KEY (scope_key)
			)
			""";

		await CreateTableIfAbsentAsync(connection, outboxDdl).ConfigureAwait(false);
		await CreateTableIfAbsentAsync(connection, deadLetterDdl).ConfigureAwait(false);
		await CreateTableIfAbsentAsync(connection, fenceDdl).ConfigureAwait(false);

		_initialized = true;
	}

	private static async Task CreateTableIfAbsentAsync(OracleConnection connection, string ddl)
	{
		try
		{
			await using var command = connection.CreateCommand();
			command.CommandText = ddl;
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
		catch (OracleException ex) when (ex.Number == 955)
		{
			// ORA-00955: name is already used by an existing object — table already created.
		}
	}

	/// <summary>Creates a new OracleConnection to the container.</summary>
	/// <returns>A new connection instance.</returns>
	public OracleConnection CreateConnection() => new(ConnectionString);

	/// <summary>Cleans up all rows from the outbox tables between tests.</summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		foreach (var table in new[] { OutboxTableName, DeadLetterTableName, FenceTableName })
		{
			await using var command = connection.CreateCommand();
			command.CommandText = $"DELETE FROM {table}";
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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

	// ODP.NET surfaces TIMESTAMP WITH TIME ZONE as DateTime via IDataReader.GetValue; map both DateTime
	// (assumed UTC) and DateTimeOffset into DateTimeOffset so the store's row properties bind. On write,
	// ODP.NET accepts DateTimeOffset for a TSTZ bind directly.
	private sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
	{
		public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
		{
			parameter.Value = value;
		}

		public override DateTimeOffset Parse(object value)
		{
			return value switch
			{
				DateTimeOffset dto => dto,
				DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
				_ => DateTimeOffset.Parse(value.ToString()!, System.Globalization.CultureInfo.InvariantCulture)
			};
		}
	}
}
