using DotNet.Testcontainers.Builders;

using Testcontainers.Kafka;

using Tests.Shared.Infrastructure;

namespace Tests.Shared.Fixtures;

/// <summary>
/// TestContainer fixture for Kafka messaging integration tests.
/// </summary>
/// <remarks>
/// <para>
/// Kafka's native rdkafka library can crash the entire test host process if the broker
/// dies or is unreachable mid-run. To mitigate this:
/// </para>
/// <list type="bullet">
/// <item><description>Image version is pinned to avoid breaking changes from latest tags.</description></item>
/// <item><description>Wait strategy uses port availability (broker port 9093) instead of fragile log regex matching.</description></item>
/// <item><description>Startup timeout is extended to 3 minutes (Kafka + KRaft controller startup is heavier than most containers).</description></item>
/// <item><description>All Kafka integration tests should share this fixture via <c>[Collection(ContainerCollections.Kafka)]</c>
/// rather than creating per-class containers, to reduce Docker resource contention.</description></item>
/// </list>
/// </remarks>
public sealed class KafkaContainerFixture : ContainerFixtureBase
{
	/// <summary>
	/// Default Kafka Docker image. Pinned to a specific version to prevent CI drift.
	/// </summary>
	/// <remarks>
	/// The Testcontainers.Kafka library uses KRaft mode (no ZooKeeper) starting from cp-kafka 7.4+.
	/// The internal broker listener is on port 9093; the external (mapped) listener uses a dynamic port.
	/// </remarks>
	public const string DefaultImage = "confluentinc/cp-kafka:7.6.1";

	private KafkaContainer? _container;

	/// <summary>
	/// Gets the bootstrap servers address for the Kafka container.
	/// </summary>
	public string BootstrapServers => _container?.GetBootstrapAddress()
		?? "localhost:9092"; // Fallback when container unavailable; tests should check DockerAvailable first

	/// <summary>
	/// Gets a value indicating whether the container is ready to accept connections.
	/// </summary>
	public bool IsReady => _container is not null && DockerAvailable;

	/// <inheritdoc/>
	/// <remarks>
	/// Kafka with KRaft mode takes longer to start than typical containers (controller election,
	/// broker registration, topic auto-creation readiness). Use 3 minutes to avoid flaky CI timeouts.
	/// </remarks>
	protected override TimeSpan ContainerStartTimeout => TestTimeouts.Scale(TimeSpan.FromMinutes(3));

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new KafkaBuilder()
			.WithImage(DefaultImage)
			.WithName($"kafka-test-{Guid.NewGuid():N}")
			.WithWaitStrategy(Wait.ForUnixContainer()
				.UntilMessageIsLogged(".*Kafka Server started.*")
				.UntilPortIsAvailable(9093))
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
