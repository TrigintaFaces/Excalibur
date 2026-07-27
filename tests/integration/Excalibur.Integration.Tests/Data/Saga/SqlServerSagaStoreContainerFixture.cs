// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Shared fixture for SQL Server SagaStore TestContainers, provisioned from the <b>shipped</b>
/// <c>Excalibur.Saga.SqlServer/Scripts/01-SagaSchema.sql</c>.
/// </summary>
/// <remarks>
/// <para>
/// The <c>SqlServerSagaStore</c> does NOT auto-create its table — its Save/Load Dapper requests issue
/// <c>MERGE</c>/<c>SELECT</c> directly against the qualified table name with no DDL bootstrap. The schema
/// therefore has to come from somewhere, and it comes from the script the package ships to consumers.
/// </para>
/// <para>
/// This fixture previously carried an inline copy of that DDL, and its own comment described it as one that
/// <em>"mirrors <c>Scripts/01-SagaSchema.sql</c>"</em>. A mirror cannot detect a defect in the thing it
/// reflects. The copy declared <c>CompletedAt DATETIME2</c> — the exact offset-discarding column that
/// silently deletes sagas before their retention window closes — and every test in this suite passed
/// against it, because they all ran on the copy and none on the script. Correcting the shipped script would
/// not have turned a single test red.
/// </para>
/// <para>
/// So the schema is created by executing the shipped script. If the script is wrong these tests fail; if it
/// changes they follow. That is the only arrangement in which a green here is a statement about what a
/// consumer receives.
/// </para>
/// <para>
/// The schema/table names match the store's default <c>SqlServerSagaStoreOptions</c>
/// (<c>SchemaName = "dispatch"</c>, <c>TableName = "sagas"</c>), which is what the script hardcodes, so the
/// simple <c>new SqlServerSagaStore(connectionString, logger, serializer)</c> constructor resolves to
/// <c>[dispatch].[sagas]</c>.
/// </para>
/// </remarks>
public sealed class SqlServerSagaStoreContainerFixture : ContainerFixtureBase
{
	private MsSqlContainer? _container;
	private bool _initialized;

	/// <summary>
	/// Gets the schema name for sagas (the store's default).
	/// </summary>
	public string SchemaName { get; } = "dispatch";

	/// <summary>
	/// Gets the table name for sagas (the store's default).
	/// </summary>
	public string TableName { get; } = "sagas";

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
			.WithName($"mssql-sagastore-test-{Guid.NewGuid():N}")
			.WithPassword("Test@Pass123")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Ensures the saga store schema and table are initialized, by executing the shipped DDL script.
	/// </summary>
	public async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		foreach (var batch in ReadShippedSchemaBatches())
		{
			await using var command = new SqlCommand(batch, connection);
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		_initialized = true;
	}

	/// <summary>
	/// Reads the shipped DDL and splits it into batches on the <c>GO</c> separator.
	/// </summary>
	/// <remarks>
	/// <c>GO</c>, not <c>;</c>. This script's <c>CREATE TABLE</c> and its three <c>CREATE INDEX</c> statements
	/// live inside one <c>BEGIN…END</c> block and are separated by semicolons; splitting on <c>;</c> would cut
	/// the block open and submit its fragments as batches. <c>GO</c> is the batch separator SQL Server's own
	/// tooling uses, and it is what the script is written against.
	/// </remarks>
	private static IEnumerable<string> ReadShippedSchemaBatches()
	{
		var batches = new List<string>();
		var current = new List<string>();

		foreach (var line in File.ReadAllLines(ResolveShippedScriptPath()))
		{
			if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
			{
				AppendBatch(batches, current);
				current.Clear();
			}
			else
			{
				current.Add(line);
			}
		}

		AppendBatch(batches, current);

		return batches;

		static void AppendBatch(List<string> batches, List<string> lines)
		{
			var batch = string.Join('\n', lines);
			if (!string.IsNullOrWhiteSpace(batch))
			{
				batches.Add(batch);
			}
		}
	}

	/// <summary>
	/// Locates the shipped script by walking up to the repository root. Fails loudly rather than falling back
	/// to an inline copy: a missing product script is the defect, not a licence to invent a schema.
	/// </summary>
	private static string ResolveShippedScriptPath()
	{
		const string RelativePath = "src/Excalibur/Excalibur.Saga.SqlServer/Scripts/01-SagaSchema.sql";

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
			$"The shipped SQL Server saga DDL was not found by walking up from '{AppContext.BaseDirectory}' "
			+ $"looking for '{RelativePath}'. This fixture provisions its schema from the script the package "
			+ "ships; it deliberately does not carry its own copy.");
	}

	/// <summary>
	/// Creates a new SqlConnection to the container.
	/// </summary>
	/// <returns>A new connection instance.</returns>
	public SqlConnection CreateConnection() => new(ConnectionString);

	/// <summary>
	/// Cleans up all rows from the sagas table between tests.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var truncateSql = $"TRUNCATE TABLE [{SchemaName}].[{TableName}]";
		await using var command = new SqlCommand(truncateSql, connection);
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
