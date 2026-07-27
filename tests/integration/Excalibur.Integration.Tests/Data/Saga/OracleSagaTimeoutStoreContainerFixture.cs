// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

using Tests.Shared.Fixtures;

#pragma warning disable CA2100 // SQL strings are safe - the DDL is the shipped product script; DELETE targets constant identifiers

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Oracle TestContainers fixture for <c>OracleSagaTimeoutStore</c>, provisioned from the <b>shipped</b>
/// <c>Excalibur.Saga.Oracle/Scripts/SagaTimeouts.sql</c>.
/// </summary>
/// <remarks>
/// <para>
/// The schema is created by executing the same script the package ships to consumers, not by a
/// hand-written copy in the test tree. A fixture that invents its own DDL certifies the store's PL/SQL
/// against a table no consumer has, and any drift between the two is invisible until production. Reading
/// the shipped script makes that drift impossible by construction: if the script is wrong, these tests
/// fail; if the script changes, these tests follow.
/// </para>
/// <para>
/// The container's application user is <c>DISPATCH</c>, which matches the store's default
/// <c>OracleSagaTimeoutStoreOptions.SchemaName</c>, so the unqualified objects the script creates resolve
/// to that user's own schema.
/// </para>
/// </remarks>
public sealed class OracleSagaTimeoutStoreContainerFixture : ContainerFixtureBase
{
	private OracleContainer? _container;
	private bool _initialized;

	/// <summary>Gets the schema name (the store's default, = the container app user).</summary>
	public string SchemaName { get; } = "DISPATCH";

	/// <summary>Gets the table name (the store's default).</summary>
	public string TableName { get; } = "SAGATIMEOUTS";

	/// <summary>Gets the connection string for the Oracle container.</summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new OracleBuilder()
			// Pinned to the 23 tag. The floating "slim-faststart" tag resolves to an image whose listener never
			// registers the service Testcontainers connects to (ORA-12514). Pinning the major is sufficient:
			// this fixture connects with no database rename, verified 8/8.
			.WithImage("gvenzl/oracle-free:23-slim-faststart")
			.WithName($"oracle-sagatimeout-test-{Guid.NewGuid():N}")
			.WithUsername("DISPATCH")
			.WithPassword("Test_Pass123")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Creates the timeouts table by executing the shipped DDL script.</summary>
	public async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		foreach (var statement in ReadShippedSchemaStatements())
		{
			await ExecuteAsync(connection, statement).ConfigureAwait(false);
		}

		_initialized = true;
	}

	/// <summary>Creates a new connection to the container.</summary>
	/// <returns>A new connection instance.</returns>
	public OracleConnection CreateConnection() => new(ConnectionString);

	/// <summary>Removes all rows between tests.</summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await ExecuteAsync(connection, $"DELETE FROM {SchemaName}.{TableName}").ConfigureAwait(false);
	}

	/// <summary>
	/// Reads the shipped DDL script and splits it into individual statements. Oracle's client rejects a
	/// batch containing multiple statements, and it does not accept a trailing semicolon on a DDL command.
	/// </summary>
	/// <remarks>
	/// Comments are stripped <b>before</b> the split, not after. The script's prose contains semicolons
	/// (for example "<c>default table is SAGATIMEOUTS; both are configurable</c>"), so splitting first tears
	/// a comment in half and prepends its tail to the next statement — which Oracle rejects with ORA-00900.
	/// </remarks>
	private static IEnumerable<string> ReadShippedSchemaStatements()
	{
		var scriptPath = ResolveShippedScriptPath();
		var sql = StripComments(File.ReadAllText(scriptPath));

		return sql
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(statement => !string.IsNullOrWhiteSpace(statement))
			.ToList();
	}

	/// <summary>
	/// Drops whole-line <c>--</c> comments from the script.
	/// </summary>
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
	/// rather than falling back to an inline copy: a missing product script is the defect, not a reason to
	/// invent a schema.
	/// </summary>
	private static string ResolveShippedScriptPath()
	{
		const string RelativePath = "src/Excalibur/Excalibur.Saga.Oracle/Scripts/SagaTimeouts.sql";

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
			$"The shipped Oracle saga-timeout DDL was not found by walking up from '{AppContext.BaseDirectory}' "
			+ $"looking for '{RelativePath}'. The conformance fixture provisions its schema from the script the "
			+ "package ships; it deliberately does not carry its own copy.");
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
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
			_container = null;
		}
	}
}
