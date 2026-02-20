using System.Data;

using Npgsql;

using Testcontainers.PostgreSql;

namespace Tests.Shared.Fixtures;

/// <summary>
/// TestContainer fixture for Postgres database integration tests.
/// </summary>
public sealed class PostgresContainerFixture : ContainerFixtureBase, IDatabaseContainerFixture
{
	private PostgreSqlContainer? _container;

	/// <inheritdoc/>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	public DatabaseEngine Engine => DatabaseEngine.Postgres;

	/// <inheritdoc/>
	public IDbConnection CreateDbConnection() => new NpgsqlConnection(ConnectionString);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithName($"postgres-test-{Guid.NewGuid():N}")
			.WithDatabase("testdb")
			.WithUsername("postgres")
			.WithPassword("postgres_password")
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
