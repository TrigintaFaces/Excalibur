// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Owns a single real Consul server container for the Consul leader-election conformance run.
/// </summary>
/// <remarks>
/// <para>
/// kbjd0b: there is no <c>Testcontainers.Consul</c> module in this repository (nor, at the time this was
/// written, on NuGet at all) -- but Testcontainers for .NET runs an arbitrary image through the generic
/// <see cref="ContainerBuilder"/>, and a module is only ever a convenience wrapper over exactly that. The
/// official <c>hashicorp/consul</c> image's default command is <c>agent -dev -client 0.0.0.0</c>, which
/// bootstraps a single-node dev server that is its own leader immediately -- no extra configuration is
/// needed to make it ready for a leader-election conformance run.
/// </para>
/// <para>
/// Docker is a HARD requirement here, not optional infrastructure: <see cref="ContainerFixtureBase.
/// AllowGracefulDegradation"/> is left at its default (<see langword="false"/>), so a failed start throws
/// instead of letting a conformance arm skip itself. Mutual exclusion is Consul's whole guarantee for this
/// provider; a skip-gated arm that silently never runs is the exact gap this fixture exists to close.
/// </para>
/// </remarks>
public sealed class ConsulContainerFixture : ContainerFixtureBase
{
	private const int ConsulHttpPort = 8500;
	private IContainer? _container;

	/// <summary>
	/// Gets the base HTTP address of the running Consul server (e.g. <c>http://localhost:32771</c>).
	/// </summary>
	public string ConsulAddress => _container is not null
		? $"http://{_container.Hostname}:{_container.GetMappedPublicPort(ConsulHttpPort)}"
		: throw new InvalidOperationException("Consul container is not available.");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new ContainerBuilder()
			.WithImage("hashicorp/consul:1.19")
			.WithName($"consul-le-conformance-{Guid.NewGuid():N}")
			.WithPortBinding(ConsulHttpPort, true)
			.WithWaitStrategy(Wait.ForUnixContainer()
				.UntilHttpRequestIsSucceeded(r => r.ForPath("/v1/status/leader").ForPort(ConsulHttpPort)))
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

/// <summary>
/// Declares the shared Consul container for the leader-election conformance run.
/// </summary>
/// <remarks>
/// xUnit resolves collection definitions PER ASSEMBLY (see the sibling Postgres collection in this
/// directory for the same note) -- the definition has to live in the assembly that uses it.
/// </remarks>
[CollectionDefinition(CollectionName)]
public sealed class ConsulLeaderElectionTestCollection : ICollectionFixture<ConsulContainerFixture>
{
	public const string CollectionName = "Consul LeaderElection Integration Tests";
}
