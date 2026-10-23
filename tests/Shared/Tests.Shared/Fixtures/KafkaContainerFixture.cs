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
	/// <para>
	/// The upstream Apache image, not a vendor repackage, and the reason is a failure class rather
	/// than a preference. Kafka removed ZooKeeper in 4.0, so this image is KRaft-only and carries no
	/// Python entrypoint tooling. The Confluent image this replaced started by running
	/// <c>dub</c> (confluent-docker-utils), which imports <c>boto3</c> and therefore <c>urllib3</c>;
	/// compiling urllib3's IPv6 pattern crashed that image's Python 3.9 with
	/// <c>UnboundLocalError: local variable 'skipyes' referenced before assignment</c> in
	/// <c>sre_compile</c>, so the container exited 1 before the broker ever started and every Kafka
	/// arm failed at fixture initialisation. Nothing in that path is Kafka's, ours, or fixable by us.
	/// </para>
	/// <para>
	/// The internal broker listener is on port 9093; the external (mapped) listener uses a dynamic port.
	/// </para>
	/// <para>
	/// A superseded version of this comment claimed "Testcontainers.Kafka uses KRaft mode (no
	/// ZooKeeper) starting from cp-kafka 7.4+". That was false against the image it described: the
	/// container's own first log line read <c>Running in Zookeeper mode...</c>, which is exactly the
	/// path that invoked the Python helper. It is quoted here because the claim is what hid the
	/// failure path, and because a comment asserting a constraint is a claim to verify.
	/// </para>
	/// </remarks>
	public const string DefaultImage = "apache/kafka:4.1.2";

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
				.UntilInternalTcpPortIsAvailable(9093))
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
