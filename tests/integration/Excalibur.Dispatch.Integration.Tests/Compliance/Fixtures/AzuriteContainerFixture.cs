// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using DotNet.Testcontainers.Builders;

using Testcontainers.Azurite;

using Tests.Shared.Fixtures;

using Excalibur.Dispatch.Compliance;
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
		_container = new AzuriteBuilder()
			.WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
			.WithName($"azurite-compliance-test-{Guid.NewGuid():N}")
			.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(10000))
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(true);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(true);
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
