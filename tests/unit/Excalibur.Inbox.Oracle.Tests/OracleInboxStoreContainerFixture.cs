// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

using Tests.Shared.Fixtures;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// Shared fixture for the Oracle InboxStore real-infra conformance tests.
/// </summary>
/// <remarks>
/// Starts a <c>gvenzl/oracle-free</c> container and creates the <c>INBOX_MESSAGES</c> table whose columns
/// mirror exactly what <see cref="OracleInboxStore"/>'s Dapper requests reference. NOT-NULL is applied only
/// to the identity columns and the numeric status/retry columns; the placeholder-row string/BLOB columns
/// are nullable so Oracle's <c>'' → NULL</c> fold cannot violate a NOT-NULL constraint (A0 ruling #5).
/// Registers a tolerant <see cref="DateTimeOffset"/> Dapper handler so TIMESTAMP WITH TIME ZONE columns
/// round-trip through the store's DateTimeOffset properties.
/// </remarks>
public sealed class OracleInboxStoreContainerFixture : ContainerFixtureBase
{
	private OracleContainer? _container;
	private bool _initialized;

	static OracleInboxStoreContainerFixture()
	{
		SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
	}

	/// <summary>Gets the connection string for the Oracle container.</summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>Gets the schema name for the inbox table (empty => connection default schema).</summary>
	public string SchemaName { get; } = string.Empty;

	/// <summary>Gets the table name for the inbox.</summary>
	public string TableName { get; } = "INBOX_MESSAGES";

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new OracleBuilder()
			.WithImage("gvenzl/oracle-free:23-slim-faststart")
			.WithName($"oracle-inboxstore-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Ensures the inbox store schema is initialized.</summary>
	public async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// SINGLE-TENANT shape, deliberately: PK on (MessageId, HandlerType) and NO TenantId column.
		//
		// The package ships two schemas — single-tenant and multi-tenant — and the store VERIFIES at startup
		// that the table it was given matches the mode it was constructed in. This fixture previously applied
		// the multi-tenant shape (TenantId NOT NULL, PK on the triple) while the conformance suite constructs
		// a SINGLE-tenant store, so the store rejected its own table and 26 of 48 arms failed.
		//
		// It is safe to fix here rather than parameterise: this table is used by the conformance suite alone.
		// The tenant-isolation and deployment-mode suites each hand-roll their own tables precisely because
		// they need the other shape, so nothing else observes this one.
		//
		// Oracle has no "CREATE TABLE IF NOT EXISTS"; swallow ORA-00955 (name already used) for idempotence.
		var createTableSql = $"""
			CREATE TABLE {TableName} (
				MessageId          VARCHAR2(255)                   NOT NULL,
				HandlerType        VARCHAR2(500)                   NOT NULL,
				MessageType        VARCHAR2(500),
				Payload            BLOB,
				Metadata           CLOB,
				ReceivedAt         TIMESTAMP(7) WITH TIME ZONE     NOT NULL,
				ProcessedAt        TIMESTAMP(7) WITH TIME ZONE,
				Status             NUMBER(10)     DEFAULT 0        NOT NULL,
				LastError          VARCHAR2(4000),
				RetryCount         NUMBER(10)     DEFAULT 0        NOT NULL,
				LastAttemptAt      TIMESTAMP(7) WITH TIME ZONE,
				NextAttemptAt      TIMESTAMP(7) WITH TIME ZONE,
				LeaseExpiresAtUtc  TIMESTAMP(7) WITH TIME ZONE,
				CorrelationId      VARCHAR2(255),
				Source             VARCHAR2(255),
				CONSTRAINT PK_INBOX_MESSAGES PRIMARY KEY (MessageId, HandlerType)
			)
			""";

		try
		{
			await using var command = connection.CreateCommand();
			command.CommandText = createTableSql;
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
		catch (OracleException ex) when (ex.Number == 955)
		{
			// ORA-00955: name is already used by an existing object — table already created.
		}

		_initialized = true;
	}

	/// <summary>Creates a new OracleConnection to the container.</summary>
	/// <returns>A new connection instance.</returns>
	public OracleConnection CreateConnection() => new(ConnectionString);

	/// <summary>Cleans up all rows from the inbox table between tests.</summary>
	public async Task CleanupTableAsync()
	{
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
	// (assumed UTC — the store stores UTC via SYS_EXTRACT_UTC and DateTimeOffset.UtcNow) and DateTimeOffset
	// into DateTimeOffset so the store's row properties bind. On write, ODP.NET accepts DateTimeOffset for
	// a TSTZ bind directly.
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
