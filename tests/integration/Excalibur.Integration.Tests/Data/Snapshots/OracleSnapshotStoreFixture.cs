// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

using Tests.Shared.Fixtures;

#pragma warning disable CA2100 // SQL strings are safe - identifiers are constants in this test fixture

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Shared fixture for the Oracle snapshot store conformance suite. Provisions a
/// <c>gvenzl/oracle-free</c> container and creates the snapshot table the store expects.
/// </summary>
/// <remarks>
/// <para>
/// <b>The DDL here is derived from the store's own SQL, because no Oracle snapshot DDL is shipped.</b>
/// <c>docs-site/docs/event-sourcing/snapshots.md</c> documents a schema for SQL Server only, and
/// <see cref="Excalibur.EventSourcing.Oracle.OracleSnapshotStore"/> does not auto-create its table.
/// The column set below is taken from the four data requests that read and write it, which are the
/// authoritative source: a fixture DDL invented independently of them produces
/// <c>ORA-00904 invalid identifier</c> on the integration shard only, which is the documented
/// stale-fixture-DDL failure class.
/// </para>
/// <para>
/// Columns bind 1:1 to the unquoted (therefore upper-cased) identifiers in those requests:
/// <c>SNAPSHOTID, AGGREGATEID, AGGREGATETYPE, VERSION, DATA, CREATEDAT, METADATA</c> — plus
/// <c>TENANTID</c>, which the read and delete paths filter on and the MERGE matches on when a tenant
/// scope is active.
/// </para>
/// </remarks>
public sealed class OracleSnapshotStoreFixture : ContainerFixtureBase
{
	private OracleContainer? _container;
	private bool _initialized;

	/// <summary>
	/// Gets the connection string for the Oracle container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets the schema (owning user) the snapshot table lives in, resolved from the container's
	/// connecting user at initialization time rather than assumed.
	/// </summary>
	/// <remarks>
	/// The store defaults to schema <c>EXCALIBUR</c>, which does not exist in a stock
	/// <c>oracle-free</c> container. Resolving <c>SELECT USER FROM DUAL</c> and passing the result to
	/// the store keeps the fixture honest about where it actually created the table.
	/// </remarks>
	public string Schema { get; private set; } = "SYSTEM";

	/// <summary>
	/// Gets the snapshot table name, matching the store's default.
	/// </summary>
	public string TableName { get; } = "EVENTSTORESNAPSHOTS";

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new OracleBuilder()
			.WithImage("gvenzl/oracle-free:23-slim-faststart")
			.WithName($"oracle-snapshot-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Creates the snapshot table if it does not already exist.
	/// </summary>
	public async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using (var userCommand = connection.CreateCommand())
		{
			userCommand.CommandText = "SELECT USER FROM DUAL";
			Schema = (await userCommand.ExecuteScalarAsync().ConfigureAwait(false))?.ToString() ?? "SYSTEM";
		}

		// TENANTID participates in the uniqueness key so two tenants holding a snapshot of the same
		// aggregate occupy two rows rather than colliding on one. NOTE, stated rather than assumed:
		// Oracle does not treat two NULLs as equal, so this constraint does NOT enforce uniqueness for
		// the untenanted (single-tenant) case where TENANTID is NULL. Upsert semantics come from the
		// store's MERGE in either case; the constraint is a backstop for the tenanted path, not the
		// mechanism.
		var createTableSql = $"""
			CREATE TABLE {TableName} (
				SNAPSHOTID      VARCHAR2(255) NOT NULL,
				AGGREGATEID     VARCHAR2(255) NOT NULL,
				AGGREGATETYPE   VARCHAR2(255) NOT NULL,
				VERSION         NUMBER(19) NOT NULL,
				DATA            BLOB,
				CREATEDAT       TIMESTAMP(7) WITH TIME ZONE NOT NULL,
				METADATA        BLOB,
				TENANTID        VARCHAR2(255),
				CONSTRAINT UQ_SNAPSHOT_AGGREGATE_TENANT UNIQUE (AGGREGATEID, AGGREGATETYPE, TENANTID)
			)
			""";

		await using (var command = connection.CreateCommand())
		{
			command.CommandText = createTableSql;
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		_initialized = true;
	}

	/// <summary>
	/// Creates a new <see cref="OracleConnection"/> to the container.
	/// </summary>
	public OracleConnection CreateConnection() => new(ConnectionString);

	/// <summary>
	/// Removes all rows from the snapshot table between tests.
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
			// Suppress disposal errors and timeouts to prevent a test-host crash.
		}
	}
}
