// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using Azure.Messaging.ServiceBus;

using Testcontainers.ServiceBus;

using Tests.Shared.Infrastructure;

namespace Tests.Shared.Fixtures;

/// <summary>
/// TestContainer fixture for Azure Service Bus integration tests using the emulator.
/// </summary>
/// <remarks>
/// <para>
/// Provides a shared Azure Service Bus emulator container. All Azure Service Bus
/// integration tests should share this fixture via
/// <c>[Collection(ContainerCollections.AzureServiceBus)]</c> rather than creating
/// per-class containers, to reduce Docker resource contention and prevent
/// test host hangs from undisposed static containers.
/// </para>
/// <para>
/// The fixture exposes a pre-configured <see cref="ServiceBusClient"/> and connection
/// string. Test classes are responsible for creating their own queues via
/// <see cref="Azure.Messaging.ServiceBus.Administration.ServiceBusAdministrationClient"/>
/// using the <see cref="ConnectionString"/>.
/// </para>
/// </remarks>
#pragma warning disable CA1001 // Disposal is handled by ContainerFixtureBase.DisposeAsync -> DisposeContainerAsync
public sealed class AzureServiceBusContainerFixture : ContainerFixtureBase
#pragma warning restore CA1001
{
	private ServiceBusContainer? _container;
	private ServiceBusClient? _client;

	/// <summary>
	/// Gets the pre-configured Service Bus client connected to the emulator container.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when accessed before the container has been initialized.
	/// Tests should check <see cref="ContainerFixtureBase.DockerAvailable"/> first.
	/// </exception>
	public ServiceBusClient Client => _client
		?? throw new InvalidOperationException("ServiceBusClient is not available. Check DockerAvailable before accessing.");

	/// <summary>
	/// Gets the emulator connection string.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when accessed before the container has been initialized.
	/// </exception>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container is not available. Check DockerAvailable before accessing.");

	/// <inheritdoc/>
	/// <remarks>
	/// The Azure Service Bus emulator can take longer to start than typical containers
	/// due to its initialization sequence. Use 2 minutes with CI scaling.
	/// </remarks>
	protected override TimeSpan ContainerStartTimeout => TestTimeouts.Scale(TimeSpan.FromMinutes(2));

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new ServiceBusBuilder()
			.WithAcceptLicenseAgreement(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		_client = new ServiceBusClient(_container.GetConnectionString());
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_client is not null)
		{
			await _client.DisposeAsync().ConfigureAwait(false);
		}

		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}
