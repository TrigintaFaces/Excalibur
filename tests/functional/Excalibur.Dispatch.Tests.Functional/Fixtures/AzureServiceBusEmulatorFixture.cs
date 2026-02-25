// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Tests.Functional.Fixtures;

/// <summary>
///     Provides an Azure Service Bus emulator container for integration tests.
/// </summary>
public sealed class AzureServiceBusEmulatorFixture : ContainerFixtureBase
{
	private IContainer? _container;

	/// <summary>
	///     Gets the connection string for the emulator.
	/// </summary>
	public string ConnectionString { get; private set; } = string.Empty;

	/// <inheritdoc />
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new ContainerBuilder()
			.WithImage("mcr.microsoft.com/azure-service-bus/emulator")
			.WithName($"asb-emulator-{Guid.NewGuid():N}")
			.WithPortBinding(5672, true)
			.WithWaitStrategy(DotNet.Testcontainers.Builders.Wait.ForUnixContainer().UntilPortIsAvailable(5672))
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
		var port = _container.GetMappedPublicPort(5672);
		ConnectionString =
			$"Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=localKey;Port={port}";
	}

	/// <inheritdoc />
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}
