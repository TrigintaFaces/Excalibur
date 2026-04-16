// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>
/// Fluent builder for configuring Kubernetes leader election settings.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="InCluster"/> and <see cref="BindConfiguration"/> methods
/// use last-wins semantics for the connection strategy.
/// </para>
/// <para>
/// Non-connection methods (<see cref="Namespace"/>, <see cref="LeaseName"/>,
/// <see cref="LeaseIdentity"/>, <see cref="LeaseDuration"/>,
/// <see cref="RenewDeadline"/>, <see cref="RetryPeriod"/>) are additive.
/// </para>
/// </remarks>
public interface ILeaderElectionKubernetesBuilder
{
	/// <summary>Sets the Kubernetes namespace for the lease resource.</summary>
	ILeaderElectionKubernetesBuilder Namespace(string @namespace);

	/// <summary>Sets the lease resource name.</summary>
	ILeaderElectionKubernetesBuilder LeaseName(string leaseName);

	/// <summary>Sets the identity of this candidate (defaults to pod name).</summary>
	ILeaderElectionKubernetesBuilder LeaseIdentity(string identity);

	/// <summary>Sets the lease duration in seconds.</summary>
	ILeaderElectionKubernetesBuilder LeaseDuration(int seconds);

	/// <summary>Sets the renewal deadline interval in milliseconds.</summary>
	ILeaderElectionKubernetesBuilder RenewDeadline(int milliseconds);

	/// <summary>Sets the retry period in milliseconds for acquiring leadership.</summary>
	ILeaderElectionKubernetesBuilder RetryPeriod(int milliseconds);

	/// <summary>Configures the client to use in-cluster credentials (auto-detected).</summary>
	ILeaderElectionKubernetesBuilder InCluster();

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	ILeaderElectionKubernetesBuilder BindConfiguration(string sectionPath);
}
