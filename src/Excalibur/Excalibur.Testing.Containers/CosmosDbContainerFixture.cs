// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Testcontainers.CosmosDb;

namespace Excalibur.Testing.Containers;

/// <summary>
/// A reusable Azure Cosmos DB fixture backed by the TestContainers Cosmos DB <b>emulator</b>
/// (<see cref="CosmosDbContainer"/>). Inherit or use directly to test a Cosmos DB provider implementation
/// (event store, outbox fence, inbox) against a real emulator.
/// </summary>
/// <remarks>
/// The Cosmos DB emulator serves over HTTPS with a self-signed certificate, so a consuming test MUST
/// configure its <c>CosmosClient</c> to accept it — e.g. supply a <c>CosmosClientOptions.HttpClientFactory</c>
/// (or <c>ConnectionMode.Gateway</c> with server-certificate validation bypassed) when connecting with
/// <see cref="ConnectionString"/>. This fixture only owns the container lifecycle and connection string; the
/// emulator can be slow to become ready, so keep test timeouts generous.
/// </remarks>
public class CosmosDbContainerFixture : ContainerFixtureBase
{
	private CosmosDbContainer? _container;

	/// <summary>
	/// Gets the Cosmos DB emulator connection string for the started container.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when the container has not been initialized.</exception>
	public string ConnectionString =>
		_container?.GetConnectionString()
		?? throw new InvalidOperationException("The Cosmos DB emulator container has not been initialized.");

	/// <summary>Gets the Docker image used for the Cosmos DB emulator container.</summary>
	protected virtual string Image => "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest";

	/// <inheritdoc />
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new CosmosDbBuilder().WithImage(Image).Build();
		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
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
