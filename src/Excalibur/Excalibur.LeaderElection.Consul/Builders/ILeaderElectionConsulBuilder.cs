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

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	ILeaderElectionConsulBuilder BindConfiguration(string sectionPath);
}
