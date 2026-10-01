// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Tests.Shared.Fixtures;

using Excalibur.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

/// <summary>
/// Fixture for HashiCorp Vault container for encryption key management integration tests.
/// </summary>
public class VaultContainerFixture : ContainerFixtureBase
{
	private const int VaultPort = 8200;
	private const string RootToken = "test-root-token";
	private IContainer? _container;

	/// <inheritdoc/>
	/// <remarks>
	/// Vault container is optional infrastructure — allow graceful degradation
	/// so a Docker exec compatibility issue doesn't crash the entire test host.
	/// Tests should check <see cref="ContainerFixtureBase.DockerAvailable"/> and skip when false.
	/// </remarks>
	protected override bool AllowGracefulDegradation => true;

	// REFUSES rather than falling back, and the refusal is the point. This property used to return
	// http://localhost:8200 when _container was null -- i.e. exactly when the container had FAILED TO
	// START -- so an arm that read it without first asserting availability would run against whatever
	// happened to be on that port. Nothing there fails loudly, which is fine. An unrelated Vault there
	// PASSES, about a server nobody provisioned, and that outcome is indistinguishable from a real run
	// in the exit code and in the arm count.
	//
	// That was unreachable in practice, because every caller asserts DockerAvailable first and that flag
	// is set true only after the container has actually started (ContainerFixtureBase:246-248). But it
	// was unreachable by CALLER CONVENTION rather than by construction, so the next arm written without
	// the assert would have reintroduced it silently. Throwing makes the trap inexpressible instead of
	// merely improbable, and it is the shape the base fixture usage example already recommends.
	private IContainer Container => _container ?? throw new InvalidOperationException(
		"The Vault container did not start, so this fixture has no address to hand out. Returning a " +
		"localhost address here would point tests at whatever else is on the port and let them pass " +
		"against a server nobody provisioned. Assert DockerAvailable before using this fixture.");

	/// <summary>
	/// Gets the Vault server address.
	/// </summary>
	/// <exception cref="InvalidOperationException">The container did not start.</exception>
	public string VaultAddress =>
		$"http://{Container.Hostname}:{Container.GetMappedPublicPort(VaultPort)}";

	/// <summary>
	/// Gets the root token for authentication.
	/// </summary>
	public string Token => RootToken;

	/// <summary>
	/// Creates a new encryption key in Vault.
	/// </summary>
	public async Task CreateKeyAsync(string keyName, CancellationToken cancellationToken = default)
	{
		var result = await Container.ExecAsync(
			new[] { "vault", "write", "-f", $"transit/keys/{keyName}" },
			cancellationToken);

		if (result.ExitCode != 0)
		{
			throw new InvalidOperationException($"Failed to create key: {result.Stderr}");
		}
	}

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new ContainerBuilder()
			.WithImage("hashicorp/vault:1.15")
			.WithName($"vault-compliance-test-{Guid.NewGuid():N}")
			.WithPortBinding(VaultPort, true)
			.WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", RootToken)
			.WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", $"0.0.0.0:{VaultPort}")
			.WithEnvironment("VAULT_ADDR", $"http://0.0.0.0:{VaultPort}")
			.WithEnvironment("VAULT_TOKEN", RootToken)
			.WithWaitStrategy(Wait.ForUnixContainer()
				.UntilHttpRequestIsSucceeded(r => r
					.ForPath("/v1/sys/health")
					.ForPort(VaultPort)
					.ForStatusCode(System.Net.HttpStatusCode.OK)))
			.Build();

		await _container.StartAsync(cancellationToken);

		// Enable transit secrets engine for encryption
		await EnableTransitEngineAsync(cancellationToken);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync();
		}
	}

	private async Task EnableTransitEngineAsync(CancellationToken cancellationToken)
	{
		// Use exec to enable the transit engine
		var result = await Container.ExecAsync(
			new[] { "vault", "secrets", "enable", "transit" },
			cancellationToken);

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
