using DotNet.Testcontainers.Builders;

using Testcontainers.Kafka;

namespace Tests.Shared.Fixtures;

/// <summary>
/// TestContainer fixture for Kafka messaging integration tests.
/// </summary>
public sealed class KafkaContainerFixture : ContainerFixtureBase
{
	private KafkaContainer? _container;

	/// <summary>
	/// Gets the bootstrap servers address for the Kafka container.
	/// </summary>
	public string BootstrapServers => _container?.GetBootstrapAddress()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new KafkaBuilder()
			.WithImage("confluentinc/cp-kafka:7.5.0")
			.WithName($"kafka-test-{Guid.NewGuid():N}")
			.WithPortBinding(9092, true)
			.WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(".*\\[KafkaServer id=\\d+\\] started.*"))
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
