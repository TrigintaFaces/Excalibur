// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Globalization;

using Microsoft.Data.SqlClient;

using Tests.Shared.Infrastructure;

using Testcontainers.MsSql;

namespace Tests.Shared.Fixtures;

/// <summary>
/// A SQL Server container whose SQL Server Agent is running, so Change Data Capture can actually capture.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists rather than a flag on <see cref="SqlServerContainerFixture"/>.</b> CDC is not a
/// connection-level feature: <c>sp_cdc_enable_table</c> creates two Agent jobs (capture and cleanup), and the
/// capture job is what moves rows from the log into the change tables. The <c>mssql</c> image ships with the
/// Agent DISABLED, so a container started without <c>MSSQL_AGENT_ENABLED</c> accepts every CDC stored
/// procedure, reports <c>is_cdc_enabled = 1</c>, and then captures nothing — the most expensive shape of
/// wrong, because every setup step succeeds and only the assertion fails.
/// </para>
/// <para>
/// Measured before this type was written: of the sites that build an MSSQL container in this repository,
/// <b>none</b> sets <c>MSSQL_AGENT_ENABLED</c>. So no existing fixture can host a CDC test, and an arm
/// written against one could never observe capture regardless of how the arm was written.
/// </para>
/// <para>
/// <b>Why a separate fixture and not the shared one.</b> The Agent is a second process inside the container
/// and it costs start-up time on every suite that uses the fixture it is added to. The shared
/// <see cref="SqlServerContainerFixture"/> has a large consumer set that does not need CDC, so enabling the
/// Agent there would spend that cost on all of them to serve a few. A fixture with genuinely different
/// container configuration is not a thin wrapper, which is the thing the consolidation rule forbids.
/// </para>
/// </remarks>
public sealed class SqlServerCdcContainerFixture : ContainerFixtureBase, IDatabaseContainerFixture
{
	private const string SaPassword = "Test@Pass123";

	private MsSqlContainer? _container;

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	public DatabaseEngine Engine => DatabaseEngine.SqlServer;

	/// <inheritdoc/>
	public IDbConnection CreateDbConnection() => new SqlConnection(ConnectionString);

	/// <summary>
	/// Creates a database, enables CDC on it, and verifies the Agent is running.
	/// </summary>
	/// <param name="databaseName">The database to create and enable CDC on.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>A connection string scoped to the new database.</returns>
	/// <remarks>
	/// The Agent check is here rather than in the arm deliberately. Without it a CDC test fails at its
	/// assertion with "no rows captured", which reads as a defect in the code under test; with it the
	/// failure names the container configuration, which is what is actually wrong.
	/// </remarks>
	public async Task<string> CreateCdcEnabledDatabaseAsync(
		string databaseName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		await ExecuteAsync(ConnectionString, $"CREATE DATABASE [{databaseName}];", cancellationToken)
			.ConfigureAwait(false);

		var scoped = ScopeToDatabase(databaseName);

		var agentState = await ScalarAsync(
			scoped,
			"SELECT status_desc FROM sys.dm_server_services WHERE servicename LIKE 'SQL Server Agent%';",
			cancellationToken).ConfigureAwait(false);

		if (!string.Equals(agentState as string, "Running", StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"SQL Server Agent is '{agentState ?? "absent"}', not 'Running'. CDC capture is driven by "
				+ "Agent jobs, so every CDC stored procedure below would succeed and capture nothing. This is "
				+ "a container configuration fault, not a defect in the code under test.");
		}

		await ExecuteAsync(scoped, "EXEC sys.sp_cdc_enable_db;", cancellationToken).ConfigureAwait(false);

		return scoped;
	}

	/// <summary>
	/// Enables CDC capture on a table and waits until its capture instance is actually present.
	/// </summary>
	/// <param name="databaseConnectionString">A connection scoped to the CDC-enabled database.</param>
	/// <param name="schema">The table's schema.</param>
	/// <param name="table">The table to capture.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>A task that completes when the capture instance exists.</returns>
	public static async Task EnableTableCaptureAsync(
		string databaseConnectionString,
		string schema,
		string table,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseConnectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		ArgumentException.ThrowIfNullOrWhiteSpace(table);

		await EnableCaptureOnceTheAgentAcceptsJobsAsync(databaseConnectionString, schema, table, cancellationToken)
			.ConfigureAwait(false);

		var appeared = await WaitHelpers.WaitUntilAsync(
			async () =>
			{
				var count = await ScalarAsync(
					databaseConnectionString,
					"SELECT COUNT(*) FROM cdc.change_tables ct "
					+ "JOIN sys.tables t ON ct.source_object_id = t.object_id "
					+ "JOIN sys.schemas s ON t.schema_id = s.schema_id "
					+ "WHERE s.name = @schema AND t.name = @table;",
					cancellationToken,
					("@schema", schema),
					("@table", table)).ConfigureAwait(false);

				return Convert.ToInt32(count, CultureInfo.InvariantCulture) > 0;
			},
			TimeSpan.FromSeconds(30),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		if (!appeared)
		{
			throw new InvalidOperationException(
				$"No CDC capture instance appeared for [{schema}].[{table}] within 30s.");
		}
	}

	/// <summary>
	/// Waits until the capture job has moved at least <paramref name="minimumRows"/> rows into the change table.
	/// </summary>
	/// <param name="databaseConnectionString">A connection scoped to the CDC-enabled database.</param>
	/// <param name="captureInstance">The capture instance, e.g. <c>dbo_Orders</c>.</param>
	/// <param name="minimumRows">The number of captured rows to wait for.</param>
	/// <param name="timeout">How long to wait before giving up.</param>
	/// <param name="cancellationToken">Cancels the wait.</param>
	/// <returns><see langword="true"/> if the rows appeared within the timeout.</returns>
	/// <remarks>
	/// Polls rather than sleeping. The capture job runs on its own schedule, so the delay between a write and
	/// its appearance in the change table is a property of the server, not a constant a test may assume — a
	/// fixed sleep is both slower than necessary and flaky under load.
	/// </remarks>
	public static Task<bool> WaitForCapturedRowsAsync(
		string databaseConnectionString,
		string captureInstance,
		int minimumRows,
		TimeSpan timeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseConnectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(captureInstance);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumRows);

		return WaitHelpers.WaitUntilAsync(
			async () =>
			{
				var count = await ScalarAsync(
					databaseConnectionString,
					$"SELECT COUNT(*) FROM cdc.[{RequireSqlIdentifier(captureInstance)}_CT];",
					cancellationToken).ConfigureAwait(false);

				return Convert.ToInt32(count, CultureInfo.InvariantCulture) >= minimumRows;
			},
			timeout,
			cancellationToken: cancellationToken);
	}

	/// <summary>
	/// Calls <c>sp_cdc_enable_table</c>, retrying only while the Agent is still starting its job subsystem.
	/// </summary>
	/// <param name="databaseConnectionString">A connection scoped to the CDC-enabled database.</param>
	/// <param name="schema">The table's schema.</param>
	/// <param name="table">The table to capture.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <remarks>
	/// <para>
	/// <b>The Running check above is a proxy, and this is the requirement.</b>
	/// <c>sys.dm_server_services</c> reports the Agent as <c>Running</c> before its job subsystem will
	/// accept a job, so a container that has only just started fails here with 22836 wrapping 14258,
	/// <i>"Cannot perform this operation while SQLServerAgent is starting"</i> - and it fails for the
	/// FIRST test to touch a fresh container and for no other, which is the signature of a flake nobody
	/// can reproduce. Measured: the arm passed twice, then failed on a cold container in 1 second.
	/// </para>
	/// <para>
	/// Retrying the real call is deliberately preferred to polling a readiness view. The property that
	/// matters is "the Agent will accept this job", and the only check whose predicate IS that property
	/// is the job itself. A readiness view would be a second proxy stacked on the first.
	/// </para>
	/// <para>
	/// The retry is narrow on purpose: any error that is not the starting-up condition is rethrown
	/// immediately, so a genuinely broken CDC configuration still fails fast and loudly instead of being
	/// buried under a minute of retries.
	/// </para>
	/// </remarks>
	private static async Task EnableCaptureOnceTheAgentAcceptsJobsAsync(
		string databaseConnectionString,
		string schema,
		string table,
		CancellationToken cancellationToken)
	{
		var deadline = DateTimeOffset.UtcNow.AddSeconds(90);

		while (true)
		{
			try
			{
				await ExecuteAsync(
					databaseConnectionString,
					"EXEC sys.sp_cdc_enable_table @source_schema = @schema, @source_name = @table, "
					+ "@role_name = NULL, @supports_net_changes = 0;",
					cancellationToken,
					("@schema", schema),
					("@table", table)).ConfigureAwait(false);

				return;
			}
			catch (SqlException ex) when (IsAgentStillStarting(ex) && DateTimeOffset.UtcNow < deadline)
			{
				await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>Identifies the one transient condition worth retrying.</summary>
	/// <param name="exception">The error <c>sp_cdc_enable_table</c> raised.</param>
	/// <returns><see langword="true"/> when the Agent is still starting.</returns>
	/// <remarks>
	/// The inner 14258 is reported through an outer 22836, and the number that survives on
	/// <see cref="SqlException.Number"/> is the outer one - so the inner condition is matched on the
	/// message text it is nested in. Both are required: 22836 alone covers real CDC metadata failures
	/// that must not be retried.
	/// </remarks>
	private static bool IsAgentStillStarting(SqlException exception) =>
		exception.Message.Contains("SQLServerAgent is starting", StringComparison.OrdinalIgnoreCase)
		|| exception.Message.Contains("14258", StringComparison.Ordinal);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MsSqlBuilder()
			.WithBoundedMemory()
			.WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
			.WithName($"mssql-cdc-test-{Guid.NewGuid():N}")
			.WithPassword(SaPassword)

			// The whole reason this fixture exists. Without it every CDC procedure below still succeeds and
			// nothing is ever captured.
			.WithEnvironment("MSSQL_AGENT_ENABLED", "true")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}

	private string ScopeToDatabase(string databaseName) =>
		new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = databaseName }.ConnectionString;

	/// <summary>
	/// Refuses anything that is not a bare SQL identifier, for the one place a name is interpolated.
	/// </summary>
	/// <param name="identifier">The capture-instance name supplied by the caller.</param>
	/// <returns>The identifier, when it is safe to interpolate.</returns>
	/// <remarks>
	/// A capture instance cannot be passed as a parameter - it names an object, and SQL Server does not
	/// accept a parameter where a table name belongs. So this value IS concatenated, and the only honest
	/// answer is to make concatenation safe rather than to annotate it as reviewed. Brackets are rejected
	/// too: permitting them would let a caller close the identifier and continue the statement.
	/// </remarks>
	private static string RequireSqlIdentifier(string identifier)
	{
		foreach (var c in identifier)
		{
			if (!char.IsAsciiLetterOrDigit(c) && c != '_')
			{
				throw new ArgumentException(
					$"'{identifier}' is not a bare SQL identifier. This value is concatenated into a "
					+ "statement because SQL Server does not accept a parameter in place of an object name, "
					+ "so anything outside [A-Za-z0-9_] is refused rather than escaped.",
					nameof(identifier));
			}
		}

		return identifier;
	}

	private static async Task ExecuteAsync(
		string connectionString,
		string sql,
		CancellationToken cancellationToken,
		params (string Name, object Value)[] parameters)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// CA2100: this helper is private and every call site in this file passes a compile-time literal;
		// values travel as parameters, and the single interpolated identifier is validated by
		// RequireSqlIdentifier above. The suppression is on the construction only, not the method.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100
		foreach (var (name, value) in parameters)
		{
			_ = command.Parameters.AddWithValue(name, value);
		}

		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task<object?> ScalarAsync(
		string connectionString,
		string sql,
		CancellationToken cancellationToken,
		params (string Name, object Value)[] parameters)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// CA2100: see ExecuteAsync above - private helper, literal SQL at every call site, values
		// parameterised, and the one interpolated identifier validated before it reaches here.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100
		foreach (var (name, value) in parameters)
		{
			_ = command.Parameters.AddWithValue(name, value);
		}

		return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
	}
}
