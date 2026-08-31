// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Consul;

/// <summary>
/// Fluent builder for configuring Consul leader election settings.
/// </summary>
/// <remarks>
/// <para>
/// Connection methods (<see cref="Address"/>, <see cref="BindConfiguration"/>)
/// use last-wins semantics: setting one clears the other.
/// </para>
/// <para>
/// Non-connection methods (<see cref="Token"/>, <see cref="Datacenter"/>,
/// <see cref="SessionTtl"/>, <see cref="LockKey"/>) are additive
/// and can be combined with any connection method.
/// </para>
/// </remarks>
public interface ILeaderElectionConsulBuilder
{
	/// <summary>Sets the Consul server address (e.g. "http://localhost:8500").</summary>
	ILeaderElectionConsulBuilder Address(string address);

	/// <summary>Sets the Consul ACL token for authentication.</summary>
	ILeaderElectionConsulBuilder Token(string token);

	/// <summary>Sets the Consul datacenter.</summary>
	ILeaderElectionConsulBuilder Datacenter(string datacenter);

	/// <summary>Sets the session TTL for Consul sessions.</summary>
	ILeaderElectionConsulBuilder SessionTtl(TimeSpan ttl);

	/// <summary>Sets the key prefix in the Consul KV store for leader election locks.</summary>
	ILeaderElectionConsulBuilder LockKey(string lockKey);

	/// <summary>
	/// Names the resource this application contends for, and makes a single <c>ILeaderElection</c> for it
	/// resolvable from the container.
	/// </summary>
	/// <param name="resourceName">
	/// The resource name. It is combined with the key prefix to form the Consul KV key, so it must be unique
	/// to this application across every application pointed at the same Consul cluster.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// Without this, the registration provides an <c>ILeaderElectionFactory</c> only, and a per-resource
	/// election must be created from it explicitly. With it, the outbox drain is fenced on the named
	/// election automatically. The name is required rather than defaulted because a Consul key is shared
	/// across every application on the cluster, so a framework-chosen name would put unrelated applications
	/// into contention for one lock.
	/// </remarks>
	ILeaderElectionConsulBuilder ResourceName(string resourceName);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	ILeaderElectionConsulBuilder BindConfiguration(string sectionPath);
}
