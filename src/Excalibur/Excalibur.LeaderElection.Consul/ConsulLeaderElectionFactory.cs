// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Consul;

using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Consul;

/// <summary>
/// Factory for creating Consul-based leader election instances.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ConsulLeaderElectionFactory" /> class. </remarks>
/// <param name="options"> The Consul leader election options. </param>
/// <param name="consulClient"> Optional Consul client to use. </param>
/// <param name="loggerFactory"> The logger factory. </param>
public sealed class ConsulLeaderElectionFactory(
	IOptions<ConsulLeaderElectionOptions> options,
	IConsulClient? consulClient = null,
	ILoggerFactory? loggerFactory = null) : ILeaderElectionFactory
{
	private readonly IOptions<ConsulLeaderElectionOptions> _options = options ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	public ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		var electionOptions = new ConsulLeaderElectionOptions
		{
			InstanceId = candidateId ?? _options.Value.InstanceId,
			LeaseDuration = _options.Value.LeaseDuration,
			RenewInterval = _options.Value.RenewInterval,
			RetryInterval = _options.Value.RetryInterval,
			GracePeriod = _options.Value.GracePeriod,
			EnableHealthChecks = false,
			ConsulAddress = _options.Value.ConsulAddress,
			Datacenter = _options.Value.Datacenter,
			Token = _options.Value.Token,
			KeyPrefix = _options.Value.KeyPrefix,
			SessionTTL = _options.Value.SessionTTL,
			LockDelay = _options.Value.LockDelay,
			HealthCheckId = _options.Value.HealthCheckId,
			MaxRetryAttempts = _options.Value.MaxRetryAttempts,
		};

		foreach (var kvp in _options.Value.CandidateMetadata)
		{
			electionOptions.CandidateMetadata[kvp.Key] = kvp.Value;
		}

		var optionsCopy = Options.Create(electionOptions);

		return new ConsulLeaderElection(
			resourceName,
			optionsCopy,
			consulClient,
			loggerFactory?.CreateLogger<ConsulLeaderElection>());
	}

	/// <inheritdoc />
	public IHealthBasedLeaderElection CreateHealthBasedElection(string resourceName, string? candidateId)
	{
		var electionOptions = new ConsulLeaderElectionOptions
		{
			InstanceId = candidateId ?? _options.Value.InstanceId,
			LeaseDuration = _options.Value.LeaseDuration,
			RenewInterval = _options.Value.RenewInterval,
			RetryInterval = _options.Value.RetryInterval,
			GracePeriod = _options.Value.GracePeriod,
			EnableHealthChecks = true,
			MinimumHealthScore = _options.Value.MinimumHealthScore,
			StepDownWhenUnhealthy = _options.Value.StepDownWhenUnhealthy,
			ConsulAddress = _options.Value.ConsulAddress,
			Datacenter = _options.Value.Datacenter,
			Token = _options.Value.Token,
			KeyPrefix = _options.Value.KeyPrefix,
			SessionTTL = _options.Value.SessionTTL,
			LockDelay = _options.Value.LockDelay,
			HealthCheckId = _options.Value.HealthCheckId,
			MaxRetryAttempts = _options.Value.MaxRetryAttempts,
		};

		foreach (var kvp in _options.Value.CandidateMetadata)
		{
			electionOptions.CandidateMetadata[kvp.Key] = kvp.Value;
		}

		var optionsCopy = Options.Create(electionOptions);

		return new ConsulLeaderElection(
			resourceName,
			optionsCopy,
			consulClient,
			loggerFactory?.CreateLogger<ConsulLeaderElection>());
	}
}
