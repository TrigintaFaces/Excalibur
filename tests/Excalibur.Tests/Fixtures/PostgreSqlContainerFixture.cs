using System.Data;

using Npgsql;

using Testcontainers.PostgreSql;

namespace Excalibur.Tests.Fixtures;

public class PostgreSqlContainerFixture : IDatabaseContainerFixture, IAsyncLifetime
{
	private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
		.WithImage("postgres:latest")
		.WithName($"postgres-test-{Guid.NewGuid():N}")
		.WithDatabase("TestDb")
		.WithUsername("postgres")
		.WithPassword("postgres_password")
		.Build();

	public string ConnectionString => _container.GetConnectionString();

	public DatabaseEngine Engine => DatabaseEngine.PostgreSql;

	public IDbConnection CreateDbConnection() => new NpgsqlConnection(ConnectionString);

	public Task InitializeAsync() => _container.StartAsync();

	public async Task DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(true);
}
