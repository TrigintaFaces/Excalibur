// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings are safe - the DDL is the shipped product script; DELETE targets constant identifiers

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// SQL Server TestContainers fixture for <c>SqlServerSagaTimeoutStore</c>, provisioned from the
/// <b>shipped</b> <c>Excalibur.Saga.SqlServer/Scripts/SagaTimeouts.sql</c>.
/// </summary>
/// <remarks>
/// The schema is created by executing the same script the package ships to consumers. A fixture that
/// carries its own copy certifies the store against a table no consumer has, and the drift is invisible
/// until production. Reading the shipped script makes that drift impossible: if the script is wrong these
/// tests fail; if it changes they follow.
/// </remarks>
public sealed class SqlServerSagaTimeoutStoreContainerFixture : ContainerFixtureBase
{
	private MsSqlContainer? _container;
	private readonly OneTimeInitializer _initializer = new();

	/// <summary>Gets the schema name (the store's default).</summary>
	public string SchemaName { get; } = "dbo";

	/// <summary>Gets the table name (the store's default).</summary>
	public string TableName { get; } = "SagaTimeouts";

	/// <summary>Gets the connection string for the container.</summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MsSqlBuilder()
			.WithBoundedMemory()
			.WithName($"mssql-sagatimeout-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Creates the timeouts table by executing the shipped DDL script.</summary>
	public Task EnsureInitializedAsync() => _initializer.RunAsync(InitializeSchemaAsync);

	/// <summary>
	/// Provisions the schema. Runs once, through <see cref="OneTimeInitializer"/>, so a failure
	/// here is rethrown to every later caller instead of being retried against a database this
	/// call already half-provisioned.
	/// </summary>
	private async Task InitializeSchemaAsync()
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		foreach (var statement in ReadShippedSchemaStatements())
		{
			await ExecuteAsync(connection, statement).ConfigureAwait(false);
		}

	}

	/// <summary>Removes all rows between tests.</summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await ExecuteAsync(connection, $"DELETE FROM [{SchemaName}].[{TableName}]").ConfigureAwait(false);
	}

	/// <summary>
	/// Reads the shipped DDL and splits it into statements. Comments are stripped <b>before</b> the split:
	/// script prose contains semicolons, and splitting first tears a comment and prepends its tail to the
	/// next statement.
	/// </summary>
	private static IEnumerable<string> ReadShippedSchemaStatements()
	{
		var sql = StripComments(File.ReadAllText(ResolveShippedScriptPath()));

		return sql
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(statement => !string.IsNullOrWhiteSpace(statement))
			.ToList();
	}

	private static string StripComments(string sql)
	{
		var lines = sql
			.Split('\n')
			.Select(line => line.Trim())
			.Where(line => !line.StartsWith("--", StringComparison.Ordinal));

		return string.Join('\n', lines);
	}

	/// <summary>
	/// Locates the shipped script by walking up to the repository root. Fails loudly rather than falling
	/// back to an inline copy: a missing product script is the defect, not a reason to invent a schema.
	/// </summary>
	private static string ResolveShippedScriptPath()
	{
		const string RelativePath = "src/Excalibur/Excalibur.Saga.SqlServer/Scripts/SagaTimeouts.sql";

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
			$"The shipped SQL Server saga-timeout DDL was not found by walking up from '{AppContext.BaseDirectory}' "
			+ $"looking for '{RelativePath}'. The conformance fixture provisions its schema from the script the "
			+ "package ships; it deliberately does not carry its own copy.");
	}

	private static async Task ExecuteAsync(SqlConnection connection, string sql)
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
