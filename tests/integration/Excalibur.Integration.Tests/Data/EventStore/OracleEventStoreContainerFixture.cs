// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings are safe - identifiers are constants in test fixture

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Shared fixture for Oracle EventStore TestContainers. Creates and manages a
/// <c>gvenzl/oracle-free</c> container with the event store schema.
/// </summary>
public sealed class OracleEventStoreContainerFixture : ContainerFixtureBase
{
	private OracleContainer? _container;
	private readonly OneTimeInitializer _initializer = new();
	private bool _schemaCreated;

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

		using (var userCommand = connection.CreateCommand())
		{
			userCommand.CommandText = "SELECT USER FROM DUAL";
			Schema = (await userCommand.ExecuteScalarAsync().ConfigureAwait(false))?.ToString() ?? "SYSTEM";
		}

		// Provision from the DDL the package SHIPS, not a copy of it. The copy had diverged: it declared
		// TENANTID nullable and left it out of the stream key, while Scripts/003 declares it NOT NULL DEFAULT
		// '__untenanted__' and keys on (AGGREGATEID, AGGREGATETYPE, VERSION, TENANTID). Both halves matter, and
		// the nullability half is Oracle-specific: Oracle treats NULLs as DISTINCT in a unique index, so a
		// nullable tenant column leaves untenanted rows unconstrained by the stream key altogether. Against the
		// copy, the store's own optimistic concurrency was being asserted against a key the product never ships.
		// A mirror cannot detect a defect in the thing it mirrors -- and it regenerates the moment someone edits
		// one side, which is why this reads the file rather than restating it.
		foreach (var statement in ReadShippedSchemaStatements())
		{
			await ExecuteAsync(connection, statement).ConfigureAwait(false);
		}

		_schemaCreated = true;
	}

	/// <summary>
	/// Reads the shipped event-store DDL and splits it into individual statements.
	/// </summary>
	/// <remarks>
	/// ODP.NET submits one statement per command, so the script is split on ';'. Whole-line comments are
	/// stripped FIRST: a trailing '--' comment would otherwise survive the split, be prepended to the next
	/// statement, and comment it out -- which Oracle rejects with ORA-00900. Same handling the Oracle saga
	/// fixtures already use against their own shipped scripts.
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
		const string RelativePath =
			"src/Excalibur/Excalibur.EventSourcing.Oracle/Scripts/003_CreateEventStoreSchema.sql";

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
			$"The shipped Oracle event-store DDL was not found by walking up from '{AppContext.BaseDirectory}' "
			+ $"looking for '{RelativePath}'. This fixture provisions its schema from the script the package "
			+ "ships; it deliberately does not carry its own copy.");
	}

	private static async Task ExecuteAsync(OracleConnection connection, string sql)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
		if (!_schemaCreated)
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
