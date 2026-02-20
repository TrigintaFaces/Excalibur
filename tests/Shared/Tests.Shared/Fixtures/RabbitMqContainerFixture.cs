// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Builders;

using Testcontainers.RabbitMq;

namespace Tests.Shared.Fixtures;

/// <summary>
/// TestContainer fixture for RabbitMQ messaging integration tests.
/// </summary>
/// <remarks>
/// <para>
/// This fixture provides a RabbitMQ container with the management plugin enabled,
/// allowing both AMQP messaging (port 5672) and HTTP management API (port 15672).
/// </para>
/// <para>
/// Uses RabbitMQ 3.12 with Alpine variant for smaller image size (~150MB vs ~350MB).
/// The management plugin enables debugging via HTTP API.
/// </para>
/// <para>
/// <b>Usage with xUnit Collection Fixture:</b>
/// <code>
/// [Collection(ContainerCollections.RabbitMQ)]
/// public class RabbitMqConnectionShould
/// {
///     private readonly RabbitMqContainerFixture _container;
///
///     public RabbitMqConnectionShould(RabbitMqContainerFixture container)
///         =&gt; _container = container;
///
///     [Fact]
///     public async Task EstablishConnection()
///     {
///         var factory = new ConnectionFactory { Uri = new Uri(_container.ConnectionString) };
///         await using var connection = await factory.CreateConnectionAsync();
///         connection.IsOpen.ShouldBeTrue();
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class RabbitMqContainerFixture : ContainerFixtureBase
{
	/// <summary>
	/// Default AMQP port for RabbitMQ.
	/// </summary>
	public const int DefaultAmqpPort = 5672;

	/// <summary>
	/// Default management HTTP port for RabbitMQ.
	/// </summary>
	public const int DefaultManagementPort = 15672;

	/// <summary>
	/// Default RabbitMQ Docker image.
	/// </summary>
	/// <remarks>
	/// Uses RabbitMQ 3.12 with management plugin and Alpine Linux for smaller footprint.
	/// </remarks>
	public const string DefaultImage = "rabbitmq:3.12-management-alpine";

	/// <summary>
	/// Default username for the RabbitMQ container.
	/// </summary>
	public const string DefaultUsername = "guest";

	/// <summary>
	/// Default password for the RabbitMQ container.
	/// </summary>
	public const string DefaultPassword = "guest";

	private RabbitMqContainer? _container;

	/// <summary>
	/// Gets the AMQP connection string for the RabbitMQ container.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when container is not initialized.</exception>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized. Ensure InitializeAsync() has been called.");

	/// <summary>
	/// Gets the hostname of the running RabbitMQ container.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when container is not initialized.</exception>
	public string Hostname => _container?.Hostname
		?? throw new InvalidOperationException("Container not initialized. Ensure InitializeAsync() has been called.");

	/// <summary>
	/// Gets the mapped AMQP port (5672) of the running container.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when container is not initialized.</exception>
	public int AmqpPort => _container?.GetMappedPublicPort(DefaultAmqpPort)
		?? throw new InvalidOperationException("Container not initialized. Ensure InitializeAsync() has been called.");

	/// <summary>
	/// Gets the mapped management HTTP port (15672) of the running container.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when container is not initialized.</exception>
	public int ManagementPort => _container?.GetMappedPublicPort(DefaultManagementPort)
		?? throw new InvalidOperationException("Container not initialized. Ensure InitializeAsync() has been called.");

	/// <summary>
	/// Gets the management HTTP URL for the RabbitMQ container.
	/// </summary>
	/// <remarks>
	/// Use this URL to access the RabbitMQ management console or HTTP API for debugging.
	/// Example: http://localhost:32768
	/// </remarks>
	/// <exception cref="InvalidOperationException">Thrown when container is not initialized.</exception>
	public string ManagementUrl => _container is not null
		? $"http://{Hostname}:{ManagementPort}"
		: throw new InvalidOperationException("Container not initialized. Ensure InitializeAsync() has been called.");

	/// <summary>
	/// Gets a value indicating whether the container is ready to accept connections.
	/// </summary>
	public bool IsReady => _container is not null && DockerAvailable;

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new RabbitMqBuilder()
			.WithImage(DefaultImage)
			.WithName($"rabbitmq-test-{Guid.NewGuid():N}")
			.WithUsername(DefaultUsername)
			.WithPassword(DefaultPassword)
			.WithPortBinding(DefaultAmqpPort, true)
			.WithPortBinding(DefaultManagementPort, true)
			.WithWaitStrategy(Wait.ForUnixContainer()
				.UntilPortIsAvailable(DefaultAmqpPort)
				.UntilPortIsAvailable(DefaultManagementPort))
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
