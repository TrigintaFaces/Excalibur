// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

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
	private readonly OneTimeInitializer _initializer = new();

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

		// The schema is the one the package SHIPS, applied in the order a consumer applies it. This
		// fixture used to restate the DDL, and a restatement can drift permissively -- a nullable column
		// where the script says NOT NULL -- leaving every arm running against it structurally unable to
		// detect the divergence it exists to catch, while still reporting green. Oracle has no container
		// in CI, so unlike the Postgres and SqlServer fixtures there is no integration shard to catch the
		// drift later: provisioning from the script is the only thing standing in for it.
		//
		// The table names below are the script defaults, which is why it can be applied unmodified.
		var units = new[]
		{
			"src/Excalibur/Excalibur.Outbox.Oracle/Scripts/001_CreateOutboxSchema.sql",
			"src/Excalibur/Excalibur.Outbox.Oracle/Scripts/002_MakeOutboxTenantTotal.sql",
			"src/Excalibur/Excalibur.Outbox.Oracle/Scripts/003_CarryTenantOnDeadLetters.sql",
		}.SelectMany(ShippedSchemaScript.ReadOracleUnits);

		foreach (var unit in units)
		{
			await CreateTableIfAbsentAsync(connection, unit).ConfigureAwait(false);
		}
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
