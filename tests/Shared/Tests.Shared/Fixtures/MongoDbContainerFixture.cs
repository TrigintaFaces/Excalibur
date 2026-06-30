using Testcontainers.MongoDb;

namespace Tests.Shared.Fixtures;

/// <summary>
/// TestContainer fixture for MongoDB integration tests.
/// </summary>
/// <remarks>
/// The container is started as a single-node <b>replica set</b> (<c>rs0</c>). A replica set is a hard
/// requirement for MongoDB <b>change streams</b> (CDC) and multi-document transactions; a single-node
/// replica set is strictly more capable than a standalone server and supports all standalone CRUD, so it
/// is safe for every consumer of this shared fixture. Testcontainers runs <c>rs.initiate()</c> and waits
/// for the set to elect a primary before <see cref="InitializeContainerAsync"/> returns (e9u90j).
/// </remarks>
public sealed class MongoDbContainerFixture : ContainerFixtureBase
{
	private MongoDbContainer? _container;

	/// <summary>
	/// Gets the connection string for the MongoDB container. The string is replica-set aware so callers
	/// can open change streams without further configuration.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MongoDbBuilder()
			.WithImage("mongo:7")
			.WithName($"mongo-test-{Guid.NewGuid():N}")
			// Single-node replica set — required for change streams (CDC) and transactions.
			.WithReplicaSet("rs0")
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
