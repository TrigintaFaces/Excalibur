// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Shared fixture for Oracle SagaStore TestContainers (gvenzl/oracle-free).
/// </summary>
/// <remarks>
/// <para>
/// The <c>OracleSagaStore</c> does NOT auto-create its table; its Save/Load Dapper requests issue
/// <c>MERGE</c>/<c>SELECT</c> directly against the qualified table name. This fixture provisions
/// <c>DISPATCH.SAGAS</c> (plus the idempotency-key table) whose columns mirror exactly what the store's
/// requests reference — with Oracle-native types: <c>RAW(16)</c> for the Guid key, <c>CLOB</c> for the
/// JSON state, <c>NUMBER(1)</c> for the completion flag, <c>NUMBER(19)</c> for the version, and
/// <c>TIMESTAMP WITH TIME ZONE</c> for the <c>DateTimeOffset</c> columns.
/// </para>
/// <para>
/// The container app user is created as <c>DISPATCH</c>, so the store's default
/// <c>OracleSagaStoreOptions</c> (<c>SchemaName = "DISPATCH"</c>, <c>TableName = "SAGAS"</c>) resolves
/// to that user's own schema.
/// </para>
/// </remarks>
public sealed class OracleSagaStoreContainerFixture : ContainerFixtureBase
{
	private OracleContainer? _container;
	private readonly OneTimeInitializer _initializer = new();

	/// <summary>Gets the schema name for sagas (the store's default, = the container app user).</summary>
	public string SchemaName { get; } = "DISPATCH";

	/// <summary>Gets the table name for sagas (the store's default).</summary>
	public string TableName { get; } = "SAGAS";

	/// <summary>Gets the connection string for the Oracle container.</summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new OracleBuilder()
			// Pinned to the 23 tag. The floating "slim-faststart" tag resolves to an image whose listener never
			// registers the service Testcontainers connects to, so every test in this fixture failed with
			// ORA-12514 before it reached a database. Pinning the major is the fix; renaming the database is not
			// (that image rejects the rename and the container exits 244).
			.WithImage("gvenzl/oracle-free:23-slim-faststart")
			.WithName($"oracle-sagastore-test-{Guid.NewGuid():N}")
			.WithUsername("DISPATCH")
			.WithPassword("Test_Pass123")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Ensures the saga store table and idempotency-key table are initialized.</summary>
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

		// Provision from the DDL the package SHIPS, not a copy of it. A fixture that restates the schema can
		// only ever agree with itself: a defect in Scripts/01-SagaSchema.sql -- a wrong key, a dropped column, a
		// nullable discriminator, a statement that silently does not execute -- is invisible to every test that
		// runs against the restatement, which is the class this suite exists to catch. A mirror cannot detect a
		// defect in the thing it mirrors.
		//
		// The container is built WithUsername("DISPATCH"), so the connected user owns the DISPATCH schema the
		// script names; it needs no CREATE SCHEMA of its own (Oracle has none).
		foreach (var statement in ReadShippedSchemaStatements())
		{
			await ExecuteAsync(connection, statement).ConfigureAwait(false);
		}

	}

	/// <summary>
	/// Reads the shipped DDL and splits it into individual statements.
	/// </summary>
	/// <remarks>
	/// ODP.NET submits one statement per command, so the script is split on ';'. Whole-line comments are
	/// stripped FIRST: a trailing '--' comment would otherwise survive the split, be prepended to the next
	/// statement, and comment it out -- which Oracle rejects with ORA-00900. Same handling the saga-timeout
	/// fixture already uses against its own shipped script.
	/// </remarks>
	private static IEnumerable<string> ReadShippedSchemaStatements()
	{
		var sql = StripComments(File.ReadAllText(ResolveShippedScriptPath()));

		return sql
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(statement => !string.IsNullOrWhiteSpace(statement))
			.ToList();
	}

	/// <summary>Drops whole-line <c>--</c> comments from the script.</summary>
	private static string StripComments(string sql)
	{
		var lines = sql
			.Split('\n')
			.Select(line => line.Trim())
			.Where(line => !line.StartsWith("--", StringComparison.Ordinal));

		return string.Join('\n', lines);
	}

	/// <summary>
	/// Locates the shipped script by walking up from the test binary to the repository root. Fails loudly
	/// rather than falling back to an inline copy: a missing product script is the defect, not a licence to
	/// invent a schema.
	/// </summary>
	private static string ResolveShippedScriptPath()
	{
		const string RelativePath = "src/Excalibur/Excalibur.Saga.Oracle/Scripts/01-SagaSchema.sql";

		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			var candidate = Path.Combine(directory.FullName, RelativePath.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(candidate))
			{
				return candidate;
			}

			directory = directory.Parent;
		}

		throw new FileNotFoundException(
			$"The shipped Oracle saga DDL was not found by walking up from '{AppContext.BaseDirectory}' "
			+ $"looking for '{RelativePath}'. This fixture provisions its schema from the script the package "
			+ "ships; it deliberately does not carry its own copy.");
	}

	/// <summary>Creates a new OracleConnection to the container.</summary>
	/// <returns>A new connection instance.</returns>
	public OracleConnection CreateConnection() => new(ConnectionString);

	/// <summary>Cleans up all rows from the sagas table between tests.</summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await ExecuteAsync(connection, $"DELETE FROM {SchemaName}.{TableName}").ConfigureAwait(false);
	}

	private static async Task ExecuteAsync(OracleConnection connection, string sql)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
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
