// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.LeaderElection;

using k8s;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>
/// Factory for creating Kubernetes leader election instances.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="KubernetesLeaderElectionFactory" /> class. </remarks>
/// <param name="kubernetesClient"> The Kubernetes client. </param>
/// <param name="options"> The Kubernetes leader election options. </param>
/// <param name="loggerFactory"> The logger factory. </param>
public sealed class KubernetesLeaderElectionFactory(
	IKubernetes kubernetesClient,
	IOptions<KubernetesLeaderElectionOptions> options,
	ILoggerFactory? loggerFactory = null) : ILeaderElectionFactory
{
	private readonly IKubernetes _kubernetesClient = kubernetesClient ?? throw new ArgumentNullException(nameof(kubernetesClient));
	private readonly IOptions<KubernetesLeaderElectionOptions> _options = options ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	public ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		var electionOptions = new KubernetesLeaderElectionOptions
		{
			Namespace = _options.Value.Namespace,
			LeaseName = _options.Value.LeaseName,
			CandidateId = candidateId ?? _options.Value.CandidateId,
			LeaseDurationSeconds = _options.Value.LeaseDurationSeconds,
			RenewIntervalMilliseconds = _options.Value.RenewIntervalMilliseconds,
			RetryIntervalMilliseconds = _options.Value.RetryIntervalMilliseconds,
			GracePeriodSeconds = _options.Value.GracePeriodSeconds,
			MaxRetries = _options.Value.MaxRetries,
			MaxRetryDelayMilliseconds = _options.Value.MaxRetryDelayMilliseconds,
			EnableHealthChecks = false,
			StepDownWhenUnhealthy = false,
		};

		foreach (var kvp in _options.Value.CandidateMetadata)
		{
			electionOptions.CandidateMetadata[kvp.Key] = kvp.Value;
		}

		var optionsCopy = Options.Create(electionOptions);

		return new KubernetesLeaderElection(
			_kubernetesClient,
			resourceName,
			optionsCopy,
			loggerFactory?.CreateLogger<KubernetesLeaderElection>());
	}

	/// <inheritdoc />
	public IHealthBasedLeaderElection CreateHealthBasedElection(string resourceName, string? candidateId)
	{
		var electionOptions = new KubernetesLeaderElectionOptions
		{
			Namespace = _options.Value.Namespace,
			LeaseName = _options.Value.LeaseName,
			CandidateId = candidateId ?? _options.Value.CandidateId,
			LeaseDurationSeconds = _options.Value.LeaseDurationSeconds,
			RenewIntervalMilliseconds = _options.Value.RenewIntervalMilliseconds,
			RetryIntervalMilliseconds = _options.Value.RetryIntervalMilliseconds,
			GracePeriodSeconds = _options.Value.GracePeriodSeconds,
			MaxRetries = _options.Value.MaxRetries,
			MaxRetryDelayMilliseconds = _options.Value.MaxRetryDelayMilliseconds,
			EnableHealthChecks = true,
			MinimumHealthScore = _options.Value.MinimumHealthScore,
			StepDownWhenUnhealthy = _options.Value.StepDownWhenUnhealthy,
		};

		foreach (var kvp in _options.Value.CandidateMetadata)
		{
			electionOptions.CandidateMetadata[kvp.Key] = kvp.Value;
		}

		var optionsCopy = Options.Create(electionOptions);

		return new KubernetesLeaderElection(
			_kubernetesClient,
			resourceName,
			optionsCopy,
			loggerFactory?.CreateLogger<KubernetesLeaderElection>());
	}
}
