// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Consul;

/// <summary>
/// Configuration options for Consul-based leader election.
/// </summary>
public sealed class ConsulLeaderElectionOptions : LeaderElectionOptions
{
	/// <summary>
	/// Gets or sets the Consul server address.
	/// </summary>
	/// <value>
	/// The Consul server address.
	/// </value>
	[Required]
	public string ConsulAddress { get; set; } = "http://localhost:8500";

	/// <summary>
	/// Gets or sets the Consul datacenter.
	/// </summary>
	/// <value>
	/// The Consul datacenter.
	/// </value>
	public string? Datacenter { get; set; }

	/// <summary>
	/// Gets or sets the Consul ACL token.
	/// </summary>
	/// <value>
	/// The Consul ACL token.
	/// </value>
	public string? Token { get; set; }

	/// <summary>
	/// Gets or sets the key prefix in Consul KV store.
	/// </summary>
	/// <value>
	/// The key prefix in Consul KV store.
	/// </value>
	[Required]
	public string KeyPrefix { get; set; } = "excalibur/leader-election";

	/// <summary>
	/// Gets or sets the session TTL.
	/// </summary>
	/// <value>
	/// The session TTL.
	/// </value>
	public TimeSpan SessionTTL { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the lock delay (time before lock can be reacquired after release).
	/// </summary>
	/// <value>
	/// The lock delay (time before lock can be reacquired after release).
	/// </value>
	public TimeSpan LockDelay { get; set; } = TimeSpan.FromSeconds(15);

	/// <summary>
	/// Gets or sets the health check ID to use for session.
	/// </summary>
	/// <value>
	/// The health check ID to use for session.
	/// </value>
	public string? HealthCheckId { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts.
	/// </value>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 3;
}
