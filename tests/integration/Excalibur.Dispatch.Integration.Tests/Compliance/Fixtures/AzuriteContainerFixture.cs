// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using DotNet.Testcontainers.Builders;

using Testcontainers.Azurite;

using Tests.Shared.Fixtures;

using Excalibur.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

/// <summary>
/// Fixture for Azurite container (Azure Storage Emulator) for Key Vault-like operations.
/// Note: Azurite doesn't fully emulate Key Vault, but we can test Azure SDK integration patterns.
/// </summary>
public class AzuriteContainerFixture : ContainerFixtureBase
{
	private AzuriteContainer? _container;

	/// <summary>
	/// Gets the connection string for the Azurite blob service.
	/// </summary>
	public string BlobConnectionString => _container?.GetConnectionString() ?? string.Empty;

	/// <summary>
	/// Gets the blob service endpoint.
	/// </summary>
	public Uri BlobEndpoint => _container is not null
		? new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(10000)}")
		: new Uri("http://localhost:10000");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		// --skipApiVersionCheck is REQUIRED, not a convenience. The Azure.Storage.Blobs SDK this repo
		// references negotiates a service API version newer than any published Azurite image accepts,
		// so Azurite rejects the request outright ("The API version <ver> is not supported") before any
		// blob operation runs. That is client/emulator VERSION SKEW, not an outage: the container
		// starts, the port opens, and the wait strategy below is satisfied -- it just refuses the
		// header on the first real call. The flag is Azurite's own documented remedy.
		//
		// It relaxes only the version GATE, not blob semantics, so conditional writes and ETag
		// behaviour remain enforced.
		_container = new AzuriteBuilder()
			.WithImage("mcr.microsoft.com/azure-storage/azurite:3.36.0")
			.WithName($"azurite-compliance-test-{Guid.NewGuid():N}")
			.WithCommand("--skipApiVersionCheck")
			.WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(10000))
			.Build();

		await _container.StartAsync(cancellationToken);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync();
		}
	}
}

/// <summary>
/// Collection definition for Azurite integration tests.
/// </summary>
[CollectionDefinition(Name)]
public class AzuriteTestCollection : ICollectionFixture<AzuriteContainerFixture>
{
	public const string Name = "Azurite";
}
