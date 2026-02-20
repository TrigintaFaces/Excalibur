using System.Data;

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

namespace Tests.Shared.Fixtures;

/// <summary>
/// TestContainer fixture for SQL Server database integration tests.
/// </summary>
public sealed class SqlServerContainerFixture : ContainerFixtureBase, IDatabaseContainerFixture
{
	private MsSqlContainer? _container;

	/// <inheritdoc/>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	public DatabaseEngine Engine => DatabaseEngine.SqlServer;

	/// <inheritdoc/>
	public IDbConnection CreateDbConnection() => new SqlConnection(ConnectionString);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.WithName($"mssql-test-{Guid.NewGuid():N}")
			.WithPassword("Test@Pass123")
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
}
