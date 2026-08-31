// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using k8s;

using Tests.Shared.Fixtures;

using Testcontainers.K3s;

using Xunit;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Owns a single real k3s (Rancher lightweight Kubernetes) container for the Kubernetes leader-election
/// conformance run.
/// </summary>
/// <remarks>
/// <para>
/// kbjd0b: Kubernetes leader election arbitrates through the real API server's <c>coordination.k8s.io</c>
/// Lease resource -- there is no in-process substitute that would make a mutual-exclusion arm mean
/// anything. <c>Testcontainers.K3s</c> ships an official module (unlike Consul, which has none), so this
/// fixture builds directly on it rather than hand-rolling privileged mode, cgroup mounts, and a
/// "Node controller sync successful" log-wait strategy -- <see cref="K3sBuilder"/> already configures all
/// three.
/// </para>
/// <para>
/// Docker is a HARD requirement here, not optional infrastructure: <see cref="ContainerFixtureBase.
/// AllowGracefulDegradation"/> is left at its default (<see langword="false"/>), so a failed start throws
/// instead of letting a conformance arm skip itself.
/// </para>
/// </remarks>
public sealed class KubernetesContainerFixture : ContainerFixtureBase
{
	private K3sContainer? _container;

	/// <summary>
	/// A privileged-mode container with cgroup mounts and a real control-plane boot takes longer than the
	/// default container-start budget.
	/// </summary>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(3);

	/// <summary>
	/// Builds a new <see cref="IKubernetes"/> client from the running cluster's kubeconfig, rewritten by
	/// the module to point at the container's mapped public port.
	/// </summary>
	/// <remarks>
	/// A fresh client is built per call rather than shared, matching how every other provider's
	/// conformance test builds a fresh <see cref="Microsoft.Extensions.DependencyInjection.
	/// ServiceProvider"/> (and therefore a fresh client) per election -- see
	/// <c>KubernetesLeaderElectionKitConformanceShould</c>.
	/// </remarks>
	public async Task<IKubernetes> CreateClientAsync()
	{
		if (_container is null)
		{
			throw new InvalidOperationException("Kubernetes (k3s) container is not available.");
		}

		var kubeconfigYaml = await _container.GetKubeconfigAsync().ConfigureAwait(false);
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(kubeconfigYaml));
		var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(stream);
		return new k8s.Kubernetes(config);
	}

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new K3sBuilder("rancher/k3s:v1.31.5-k3s1").Build();

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
/// Declares the shared k3s container for the leader-election conformance run.
/// </summary>
/// <remarks>
/// xUnit resolves collection definitions PER ASSEMBLY (see the sibling Postgres collection in this
/// directory for the same note) -- the definition has to live in the assembly that uses it.
/// </remarks>
[CollectionDefinition(CollectionName)]
public sealed class KubernetesLeaderElectionTestCollection : ICollectionFixture<KubernetesContainerFixture>
{
	public const string CollectionName = "Kubernetes LeaderElection Integration Tests";
}
