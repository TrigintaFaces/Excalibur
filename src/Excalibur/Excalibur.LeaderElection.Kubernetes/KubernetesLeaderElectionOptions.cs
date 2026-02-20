// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>
/// Options for Kubernetes leader election.
/// </summary>
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
	/// Gets or sets the lease duration in seconds.
	/// </summary>
	/// <value>
	/// The lease duration in seconds.
	/// </value>
	[Range(1, int.MaxValue)]
	public int LeaseDurationSeconds { get; set; } = 15;

	/// <summary>
	/// Gets or sets the renewal interval in milliseconds.
	/// </summary>
	/// <value>
	/// The renewal interval in milliseconds.
	/// </value>
	[Range(1, int.MaxValue)]
	public int RenewIntervalMilliseconds { get; set; } = 5000;

	/// <summary>
	/// Gets or sets the retry interval in milliseconds when attempting to acquire leadership.
	/// </summary>
	/// <value>
	/// The retry interval in milliseconds when attempting to acquire leadership.
	/// </value>
	[Range(1, int.MaxValue)]
	public int RetryIntervalMilliseconds { get; set; } = 2000;

	/// <summary>
	/// Gets or sets the grace period in seconds before considering a leader dead.
	/// </summary>
	/// <value>
	/// The grace period in seconds before considering a leader dead.
	/// </value>
	[Range(1, int.MaxValue)]
	public int GracePeriodSeconds { get; set; } = 5;

	/// <summary>
	/// Gets or sets the maximum number of retries for Kubernetes API operations.
	/// </summary>
	/// <value>
	/// The maximum number of retries for Kubernetes API operations.
	/// </value>
	[Range(0, int.MaxValue)]
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the maximum retry delay in milliseconds.
	/// </summary>
	/// <value>
	/// The maximum retry delay in milliseconds.
	/// </value>
	[Range(1, int.MaxValue)]
	public int MaxRetryDelayMilliseconds { get; set; } = 5000;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically step down when unhealthy.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if automatically stepping down when unhealthy; otherwise, <see langword="false"/>.
	/// </value>
	public new bool StepDownWhenUnhealthy { get; set; } = true;

	/// <summary>
	/// Gets additional metadata for this candidate.
	/// </summary>
	/// <value>
	/// The additional metadata for this candidate.
	/// </value>
	public new IDictionary<string, string> CandidateMetadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
