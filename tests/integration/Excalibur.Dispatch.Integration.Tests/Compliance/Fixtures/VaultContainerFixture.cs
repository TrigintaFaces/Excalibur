// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Tests.Shared.Fixtures;

using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

/// <summary>
/// Fixture for HashiCorp Vault container for encryption key management integration tests.
/// </summary>
public class VaultContainerFixture : ContainerFixtureBase
{
	private const int VaultPort = 8200;
	private const string RootToken = "test-root-token";
	private IContainer? _container;

	/// <summary>
	/// Gets the Vault server address.
	/// </summary>
	public string VaultAddress => _container is not null
		? $"http://{_container.Hostname}:{_container.GetMappedPublicPort(VaultPort)}"
		: $"http://localhost:{VaultPort}";

	/// <summary>
	/// Gets the root token for authentication.
	/// </summary>
	public string Token => RootToken;

	/// <summary>
	/// Creates a new encryption key in Vault.
	/// </summary>
	public async Task CreateKeyAsync(string keyName, CancellationToken cancellationToken = default)
	{
		var result = await _container.ExecAsync(
			new[] { "vault", "write", "-f", $"transit/keys/{keyName}" },
			cancellationToken).ConfigureAwait(true);

		if (result.ExitCode != 0)
		{
			throw new InvalidOperationException($"Failed to create key: {result.Stderr}");
		}
	}

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new ContainerBuilder()
			.WithImage("hashicorp/vault:latest")
			.WithName($"vault-compliance-test-{Guid.NewGuid():N}")
			.WithPortBinding(VaultPort, true)
			.WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", RootToken)
			.WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", $"0.0.0.0:{VaultPort}")
			.WithEnvironment("VAULT_ADDR", $"http://0.0.0.0:{VaultPort}")
			.WithEnvironment("VAULT_TOKEN", RootToken)
			.WithWaitStrategy(Wait.ForUnixContainer()
				.UntilPortIsAvailable(VaultPort)
				.UntilHttpRequestIsSucceeded(r => r
					.ForPath("/v1/sys/health")
					.ForPort(VaultPort)))
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(true);

		// Enable transit secrets engine for encryption
		await EnableTransitEngineAsync(cancellationToken).ConfigureAwait(true);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(true);
		}
	}

	private async Task EnableTransitEngineAsync(CancellationToken cancellationToken)
	{
		// Use exec to enable the transit engine
		var result = await _container.ExecAsync(
			new[] { "vault", "secrets", "enable", "transit" },
			cancellationToken).ConfigureAwait(true);

		if (result.ExitCode != 0 && !result.Stderr.Contains("already in use"))
		{
			throw new InvalidOperationException($"Failed to enable transit engine: {result.Stderr}");
		}
	}
}

/// <summary>
/// Collection definition for HashiCorp Vault integration tests.
/// </summary>
[CollectionDefinition(Name)]
public class VaultTestCollection : ICollectionFixture<VaultContainerFixture>
{
	public const string Name = "Vault";
}
