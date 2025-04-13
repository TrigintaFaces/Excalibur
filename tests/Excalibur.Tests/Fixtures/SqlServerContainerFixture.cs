using System.Data;

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

namespace Excalibur.Tests.Fixtures;

public class SqlServerContainerFixture : IDatabaseContainerFixture, IAsyncLifetime
{
	private readonly MsSqlContainer _container = new MsSqlBuilder()
		.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
		.WithName($"mssql-test-{Guid.NewGuid():N}")
		.WithPassword("YourStrong(!)Password")
		.Build();

	public string ConnectionString => _container.GetConnectionString();

	public DatabaseEngine Engine => DatabaseEngine.SqlServer;

	public IDbConnection CreateDbConnection() => new SqlConnection(ConnectionString);

	public Task InitializeAsync() => _container.StartAsync();

	public async Task DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(true);
}
