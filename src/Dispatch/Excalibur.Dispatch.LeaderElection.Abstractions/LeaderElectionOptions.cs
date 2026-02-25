// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Options for configuring leader election.
/// </summary>
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
	/// Gets or sets the instance identifier.
	/// </summary>
	/// <value>
	/// The instance identifier.
	/// </value>
	[Required]
	public string InstanceId { get; set; } = $"{Environment.MachineName}-{Guid.NewGuid():N}"[..24];

	/// <summary>
	/// Gets or sets the grace period before considering a leader dead.
	/// </summary>
	/// <value>
	/// The grace period before considering a leader dead.
	/// </value>
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
