// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

using Testcontainers.PostgreSql;
using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings are safe - table name is a constant in test fixture

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Shared fixture for Postgres Saga Store TestContainers.
/// </summary>
/// <remarks>
/// Creates and manages a Postgres container with the saga store schema.
/// Uses postgres:16-alpine for fast container startup.
/// Enables Npgsql legacy timestamp behavior to map TIMESTAMPTZ to DateTimeOffset.
/// </remarks>
public sealed class PostgresSagaStoreContainerFixture : ContainerFixtureBase
{
	private PostgreSqlContainer? _container;
	private readonly OneTimeInitializer _initializer = new();

	/// <summary>
	/// Gets the connection string for the Postgres container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets the schema name for the saga store.
	/// </summary>
	public string Schema { get; } = "dispatch";

	/// <summary>
	/// Gets the table name for sagas.
	/// </summary>
	public string TableName { get; } = "sagas";

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(4);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithName($"postgres-sagastore-test-{Guid.NewGuid():N}")
			.WithDatabase("sagastore_test")
			.WithUsername("postgres")
			.WithPassword("postgres_password")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Ensures the saga store schema is initialized.
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

		// Provision from the DDL the package SHIPS, not a copy of it. A fixture that restates the schema can
		// only ever agree with itself: a defect in Scripts/01-SagaSchema.sql -- a wrong key, a dropped column, a
		// nullable discriminator, a statement that silently does not execute -- is invisible to every test that
		// runs against the restatement, which is the class this suite exists to catch. A mirror cannot detect a
		// defect in the thing it mirrors.
		//
		// Executed as ONE command rather than split on ';': the script's upgrade path is a DO $$ ... $$ block
		// whose body contains semicolons, and splitting on them would submit its fragments as separate
		// statements. Npgsql sends a multi-statement command in a single implicit transaction, which is also
		// what a consumer running the file through psql gets.
		var shippedDdl = await File.ReadAllTextAsync(ResolveShippedScriptPath()).ConfigureAwait(false);

		await using var command = new NpgsqlCommand(shippedDdl, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

	}

	/// <summary>
	/// Locates the shipped script by walking up from the test binary to the repository root. Fails loudly
	/// rather than falling back to an inline copy: a missing product script is the defect, not a licence to
	/// invent a schema.
	/// </summary>
	private static string ResolveShippedScriptPath()
	{
		const string RelativePath = "src/Excalibur/Excalibur.Saga.Postgres/Scripts/01-SagaSchema.sql";

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
			$"The shipped Postgres saga DDL was not found by walking up from '{AppContext.BaseDirectory}' "
			+ $"looking for '{RelativePath}'. This fixture provisions its schema from the script the package "
			+ "ships; it deliberately does not carry its own copy.");
	}

	/// <summary>
	/// Creates a new NpgsqlConnection to the container.
	/// </summary>
	/// <returns>A new connection instance.</returns>
	public NpgsqlConnection CreateConnection()
	{
		return new NpgsqlConnection(ConnectionString);
	}

	/// <summary>
	/// Cleans up all items from the sagas table.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var truncateSql = $"TRUNCATE TABLE \"{Schema}\".\"{TableName}\" RESTART IDENTITY";
		await using var command = new NpgsqlCommand(truncateSql, connection);
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
			// Suppress disposal errors and timeouts to prevent test host crash
		}
	}
}

/// <summary>
/// xUnit collection definition for Postgres Saga Store integration tests.
/// </summary>
[CollectionDefinition("PostgresSagaStore")]
public class PostgresSagaStoreTestCollection : ICollectionFixture<PostgresSagaStoreContainerFixture>
{
}
