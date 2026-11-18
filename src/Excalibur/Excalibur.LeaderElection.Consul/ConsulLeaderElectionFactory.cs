// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Consul;
using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Consul;

/// <summary>Creates resource-specific Consul elections.</summary>
public sealed class ConsulLeaderElectionFactory : ILeaderElectionFactory
{
    private readonly IOptions<ConsulLeaderElectionOptions> _options;
    private readonly IConsulClient? _consulClient;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IFencingTokenIssuer? _fencingTokenProvider;
    private readonly IMessageFailureClassifier? _failureClassifier;

    /// <summary>Initializes the existing factory contract.</summary>
    /// <param name="options">Election settings.</param>
    /// <param name="consulClient">Optional borrowed client.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <param name="fencingTokenProvider">Optional fencing provider.</param>
    public ConsulLeaderElectionFactory(IOptions<ConsulLeaderElectionOptions> options, IConsulClient? consulClient = null,
        ILoggerFactory? loggerFactory = null, IFencingTokenIssuer? fencingTokenProvider = null)
        : this(options, consulClient, loggerFactory, fencingTokenProvider, null) { }

    /// <summary>Initializes a factory with optional failure classification.</summary>
    /// <param name="options">Election settings.</param>
    /// <param name="consulClient">Optional borrowed client.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <param name="fencingTokenProvider">Optional fencing provider.</param>
    /// <param name="failureClassifier">Optional classifier resolved by dependency injection when registered.</param>
    public ConsulLeaderElectionFactory(IOptions<ConsulLeaderElectionOptions> options, IConsulClient? consulClient,
        ILoggerFactory? loggerFactory, IFencingTokenIssuer? fencingTokenProvider, IMessageFailureClassifier? failureClassifier)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _consulClient = consulClient; _loggerFactory = loggerFactory;
        _fencingTokenProvider = fencingTokenProvider; _failureClassifier = failureClassifier;
    }

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
			_consulClient,
			_loggerFactory?.CreateLogger<ConsulLeaderElection>(),
			_fencingTokenProvider, timeProvider: null, failureClassifier: _failureClassifier);
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
			_consulClient,
			_loggerFactory?.CreateLogger<ConsulLeaderElection>(),
			_fencingTokenProvider, timeProvider: null, failureClassifier: _failureClassifier);
	}
}
