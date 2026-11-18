using Tests.Shared.Infrastructure;
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

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

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
		var ownerToken = Environment.GetEnvironmentVariable("DISPATCH_BENCHMARK_OWNER_TOKEN");
		if (ownerToken is not null && (ownerToken.Length != 32 ||
		    ownerToken.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))))
		{
		    throw new InvalidOperationException("DISPATCH_BENCHMARK_OWNER_TOKEN must be 32 lowercase hexadecimal characters.");
		}

		var builder = new MsSqlBuilder()
			.WithBoundedMemory()
			.WithImage(TestContainerImages.SqlServer2022)
			.WithName($"mssql-test-{Guid.NewGuid():N}")
			.WithPassword("Test@Pass123")
			.WithCleanUp(true);
		if (ownerToken is not null)
		{
			builder = builder.WithLabel("dispatch.benchmark.owner", ownerToken)
				.WithLabel("dispatch.benchmark.fixture", nameof(SqlServerContainerFixture))
				.WithCreateParameterModifier(parameters => parameters.HostConfig.Memory = 4L * 1024 * 1024 * 1024);
		}

		_container = builder.Build();

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
