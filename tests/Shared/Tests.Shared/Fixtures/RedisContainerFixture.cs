using Testcontainers.Redis;

namespace Tests.Shared.Fixtures;

/// <summary>
/// TestContainer fixture for Redis cache integration tests.
/// </summary>
public sealed class RedisContainerFixture : ContainerFixtureBase
{
	private RedisContainer? _container;

	/// <summary>
	/// Gets the connection string for the Redis container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new RedisBuilder()
			.WithImage("redis:7-alpine")
			.WithName($"redis-test-{Guid.NewGuid():N}")
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
