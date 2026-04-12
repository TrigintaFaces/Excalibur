// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using System.Data;

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;

using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

/// <summary>
/// Fixture for SQL Server container for audit store and key escrow integration tests.
/// </summary>
public class SqlServerContainerFixture : ContainerFixtureBase
{
	private MsSqlContainer? _container;

	/// <summary>
	/// Gets the connection string for the SQL Server container.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when the container has not been initialized or failed to start.</exception>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException(
			$"SQL Server container not initialized. DockerAvailable={DockerAvailable}, Error={InitializationError ?? "none"}");

	/// <summary>
	/// Creates a new database connection.
	/// </summary>
	public IDbConnection CreateDbConnection() => new SqlConnection(ConnectionString);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.WithName($"mssql-compliance-test-{Guid.NewGuid():N}")
			.WithPassword("YourStrong(!)Password")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Executes a SQL script against the database.
	/// </summary>
	public async Task ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities - test code with controlled input
		command.CommandText = script;
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync();
		}
	}
}

/// <summary>
/// Collection definition for SQL Server integration tests.
/// </summary>
[CollectionDefinition(Name)]
public class SqlServerTestCollection : ICollectionFixture<SqlServerContainerFixture>
{
	public const string Name = "SqlServer";
}
