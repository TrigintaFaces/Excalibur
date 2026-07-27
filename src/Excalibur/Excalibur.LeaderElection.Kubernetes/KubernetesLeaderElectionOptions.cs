// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>
/// Options for Kubernetes leader election.
/// </summary>
/// <remarks>
/// Timing (lease duration, renew interval, retry interval, grace period) and the step-down and
/// candidate-metadata settings are inherited from <see cref="LeaderElectionOptions"/> as
/// <see cref="TimeSpan"/> values — this type adds only the Kubernetes-specific settings.
/// </remarks>
public sealed class KubernetesLeaderElectionOptions : LeaderElectionOptions
{
	/// <summary>
	/// Gets or sets the Kubernetes namespace. If not specified, will attempt to auto-detect.
	/// </summary>
	/// <value>
	/// The Kubernetes namespace. If not specified, will attempt to auto-detect.
	/// </value>
	public string? Namespace { get; set; }

	/// <summary>
	/// Gets or sets the lease name. If not specified, will use "{resourceName}-leader-election".
	/// </summary>
	/// <value>
	/// The lease name. If not specified, will use "{resourceName}-leader-election".
	/// </value>
	public string? LeaseName { get; set; }

	/// <summary>
	/// Gets or sets the candidate ID. If not specified, will use pod name or generate one.
	/// </summary>
	/// <value>
	/// The candidate ID. If not specified, will use pod name or generate one.
	/// </value>
	public string? CandidateId { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of retries for Kubernetes API operations.
	/// </summary>
	/// <value>
	/// The maximum number of retries for Kubernetes API operations.
	/// </value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the maximum delay between retries of Kubernetes API operations.
	/// </summary>
	/// <value>
	/// The maximum delay between retries of Kubernetes API operations.
	/// </value>
	public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}
