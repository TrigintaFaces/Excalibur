// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Options for configuring leader election.
/// </summary>
/// <remarks>
/// <para>
/// <b>Configuration invariant (enforced by <see cref="LeaderElectionOptionsValidator"/>):</b> all timing
/// values are positive, and <see cref="RenewInterval"/> + <see cref="GracePeriod"/> plus reserved headroom
/// must be strictly less than <see cref="LeaseDuration"/>. Validation alone does not enforce a runtime
/// deadline, bound network operations or prove exclusive ownership.
/// </para>
/// <para>
/// Runtime failover and renewal behavior are provider-specific. PostgreSQL and SQL Server use
/// session-owned locks rather than a server lease with this duration; Consul has a separate session TTL.
/// Fencing is required for stale-worker safety: process-local timing cannot establish mutual exclusion
/// through arbitrary pauses or network partitions.
/// </para>
/// </remarks>
public class LeaderElectionOptions
{
	/// <summary>
	/// Gets or sets the lease duration for leadership.
	/// </summary>
	/// <value>
	/// The lease duration for leadership.
	/// </value>
	public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(15);

	/// <summary>
	/// Gets or sets the renewal interval (should be less than LeaseDuration).
	/// </summary>
	/// <value>
	/// The renewal interval (should be less than LeaseDuration).
	/// </value>
	public TimeSpan RenewInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the retry interval when attempting to acquire leadership.
	/// </summary>
	/// <value>
	/// The retry interval when attempting to acquire leadership.
	/// </value>
	public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(2);

	/// <summary>
	/// Gets or sets the identity this instance competes under, or <see langword="null" /> to let it be
	/// resolved from the environment.
	/// </summary>
	/// <value>
	/// The configured instance identifier, or <see langword="null" /> when none was configured.
	/// </value>
	/// <remarks>
	/// <para>
	/// <b>Leave this unset unless you have a stable identity to supply.</b> When it is null the identity
	/// is taken from the container's own name for the process, which is stable across a restart; only
	/// when the environment supplies nothing is an identifier generated, and a generated one is unique
	/// but NOT stable across a restart, so such an instance cannot recognise its own prior tenure.
	/// </para>
	/// <para>
	/// This property is nullable <em>so that "nobody configured one" is expressible</em>. A non-null
	/// default would make a generated identity indistinguishable from a chosen one, and every provider
	/// would have to assume the value was deliberate. Resolution happens in exactly one place, so every
	/// provider competes under an identity of the same shape.
	/// </para>
	/// </remarks>
	public string? InstanceId { get; set; }

	/// <summary>
	/// Gets or sets the additional interval in the local ownership-proof budget.
	/// </summary>
	/// <remarks>
	/// Redis, SQL Server, PostgreSQL and Consul use a monotonic local deadline of
	/// <see cref="RenewInterval"/> plus this interval from the latest successful ownership request's start.
	/// This is not a remote-candidate death timeout or a server-side lock expiration guarantee.
	/// </remarks>
	/// <value>The additional local proof interval.</value>
	public TimeSpan GracePeriod { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets a value indicating whether to enable health-based elections.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if health-based elections are enabled; otherwise, <see langword="false"/>.
	/// </value>
	public bool EnableHealthChecks { get; set; } = true;

	/// <summary>
	/// Gets or sets the minimum health score to be eligible for leadership.
	/// </summary>
	/// <value>
	/// The minimum health score to be eligible for leadership.
	/// </value>
	[Range(0.0, 1.0)]
	public double MinimumHealthScore { get; set; } = 0.8;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically step down when unhealthy.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the instance automatically steps down when unhealthy; otherwise, <see langword="false"/>.
	/// </value>
	public bool StepDownWhenUnhealthy { get; set; } = true;

	/// <summary>
	/// Gets additional metadata for this candidate.
	/// </summary>
	/// <value>
	/// Additional metadata for this candidate.
	/// </value>
	public IDictionary<string, string> CandidateMetadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
